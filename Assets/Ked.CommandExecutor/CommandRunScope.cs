using System;
using System.Threading;

public sealed class CommandRunScope
{
    private readonly ICommandPacePolicy _pace;

    public CancellationToken Token { get; set; }

    private LifetimeScope StepLifetime { get; } = new();
    private LifetimeScope RunLifetime { get; } = new();
    
    private bool _isCancelled;

    public CommandRunScope(ICommandPacePolicy pace)
    {
        _pace = pace;
        Token = CancellationToken.None;
    }

    public bool IsCancelled => _isCancelled;
    public void MarkCancelled() => _isCancelled = true;

    public float TimeScale => _pace.TimeScale;
    
    public bool CommandIsPlaying { get; set; }

    public bool ShouldCompressCommandExecution => _pace.CompressExecution;

    public float ScalePresentationDuration(float duration)
    {
        if (duration <= 0f)
            return duration;

        return duration / Math.Max(1f, TimeScale);
    }

    public void SetCommandIsPlaying(bool busy) => CommandIsPlaying = busy;
    
    public void ClearRuntimeState(CleanupPolicy policy = CleanupPolicy.Cancel)
    {
        CleanupStep(policy);
        CleanupRun(policy);

        Token = CancellationToken.None;
        SetCommandIsPlaying(false);
    }

    public void TrackStep(Action cancel, Action finish = null) => StepLifetime.Track(cancel, finish);
    public void TrackRun(Action cancel, Action finish = null) => RunLifetime.Track(cancel, finish);
    
    public void CleanupStep(CleanupPolicy policy) => StepLifetime.Cleanup(policy);
    public void CleanupRun(CleanupPolicy policy) => RunLifetime.Cleanup(policy);
}