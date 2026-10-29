using UnityEngine;

public enum MiniAdvanceKind { User, Rapid }

// AdvanceGate + DialogueAdvanceDispatcher + 빨리 감기/스킵 컨트롤러를 한데 모은 흉내.
//
// 넘기기 요청 하나는 관문을 지나야 한다:
//   시크 중이면 거절           — 시크가 끝날 때까지는 사람이 끼어들 자리가 없다.
//   메인 루틴이 바쁘면 거절     — 줄 앞머리의 대기 명령(pause · wait)이 도는 동안. CommandRunScope.CommandIsPlaying.
//   통과하면                   — 줄이 다 안 보였으면 서두르기, 다 보였으면 다음 줄.
//
// 빨리 감기와 스킵(Rapid)은 ked-presentation-runtime의 AdvanceGate와 똑같이 **바쁨 검사를 건너뛴다**.
// 이 샘플은 그 틈을 그대로 둔다: 대기 명령이 도는 중에 다음 줄이 들어오면 실행기는 앞 메인 루틴을
// 멈추지 않은 채 새 스텝을 시작한다. 감사가 그 겹침과 늦은 쓰기를 잡는다(대본 l06).
public sealed class MiniPlaybackControls
{
    private const float UserCooldownSeconds = 0.13f;
    private const float RapidIntervalSeconds = 0.05f;

    private readonly MiniPlaybackModes _modes;
    private readonly MiniLineState _line;
    private readonly MiniDialogueRunner _runner;
    private readonly ICommandRunScopeProvider _scopes;
    private readonly MiniTrace _trace;

    private double _userCooldownUntil = double.NegativeInfinity;
    private double _nextRapidAt = double.NegativeInfinity;

    public MiniPlaybackControls(
        MiniPlaybackModes modes,
        MiniLineState line,
        MiniDialogueRunner runner,
        ICommandRunScopeProvider scopes,
        MiniTrace trace)
    {
        _modes = modes;
        _line = line;
        _runner = runner;
        _scopes = scopes;
        _trace = trace;
    }

    public void Tick()
    {
        if (!_modes.IsFastForward && !_modes.SkipActive)
            return;

        double now = Time.unscaledTimeAsDouble;

        if (now < _nextRapidAt)
            return;

        _nextRapidAt = now + RapidIntervalSeconds;
        Dispatch(MiniAdvanceKind.Rapid);
    }

    public void Advance() => Dispatch(MiniAdvanceKind.User);

    // 노드 끝까지 한 번에. 압축은 아니다 — 명령은 보통대로 진입하고, 줄이 빨리 넘어가며 스텝 경계가 끝낸다.
    public bool RequestSkip()
    {
        if (_line.IsSeeking || !_runner.IsRunning)
        {
            _trace.Write("skip refused - " + (_line.IsSeeking ? "seek active" : "node not running"));
            return false;
        }

        _modes.SkipActive = true;
        _trace.Write("skip - advancing every line until the node ends (no compression)");
        return true;
    }

    private void Dispatch(MiniAdvanceKind kind)
    {
        if (!_runner.IsRunning || !TryAccept(kind))
            return;

        if (!_line.IsLineFullyShown)
            _runner.RequestHurryUp();
        else
            _runner.RequestNextLine();
    }

    private bool TryAccept(MiniAdvanceKind kind)
    {
        if (_line.IsSeeking)
            return Reject(kind, "seek active");

        CommandRunScope scope = _scopes.CurrentScope;

        if (kind == MiniAdvanceKind.User && scope != null && scope.CommandIsPlaying)
            return Reject(kind, "main routine busy (a waited command is running)");

        if (kind == MiniAdvanceKind.User)
        {
            double now = Time.unscaledTimeAsDouble;

            if (now < _userCooldownUntil)
                return false;

            _userCooldownUntil = now + UserCooldownSeconds;
        }

        return true;
    }

    private bool Reject(MiniAdvanceKind kind, string reason)
    {
        if (kind == MiniAdvanceKind.User)
            _trace.Write($"advance refused - {reason}");

        return false;
    }
}
