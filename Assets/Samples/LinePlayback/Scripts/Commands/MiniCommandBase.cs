using System;
using System.Collections;
using UnityEngine;

// 시간을 들여 무대를 바꾸는 커맨드의 공통 모양. CommandBase가 여는 세 문을 한 최종 상태(Commit)로 모은다:
//   ExecuteInner           — 보통 실행. Duration 동안 Apply(t)를 부르고 끝에 Commit.
//   OnSkip                 — 압축(빨리 감기 · 시크). 기다리지 않고 Commit.
//   OnStepLifetimeFinished — 배경으로 돌다가 스텝 경계 CleanupStep(Finish)를 만났다. 남은 길을 건너뛰고 Commit.
//
// 어느 문으로 들어오든 같은 Commit에 닿는다 — 그래서 감사(MiniStageModel)가 길과 상관없이 같은 답을 기대할 수 있다.
//
// Cancel에는 문이 없다. 실행기는 루틴을 멈출 뿐이라 무대는 멈춘 자리에 남는다.
// 루프는 매 프레임 scope.IsCancelled와 자기 상태를 직접 본다 — StopCoroutine이 중첩된 본문까지 멈추는지에 기대지 않는다.
public abstract class MiniCommandBase : CommandBase
{
    protected readonly MiniStage Stage;
    protected readonly MiniTrace.Entry Trace;

    private readonly bool _wait;
    private CommandRunScope _scope;
    private bool _running;

    protected MiniCommandBase(MiniStage stage, bool wait, MiniTrace.Entry trace)
    {
        Stage = stage;
        _wait = wait;
        Trace = trace;
    }

    public override bool WaitForCompletion => _wait;

    // 배율 전 길이(초). 0이면 즉시 Commit.
    protected abstract float Duration { get; }

    protected virtual void Begin() { }
    protected virtual void Apply(float t) { }
    protected abstract void Commit();

    protected override IEnumerator ExecuteInner(CommandRunScope scope)
    {
        _scope = scope;
        float duration = scope.ScalePresentationDuration(Duration);

        _running = true;
        Trace.Enter(duration);
        Begin();

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (!_running || scope.IsCancelled)
                yield break;

            Apply(elapsed / duration);
            yield return null;
        }

        if (!_running || scope.IsCancelled)
            yield break;

        Finish();
        Trace.Complete();
    }

    protected override void OnSkip(CommandRunScope scope)
    {
        _scope = scope;
        Finish();
        Trace.Skip();
    }

    // 자연 완료 뒤에도 다음 스텝 경계에서 한 번 더 불린다(스텝 수명 장부에 남아 있으므로). 끝난 커맨드는 할 일이 없다.
    protected override void OnStepLifetimeFinished(CommandRunScope scope)
    {
        if (!_running)
            return;

        Finish();
        Trace.Finish("step boundary - CleanupStep(Finish) jumped to the final state");
    }

    private void Finish()
    {
        _running = false;
        Commit();
    }

    // 무대 쓰기는 전부 여기로 — 무대가 쓴 쪽(스코프 · 스텝)을 보고 위반을 센다.
    protected void Write(Action apply) => Stage.Write(Trace, _scope, apply);
}
