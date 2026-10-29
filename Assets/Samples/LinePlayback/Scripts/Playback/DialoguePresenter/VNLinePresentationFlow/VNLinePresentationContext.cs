using MiniYarn;

public enum VNSeekLineDecision
{
    NotSeeking = 0,
    SkipVisualAndDispatchSeekNext,
    TargetLineReached,
}

// 원본과 같다. 바뀐 곳: TMP_Text → DialogueTextView, Yarn.Markup → MiniYarn.
public sealed class VNLinePresentationContext
{
    // Input
    public LocalizedLine Line { get; set; }
    public LineCancellationToken Token { get; set; }
    public string NodeName { get; set; }
    public bool ShouldUseImmediateTransition { get; set; }

    // Derived
    public YarnLineMeta Meta { get; set; }

    // Seek Decision
    public VNSeekLineDecision SeekDecision { get; set; }

    public bool IsTargetLineReached => SeekDecision == VNSeekLineDecision.TargetLineReached;
    public bool ShouldSkipVisual => SeekDecision == VNSeekLineDecision.SkipVisualAndDispatchSeekNext;

    // Visual Run
    public LinePresentationRun Run { get; set; }

    // Box Presentation
    public DialogueTextView LineText { get; set; }
    public MarkupParseResult Text { get; set; }

    // Phase Tracking
    public VNLinePresentationPhase Phase { get; set; } = VNLinePresentationPhase.None;
}
