using System.Collections.Generic;
using System.Text;

// 샘플 전용 — 스텝 경계를 들여다보는 관측점(LifecycleProbe). 원본에는 없다.
public interface IPlaybackStepProbe
{
    // 앞 스텝을 정리하기 직전 — 메인 루틴이 아직 붙든 일이 있나(겹침).
    void BeforeStepBoundary();

    // 앞 스텝을 CleanupStep(Finish)로 정리한 직후, 새 스텝을 재생하기 직전 — 정리된 무대를 판정하고 새 스텝을 적는다.
    void OnStepStarting(IReadOnlyList<CommandSpecBase> specs);
}

// ked-presentation-runtime의 YarnPlaybackDriver를 옮긴 것.
//
// Yarn이 건넨 명령은 실행되지 않고 여기 쌓인다(Enqueue). VNLinePresentationFlow가 다음 줄에 들어가며
// PlayCollected를 부르면, 쌓인 것 전부가 그 줄의 스텝 하나로 재생된다 — 실행기의 PlaySpecs가 앞 스텝을
// CleanupStep(Finish)로 정리한 뒤 새 커맨드를 진입시킨다.
//
// 바뀐 곳: [샘플] 표시가 붙은 관측점 두 줄.
public sealed class YarnPlaybackDriver
{
    private readonly CommandExecutor _executor;
    private readonly ICommandRunScopeProvider _scopeProvider;
    private readonly IPlaybackStepProbe _probe;

    private readonly List<CommandSpecBase> _collectedSpecs = new();

    private CommandRunScope CurrentScope => _scopeProvider?.CurrentScope;

    public YarnPlaybackDriver(CommandExecutor executor, ICommandRunScopeProvider scopeProvider, IPlaybackStepProbe probe = null)
    {
        _executor = executor;
        _scopeProvider = scopeProvider;
        _probe = probe;
    }

    public int PendingCount => _collectedSpecs.Count;

    public void Enqueue(CommandSpecBase spec) => _collectedSpecs.Add(spec);

    public void PlayCollected()
    {
        var specs = new List<CommandSpecBase>(_collectedSpecs);
        _collectedSpecs.Clear();

        if (_probe != null)
        {
            // [샘플] 앞 스텝 정리를 PlaySpecs보다 먼저 해서, 정리된 무대를 감사가 볼 수 있게 한다.
            //        PlaySpecs 안의 같은 정리는 이미 빈 장부를 만나 아무것도 하지 않는다 — 실행 결과는 원본과 같다.
            _probe.BeforeStepBoundary();
            CurrentScope?.CleanupStep(CleanupPolicy.Finish);
            _probe.OnStepStarting(specs);
        }
        else if (specs.Count == 0)
        {
            CurrentScope?.CleanupStep(CleanupPolicy.Finish);
        }

        _executor.PlaySpecs(specs, CurrentScope);
    }

    // 쌓인 명령을 실행하지 않고 버린다. 버린 것이 있으면 개수와 요약("이름×개수, …")을 돌려준다(로그용).
    public bool TryDiscardPending(out int count, out string summary)
    {
        count = _collectedSpecs.Count;

        if (count == 0)
        {
            summary = string.Empty;
            return false;
        }

        summary = Describe(_collectedSpecs);
        _collectedSpecs.Clear();
        return true;
    }

    // 이어진 같은 종류는 하나로 센다: MiniFadeSpec×3, MiniPauseSpec, …
    private static string Describe(List<CommandSpecBase> specs)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < specs.Count; )
        {
            string name = specs[i] != null ? specs[i].GetType().Name : "null";
            int run = 1;

            while (i + run < specs.Count &&
                   (specs[i + run] != null ? specs[i + run].GetType().Name : "null") == name)
                run++;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(name);

            if (run > 1)
                sb.Append('x').Append(run);

            i += run;
        }

        return sb.ToString();
    }
}
