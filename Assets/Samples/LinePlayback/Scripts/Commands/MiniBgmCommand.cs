using System.Collections;
using UnityEngine;

// 런 수명 커맨드 — BGM. 한 줄에서 시작해 여러 줄에 걸쳐 흐르므로 스텝 경계에 묶이면 안 된다.
//
//   IRunScopedCommand        SequencePlayer가 스텝 대신 런 수명에 묶는다. 다음 줄의 CleanupStep(Finish)를 넘어 산다.
//   ExecuteEvenIfSkipping    압축(빨리 감기 · 롤백 시크) 중에도 실행한다. 지속되는 상태라 건너뛰면 재현이 깨진다.
//   RegisterRunLifetime      기본 구현은 루틴을 멈추기만 한다. 그러면 BGM 키와 볼륨이 무대에 남는다.
//                            그래서 런이 끝날 때(정지 · 롤백 · 다시 시작) 자기 흔적을 직접 거둔다.
//   stop_bgm                 다른 커맨드가 키를 비우면 그것을 보고 스스로 끝난다.
public sealed class MiniBgmCommand : CommandBase, IRunScopedCommand
{
    private readonly MiniStage _stage;
    private readonly string _key;
    private readonly float _fade;
    private readonly MiniTrace.Entry _trace;

    private CommandRunScope _scope;
    private bool _alive;

    public MiniBgmCommand(MiniStage stage, MiniBgmSpec spec, MiniTrace.Entry trace)
    {
        _stage = stage;
        _key = spec.key;
        _fade = spec.fade;
        _trace = trace;
    }

    protected override SkipPolicy SkipPolicy => SkipPolicy.ExecuteEvenIfSkipping;

    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        _scope = scope;
        _alive = true;

        float fade = scope.ShouldCompressCommandExecution ? 0f : scope.ScalePresentationDuration(_fade);
        _trace.Enter(fade);

        _stage.Write(_trace, scope, () =>
        {
            _stage.BgmKey = _key;
            _stage.BgmStopping = false;
        });

        float elapsed = 0f;

        while (_alive && !scope.IsCancelled)
        {
            if (_stage.BgmKey != _key)
            {
                _alive = false;
                _trace.Complete();
                yield break;
            }

            if (!_stage.BgmStopping)
            {
                elapsed += PresentationClock.DeltaTime;
                float volume = fade <= 0f ? 1f : Mathf.Clamp01(elapsed / fade);
                _stage.Write(_trace, scope, () => _stage.BgmVolume = volume);
            }

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
            if (_stage.BgmKey == _key)
                _stage.BgmKey = null;

            _stage.BgmVolume = 0f;
            _stage.BgmStopping = false;
        });

        const string why = "run boundary - its own cleanup stopped the music";

        if (cancelled)
            _trace.Cancel(why);
        else
            _trace.Finish(why);
    }
}
