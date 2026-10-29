using System.Collections;
using UnityEngine;

// 런 수명 커맨드 — 캐릭터의 대기 루프. 끝이 없어서 스텝 경계에 묶이면 안 된다.
//
//   IRunScopedCommand        SequencePlayer가 스텝 대신 런 수명에 묶는다. 줄을 넘어 산다.
//   ExecuteEvenIfSkipping    압축(빨리 감기 · 롤백 시크) 중에도 실행한다. 지속되는 상태라 건너뛰면 재현이 깨진다.
//   RegisterRunLifetime      기본 구현은 루틴을 멈추기만 한다. 그러면 Ambient 깃발과 출렁임이 무대에 남는다.
//                            그래서 정리(Cancel · Finish) 때 자기 흔적을 직접 거둔다.
public sealed class MiniAmbientCommand : CommandBase, IRunScopedCommand
{
    private readonly MiniStage _stage;
    private readonly MiniStage.Character _target;
    private readonly MiniTrace.Entry _trace;

    private CommandRunScope _scope;
    private bool _alive;

    public MiniAmbientCommand(MiniStage stage, MiniStage.Character target, MiniTrace.Entry trace)
    {
        _stage = stage;
        _target = target;
        _trace = trace;
    }

    protected override SkipPolicy SkipPolicy => SkipPolicy.ExecuteEvenIfSkipping;

    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        _scope = scope;
        _alive = true;
        _trace.Enter(0f);

        _stage.Write(_trace, scope, () => _target.Ambient = true);

        float t = 0f;

        while (_alive && !scope.IsCancelled)
        {
            t += Time.deltaTime;
            float bob = Mathf.Sin(t * 3f);
            _stage.Write(_trace, scope, () => _target.Bob = bob);
            yield return null;
        }
    }

    public override void RegisterRunLifetime(CommandRunScope scope, MonoBehaviour host, IEnumerator routine)
    {
        scope.TrackRun(
            cancel: () =>
            {
                host.StopCoroutine(routine);
                EndLoop(cancelled: true);
            },
            finish: () =>
            {
                host.StopCoroutine(routine);
                EndLoop(cancelled: false);
            });
    }

    private void EndLoop(bool cancelled)
    {
        if (!_alive)
            return;

        _alive = false;
        _stage.Write(_trace, _scope, () =>
        {
            _target.Ambient = false;
            _target.Bob = 0f;
        });

        const string why = "run boundary - its own cleanup removed the loop";

        if (cancelled)
            _trace.Cancel(why);
        else
            _trace.Finish(why);
    }
}
