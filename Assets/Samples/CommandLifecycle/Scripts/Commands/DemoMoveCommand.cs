using System.Collections;
using UnityEngine;

// CommandBase 위에 바로 얹은 가장 단순한 커맨드 — 도형 하나를 x까지 옮긴다.
//
// 실행기가 이 커맨드에 들어오는 문은 셋이고, 셋 다 같은 최종 상태(Commit)로 모인다:
//   ExecuteInner           — 보통 실행. 시간을 들여 옮긴다.
//   OnSkip                 — 압축(SkipPolicy.CompleteImmediately). 기다리지 않고 도착한다.
//   OnStepLifetimeFinished — 배경으로 돌다가 스텝 경계 CleanupStep(Finish)를 만났다. 남은 길을 건너뛴다.
// Cancel에는 문이 없다. 실행기는 코루틴을 멈출 뿐이라 도형은 멈춘 자리에 남는다 —
// 되돌릴지 둘지는 커맨드가 정할 일이고, 이 샘플은 "둔다"를 고른다.
public sealed class DemoMoveCommand : CommandBase
{
    private readonly Transform _target;
    private readonly float _toX;
    private readonly float _duration;
    private readonly bool _wait;
    private readonly DemoTrace.Entry _trace;

    private bool _running;

    public DemoMoveCommand(Transform target, float toX, float duration, bool wait, DemoTrace.Entry trace)
    {
        _target = target;
        _toX = toX;
        _duration = duration;
        _wait = wait;
        _trace = trace;
    }

    public override bool WaitForCompletion => _wait;

    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        Vector3 from = _target.position;
        Vector3 to = Destination();
        float duration = scope.ScalePresentationDuration(_duration);

        _running = true;
        _trace.Enter(duration);

        // 멈추는 것은 실행기의 StopCoroutine이지만, 중첩 IEnumerator까지 멈추는지는 Unity 구현에 기대지 않는다.
        // 스텝 경계(_running=false)와 런 취소(scope.IsCancelled)를 루프가 직접 본다.
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (!_running || scope.IsCancelled)
                yield break;

            _target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        if (!_running || scope.IsCancelled)
            yield break;

        Commit();
        _trace.Complete();
    }

    protected override void OnSkip(CommandRunScope scope)
    {
        Commit();
        _trace.Skip();
    }

    // 자연 완료 뒤에도 다음 스텝 경계에서 한 번 더 불린다(스텝 수명 장부에 남아 있으므로).
    // 끝난 커맨드에는 할 일이 없다.
    protected override void OnStepLifetimeFinished(CommandRunScope scope)
    {
        if (!_running)
            return;

        Commit();
        _trace.FinishByStep();
    }

    private void Commit()
    {
        _running = false;
        _target.position = Destination();
    }

    private Vector3 Destination()
    {
        Vector3 p = _target.position;
        p.x = _toX;
        return p;
    }
}
