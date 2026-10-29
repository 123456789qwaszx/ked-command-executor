using System.Collections.Generic;
using System.Text;

// YarnPlaybackDriver 흉내 — 브리지가 쌓은 스펙을 줄에 들어갈 때 한 스텝으로 재생한다.
//
// 스텝 경계에서 하는 일의 순서가 이 샘플의 핵심이다:
//   1. 겹침 검사       앞 스텝의 메인 루틴이 아직 무언가를 붙들고 있나(대기 명령 · 미진입 명령)
//   2. CleanupStep(Finish)  앞 스텝의 배경 루틴을 최종 상태로 끝낸다
//   3. 정산(settle)     무대 사진 == 순수 모델? (같은 줄을 다시 보는 것이면) == 처음 본 사진?
//   4. PlaySpecs        새 스텝. 실행기도 안에서 CleanupStep(Finish)를 부르지만 2에서 이미 비었다.
public sealed class MiniPlaybackDriver
{
    private readonly CommandExecutor _executor;
    private readonly ICommandRunScopeProvider _scopes;
    private readonly MiniStage _stage;
    private readonly MiniTrace _trace;
    private readonly MiniLifecycleAudit _audit;

    private readonly List<CommandSpecBase> _collected = new();

    public MiniPlaybackDriver(
        CommandExecutor executor,
        ICommandRunScopeProvider scopes,
        MiniStage stage,
        MiniTrace trace,
        MiniLifecycleAudit audit)
    {
        _executor = executor;
        _scopes = scopes;
        _stage = stage;
        _trace = trace;
        _audit = audit;
    }

    public int PendingCount => _collected.Count;

    public void Enqueue(CommandSpecBase spec) => _collected.Add(spec);

    public void PlayCollected(MiniLineKey line)
    {
        CommandRunScope scope = _scopes.CurrentScope;

        SettleStep(scope);

        var specs = new List<CommandSpecBase>(_collected);
        _collected.Clear();

        _trace.BeginStep(line.Id, specs.Count);
        _stage.CurrentStep = _trace.CurrentStep;
        _audit.BeginStep(line, specs);

        _executor.PlaySpecs(specs, scope);
    }

    // 노드 끝 — 마지막 줄의 스텝도 경계를 맞는다.
    public void SettleFinal() => SettleStep(_scopes.CurrentScope);

    private void SettleStep(CommandRunScope scope)
    {
        _audit.CheckOverlap();
        scope.CleanupStep(CleanupPolicy.Finish);
        _audit.Settle(_stage.Snapshot());
    }

    // 쌓인 명령을 실행하지 않고 버린다. 버린 것이 있으면 개수와 요약을 돌려준다(로그용).
    public bool TryDiscardPending(out int count, out string summary)
    {
        count = _collected.Count;

        if (count == 0)
        {
            summary = string.Empty;
            return false;
        }

        var sb = new StringBuilder();

        foreach (CommandSpecBase spec in _collected)
        {
            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(spec);
        }

        summary = sb.ToString();
        _collected.Clear();
        return true;
    }
}
