using System.Collections;
using UnityEngine;

// DemoMoveCommand와 같은 모양의 두 번째 커맨드 — 대상이 위치가 아니라 alpha일 뿐이다.
// 실행기 입장에서는 둘이 구별되지 않는다: 같은 문(ExecuteInner · OnSkip · OnStepLifetimeFinished)으로 들어온다.
public sealed class DemoFadeCommand : CommandBase
{
    private readonly SpriteRenderer _target;
    private readonly float _toAlpha;
    private readonly float _duration;
    private readonly bool _wait;
    private readonly DemoTrace.Entry _trace;

    private bool _running;

    public DemoFadeCommand(SpriteRenderer target, float toAlpha, float duration, bool wait, DemoTrace.Entry trace)
    {
        _target = target;
        _toAlpha = toAlpha;
        _duration = duration;
        _wait = wait;
        _trace = trace;
    }

    public override bool WaitForCompletion => _wait;

    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        float from = _target.color.a;
        float duration = scope.ScalePresentationDuration(_duration);

        _running = true;
        _trace.Enter(duration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (!_running || scope.IsCancelled)
                yield break;

            SetAlpha(Mathf.Lerp(from, _toAlpha, elapsed / duration));
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
        SetAlpha(_toAlpha);
    }

    private void SetAlpha(float alpha)
    {
        Color c = _target.color;
        c.a = alpha;
        _target.color = c;
    }
}
