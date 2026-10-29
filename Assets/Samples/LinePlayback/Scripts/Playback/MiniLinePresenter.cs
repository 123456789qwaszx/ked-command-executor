using System.Collections;
using UnityEngine;

// VNLinePresentationPhase의 축약판. 화면이 지금 줄이 어디까지 왔는지 보여 준다.
public enum MiniLinePhase
{
    None,
    LineEntered,        // 줄 좌표가 정해지고, 쌓인 명령이 이 줄의 스텝으로 재생됐다
    SeekPassThrough,    // 시크 중 — 보이지 않고 지나간다(명령은 압축으로 최종 상태에 선다)
    Typing,             // 타자기
    WaitingForAdvance,  // 다 보였다. 다음 줄 요청을 기다린다
    Completed,
    Stale,              // 정상 경계 전에 무너졌다(롤백 · 취소). 공유 상태에 더 쓰지 않는다
}

// 타자기 — 글자 수만 센다.
public sealed class MiniTypewriter
{
    public const float CharsPerSecond = 40f;

    private float _visible;

    public string Speaker { get; private set; }
    public string Text { get; private set; }

    public bool IsDone => Text == null || _visible >= Text.Length;

    public string VisibleText =>
        Text == null ? string.Empty : Text.Substring(0, Mathf.Min(Text.Length, (int)_visible));

    public void Begin(string speaker, string text)
    {
        Speaker = speaker;
        Text = text ?? string.Empty;
        _visible = 0f;
    }

    public void Tick(float deltaTime, float speed) => _visible += deltaTime * CharsPerSecond * speed;

    public void Complete() => _visible = Text?.Length ?? 0;

    public void Clear()
    {
        Speaker = null;
        Text = null;
        _visible = 0f;
    }
}

// CustomLinePresenter + VNLinePresentationFlow 흉내 — 대사 한 줄의 트랜잭션.
//
//   줄 진입   기록(롤백 좌표) → 쌓인 명령을 이 줄의 스텝으로 재생(PlayCollected) → 시크 판정
//   시크      표적 전 줄은 보이지 않고 지나간다. 표적에 닿으면 시크를 내리고 보통 재생으로
//   표시      타자기(서두르기 요청이면 즉시 완성) → 다 보였다 → 다음 줄 요청을 기다린다
//
// 무너짐(Stale): 롤백 · 취소는 세대를 올리고 토큰을 취소한다. 줄은 다음 확인 지점에서 그것을 보고 손을 뗀다 —
// 그 뒤로는 줄 상태도 타자기도 건드리지 않는다.
public sealed class MiniLinePresenter : IMiniLinePresenter
{
    private readonly MiniLineState _state;
    private readonly MiniRollbackHistory _history;
    private readonly MiniPlaybackDriver _driver;
    private readonly MiniTypewriter _typewriter;
    private readonly MiniPlaybackModes _modes;
    private readonly MiniTrace _trace;

    private int _generation;

    public MiniLinePhase Phase { get; private set; } = MiniLinePhase.None;
    public string CurrentLineId { get; private set; } = "-";

    public MiniLinePresenter(
        MiniLineState state,
        MiniRollbackHistory history,
        MiniPlaybackDriver driver,
        MiniTypewriter typewriter,
        MiniPlaybackModes modes,
        MiniTrace trace)
    {
        _state = state;
        _history = history;
        _driver = driver;
        _typewriter = typewriter;
        _modes = modes;
        _trace = trace;
    }

    // 지금 줄을 무효로 만든다. 줄 루틴은 다음 확인 지점에서 Stale로 빠진다.
    public void AbortCurrentLine()
    {
        _generation++;

        if (Phase != MiniLinePhase.None && Phase != MiniLinePhase.Completed && Phase != MiniLinePhase.SeekPassThrough)
            Phase = MiniLinePhase.Stale;

        _state.MarkLineTornDown();
    }

    public IEnumerator RunLine(MiniInstruction line, MiniLineToken token)
    {
        int generation = _generation;

        _state.MarkLineEntered();
        MiniLineKey key = _history.Add(line.LineId);
        CurrentLineId = key.Id;

        _driver.PlayCollected(key);
        Phase = MiniLinePhase.LineEntered;

        if (_state.IsSeeking)
        {
            if (!_state.IsSeekTarget(key))
            {
                Phase = MiniLinePhase.SeekPassThrough;
                _typewriter.Clear();
                _state.MarkLineDisplayed();
                yield break;
            }

            _state.ClearSeek();
            _trace.Write($"[{key.Id}] seek target reached - back to normal playback");
        }

        _typewriter.Begin(line.Speaker, line.Text);
        Phase = MiniLinePhase.Typing;

        while (!_typewriter.IsDone)
        {
            if (IsStale(generation, token))
                yield break;

            if (token.HurryUpRequested)
                _typewriter.Complete();
            else
                _typewriter.Tick(Time.deltaTime, _modes.SpeedUp ? MiniPlaybackModes.SpeedUpScale : 1f);

            if (!_typewriter.IsDone)
                yield return null;
        }

        _state.MarkLineDisplayed();
        Phase = MiniLinePhase.WaitingForAdvance;

        while (!token.NextContentRequested)
        {
            if (IsStale(generation, token))
                yield break;

            yield return null;
        }

        Phase = MiniLinePhase.Completed;
    }

    private bool IsStale(int generation, MiniLineToken token)
    {
        if (generation == _generation && !token.IsCancelled)
            return false;

        Phase = MiniLinePhase.Stale;
        return true;
    }
}
