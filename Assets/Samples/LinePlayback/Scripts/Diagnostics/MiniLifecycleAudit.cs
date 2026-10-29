using System.Collections.Generic;

// 생명주기 감사. 스텝 경계마다 "앞 줄이 제대로 끝났나"를 판정한다(YarnPlaybackDriver → LifecycleProbe가 부른다).
//
//   model   정리된 무대 == 순수 모델(MiniStageModel)? — 어떤 길로 왔든(보통 · 서두르기 · Finish · 압축 · 시크) 같아야 한다.
//   replay  같은 줄을 다시 지났을 때(롤백 시크 · 다시 시작) 정리된 무대 == 처음 그 줄에서 본 무대?
//   overlap 앞 스텝의 메인 루틴이 아직 일을 붙든 채 다음 스텝이 시작됐다.
//
// late / stale write는 무대가 직접 센다(MiniStage.Write).
public sealed class MiniLifecycleAudit
{
    public sealed class LineResult
    {
        public string Key;
        public bool? ModelOk;    // 마지막 정산 결과. null = 아직 정산 전
        public bool? ReplayOk;   // 처음 이후 다시 지났을 때의 결과. null = 아직 한 번만 지났다
        public int Settles;
    }

    private readonly MiniStageModel _model;
    private readonly MiniTrace _trace;

    private readonly List<LineResult> _results = new();
    private readonly Dictionary<string, LineResult> _byKey = new();
    private readonly Dictionary<string, string> _firstSnapshot = new();

    private string _pendingKey;

    public MiniLifecycleAudit(MiniStageModel model, MiniTrace trace)
    {
        _model = model;
        _trace = trace;
    }

    public IReadOnlyList<LineResult> Results => _results;

    public int Overlaps { get; private set; }
    public int ModelMismatches { get; private set; }
    public int ReplayMismatches { get; private set; }

    // 마지막 불일치의 기대값과 실제값 — 화면에 그대로 보인다.
    public string LastMismatch { get; private set; }

    // 새 세션(다시 시작 · 롤백 리플레이). 모델은 루트부터 다시 접는다. 줄별 결과와 처음 사진은 남긴다.
    public void BeginRun()
    {
        _model.Reset();
        _pendingKey = null;
    }

    // 처음부터 — 결과도 지운다.
    public void ResetAll()
    {
        _results.Clear();
        _byKey.Clear();
        _firstSnapshot.Clear();

        Overlaps = 0;
        ModelMismatches = 0;
        ReplayMismatches = 0;
        LastMismatch = null;

        BeginRun();
    }

    public void CheckOverlap()
    {
        if (_pendingKey == null)
            return;

        if (!_trace.HasForegroundWorkInFlight(out string what))
            return;

        Overlaps++;
        _trace.Write($"OVERLAP - leaving [{_pendingKey}] while its main routine still holds {what}");
    }

    public void BeginStep(string lineKey, IReadOnlyList<CommandSpecBase> specs)
    {
        for (int i = 0; i < specs.Count; i++)
            _model.Apply(specs[i]);

        _pendingKey = lineKey;

        if (!_byKey.ContainsKey(_pendingKey))
        {
            var result = new LineResult { Key = _pendingKey };
            _results.Add(result);
            _byKey.Add(_pendingKey, result);
        }
    }

    public void Settle(string actual)
    {
        if (_pendingKey == null)
            return;

        LineResult result = _byKey[_pendingKey];
        result.Settles++;

        string expected = _model.Snapshot();
        result.ModelOk = actual == expected;

        if (result.ModelOk == false)
        {
            ModelMismatches++;
            LastMismatch = $"[{_pendingKey}] model   {expected}\n[{_pendingKey}] actual  {actual}";
            _trace.Write($"MISMATCH - [{_pendingKey}] settled stage differs from the model");
        }

        if (_firstSnapshot.TryGetValue(_pendingKey, out string first))
        {
            result.ReplayOk = actual == first;

            if (result.ReplayOk == false)
            {
                ReplayMismatches++;
                LastMismatch = $"[{_pendingKey}] first   {first}\n[{_pendingKey}] replay  {actual}";
                _trace.Write($"MISMATCH - [{_pendingKey}] replay differs from the first time");
            }
        }
        else
        {
            _firstSnapshot.Add(_pendingKey, actual);
        }

        _pendingKey = null;
    }
}
