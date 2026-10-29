using System;
using System.Threading;

public sealed class LinePresentationRun
{
    private readonly int _generation;
    private readonly Func<int> _getCurrentGeneration;
    private readonly CancellationToken _visualToken;

    public CancellationToken VisualToken => _visualToken;

    public bool IsValid =>
        _generation == _getCurrentGeneration() &&
        !_visualToken.IsCancellationRequested;

    public LinePresentationRun(
        int generation,
        Func<int> getCurrentGeneration,
        CancellationToken visualToken)
    {
        _generation = generation;
        _getCurrentGeneration = getCurrentGeneration;
        _visualToken = visualToken;
    }
}