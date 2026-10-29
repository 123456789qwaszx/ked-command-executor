using System.Collections.Generic;

// 샘플 전용 관측점 — 옮겨 온 재생 계층(YarnPlaybackDriver · ScenePresentation)이 [샘플] 표시 줄에서 부른다.
// 재생 흐름에는 손대지 않는다. 지나가는 순간을 장부 · 무대 · 감사에 알릴 뿐이다.
//
//   세션 열림   ScenePresentation.BeginScene / PrepareReplay — 무대의 활성 스코프를 맞추고 장부를 새로 연다
//   스텝 경계   YarnPlaybackDriver.PlayCollected — 겹침 검사 → (Finish 정리) → 정산 → 새 스텝 기록
//   세션 닫힘   ScenePresentation.StopAsync — 그 순간 돌던 · 못 들어간 명령을 적고, 활성 스코프를 내린다
//   노드 끝     마지막 줄은 다음 경계가 없다 — 그 줄의 명령이 스스로 끝나면 정산한다(Tick)
public sealed class LifecycleProbe : IPlaybackStepProbe
{
    private readonly MiniStage _stage;
    private readonly MiniTrace _trace;
    private readonly MiniLifecycleAudit _audit;
    private readonly RollbackHistory _rollbackHistory;

    private bool _finalSettlePending;

    public LifecycleProbe(MiniStage stage, MiniTrace trace, MiniLifecycleAudit audit, RollbackHistory rollbackHistory)
    {
        _stage = stage;
        _trace = trace;
        _audit = audit;
        _rollbackHistory = rollbackHistory;
    }

    public int SessionCount { get; private set; }

    public void OnRunStarted(CommandRunScope scope, string reason)
    {
        SessionCount++;

        _stage.ActiveScope = scope;
        _stage.CurrentStep = 0;
        _trace.BeginRun(SessionCount, reason);
        _audit.BeginRun();
        _finalSettlePending = false;
    }

    public void OnRunEnded()
    {
        _trace.CancelUnfinished("PresentationScopeSession.End");

        // 이 뒤로 이전 스코프로 들어오는 쓰기는 전부 stale write다.
        _stage.ActiveScope = null;
        _finalSettlePending = false;
    }

    public void BeforeStepBoundary() => _audit.CheckOverlap();

    public void OnStepStarting(IReadOnlyList<CommandSpecBase> specs)
    {
        _audit.Settle(_stage.Snapshot());

        string key = CurrentLineKey();

        _trace.BeginStep(key, specs.Count);
        _stage.CurrentStep = _trace.CurrentStep;
        _audit.BeginStep(key, specs);
    }

    public void OnNodeCompleted()
    {
        _finalSettlePending = true;
        _trace.Write("node complete - the last line settles when its commands finish; the session stays open");
    }

    public void Note(string message) => _trace.Write(message);

    // 호스트가 매 프레임 부른다.
    public void Tick()
    {
        if (!_finalSettlePending || _trace.HasStepWorkRunning())
            return;

        _finalSettlePending = false;
        _audit.CheckOverlap();
        _audit.Settle(_stage.Snapshot());
    }

    // 지금 줄 = 방금 CommitLineEntered가 쌓은 롤백 포인트. 같은 줄을 다시 지나면 등장 순번이 붙는다.
    private string CurrentLineKey()
    {
        IReadOnlyList<RollbackPoint> points = _rollbackHistory.Points;

        if (points.Count == 0)
            return "?";

        RollbackPoint point = points[points.Count - 1];
        string id = point.lineId != null && point.lineId.StartsWith("line:") ? point.lineId.Substring(5) : point.lineId;

        return point.occurrence > 1 ? $"{id}#{point.occurrence}" : id;
    }
}
