using System.Collections;
using UnityEngine;

// Cleanup at the STEP boundary (when the next step/line starts) - the shorter, default lifetime.
// CommandBase implements this, so every command is step-scoped by default: a background routine
// is cleaned at the next step unless it opts into a longer lifetime.
public interface IStepScopedCommand
{
    void RegisterStepLifetime(CommandRunScope scope, MonoBehaviour host, IEnumerator routine);
}

// Opt-in marker for background commands that must OUTLIVE a single step and live until the RUN
// ends (session/episode) - e.g., BGM, ambient/idle loops. Overrides the step-scoped default.
public interface IRunScopedCommand
{
    void RegisterRunLifetime(CommandRunScope scope, MonoBehaviour host, IEnumerator routine);
}

public abstract class CommandBase : ISequenceCommand, IStepScopedCommand
{
    protected virtual SkipPolicy SkipPolicy => SkipPolicy.CompleteImmediately;
    public virtual bool WaitForCompletion => false;
    
    public IEnumerator Execute(CommandRunScope scope)
    {
        if (scope.IsCancelled)
            yield break;
        
        if (scope.ShouldCompressCommandExecution)
        {
            switch (SkipPolicy)
            {
                case SkipPolicy.Ignore:
                    yield break;

                case SkipPolicy.CompleteImmediately:
                    OnSkip(scope);
                    yield break;

                case SkipPolicy.ExecuteEvenIfSkipping:
                    break;
            }
        }
        
        IEnumerator inner = ExecuteInner(scope);

        if (inner != null)
            yield return inner;
    }

    protected abstract IEnumerator ExecuteInner(CommandRunScope scope);

    protected virtual void OnSkip(CommandRunScope scope)
    {
    }
    
    // Default step-lifetime binding.
    // Background routines are stopped at the next step boundary.
    // OnStepLifetimeFinished is called only when the boundary cleanup uses CleanupPolicy.Finish.
    // It is not a natural completion callback.
    public virtual void RegisterStepLifetime(CommandRunScope scope, MonoBehaviour host, IEnumerator routine)
    {
        scope.TrackStep(
            cancel: () =>
            {
                if (routine != null)
                    host.StopCoroutine(routine);
            },
            finish: () =>
            {
                if (routine != null)
                    host.StopCoroutine(routine);
                
                OnStepLifetimeFinished(scope);
            });
    }
    
    // Opt-in (run) binding for IRunScopedCommand: same shape as step, but cleaned at run end
    // instead of the next step boundary. Use for work that must persist across steps.
    public virtual void RegisterRunLifetime(CommandRunScope scope, MonoBehaviour host, IEnumerator routine)
    {
        scope.TrackRun(
            cancel: () =>
            {
                if (routine != null)
                    host.StopCoroutine(routine);
            },
            finish: () =>
            {
                if (routine != null)
                    host.StopCoroutine(routine);

                OnRunLifetimeFinished(scope);
            });
    }
    
    // Completion hook tied to step cleanup (CleanupPolicy.Finish).
    // Called when the step finishes (normal end / finish-all), not on Cancel-only cleanup.
    protected virtual void OnStepLifetimeFinished(CommandRunScope scope) { }
    protected virtual void OnRunLifetimeFinished(CommandRunScope scope) { }
}