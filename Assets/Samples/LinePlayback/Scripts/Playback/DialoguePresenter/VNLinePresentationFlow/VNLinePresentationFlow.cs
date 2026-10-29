using System;
using MiniYarn;

// ked-presentation-runtime의 VNLinePresentationFlow를 옮긴 것 — 이 샘플의 중심이다.
//
// 대사 한 줄을 명시적인 단계 열로 돌리는 트랜잭션:
//   LineReceived → LineEnteredCommitted   줄을 기록하고(백로그 · 롤백 좌표), 쌓여 있던 명령을 이 줄의 스텝으로 재생한다
//   LineRuntimeStateResolved              시크 판정 — 표적 전 줄은 보이지 않고 지나간다(SeekPassThrough)
//   VisualRunStarted → DialogueSurfaceResolved   대사창 전환(줄 실행 토큰이 무효가 되면 Stale로)
//   TypewriterReady → TypewriterCompleted        타자기(서두르기 = 이 줄 끝까지 가속)
//   DisplayCommitted → WaitingForAdvance → Completed
//
// Yarn은 명령과 줄을 순서대로 건넨다. 명령은 그 자리에서 실행되지 않고 YarnPlaybackDriver에 쌓였다가,
// 바로 여기 — 다음 줄의 LineEnteredCommitted에서 _playbackDriver.PlayCollected()로 한 번에 재생된다.
// 그래서 "줄 하나 = 스텝 하나"이고, 앞 줄의 배경 명령은 이 순간 CleanupStep(Finish)로 정리된다.
//
// 바뀐 곳: TMP_Text 대신 DialogueTextView, EllipsisBreathTypewriter 대신 DemoTypewriter.
public sealed class VNLinePresentationFlow
{
    public event Action LineDisplayed;

    private readonly VNYarnLineBoundary _vnYarnLineBoundary;
    private readonly VNLinePresentationState _advanceState;
    private readonly DialogueBoxPresentationController _boxPresentation;
    private readonly DemoTypewriter _typewriter;
    private readonly YarnPlaybackDriver _playbackDriver;
    private readonly LineHurrySpeedController _lineHurrySpeed;

    public VNLinePresentationPhase CurrentPhase { get; private set; } =
        VNLinePresentationPhase.None;

    public VNLinePresentationFlow(
        VNYarnLineBoundary vnYarnLineBoundary,
        VNLinePresentationState advanceState,
        DialogueBoxPresentationController boxPresentation,
        DemoTypewriter typewriter,
        YarnPlaybackDriver playbackDriver,
        LineHurrySpeedController lineHurrySpeed)
    {
        _vnYarnLineBoundary = vnYarnLineBoundary;
        _advanceState = advanceState;
        _boxPresentation = boxPresentation;
        _typewriter = typewriter;
        _playbackDriver = playbackDriver;
        _lineHurrySpeed = lineHurrySpeed;
    }

    public async YarnTask RunAsync(
        VNLinePresentationContext ctx,
        Func<LinePresentationRun> beginRun,
        Func<LineCancellationToken, YarnTask> waitForAdvance)
    {
        // Phase: LineReceived -> LineEnteredCommitted
        SetPhase(ctx, VNLinePresentationPhase.LineReceived);

        _advanceState.MarkLineEntered();
        ctx.Meta = _vnYarnLineBoundary.BuildLineMeta(ctx.Line, ctx.NodeName);
        _vnYarnLineBoundary.CommitLineEntered(ctx.Meta);

        _playbackDriver.PlayCollected();
        SetPhase(ctx, VNLinePresentationPhase.LineEnteredCommitted);

        // Phase: LineRuntimeStateResolved
        if (_advanceState.IsSeekingActive){
            ctx.SeekDecision = _advanceState.IsSeekTargetLine(ctx.Meta)
                ? VNSeekLineDecision.TargetLineReached
                : VNSeekLineDecision.SkipVisualAndDispatchSeekNext;
        }
        else ctx.SeekDecision = VNSeekLineDecision.NotSeeking;

        SetPhase(ctx, VNLinePresentationPhase.LineRuntimeStateResolved);

        if (ctx.ShouldSkipVisual) {
            RunSeekPassThrough(ctx);
            return;
        }

        // #auto — 다 보이면 입력 없이 넘긴다(VNLineAdvanceTag). 되감기로 닿은 줄은 다시 보려고 온 것이라 넘기지 않는다.
        // 시크 종류는 ClearSeek 전에 읽는다.
        bool rollbackTarget =
            ctx.IsTargetLineReached && _advanceState.SeekKind == VNSeekKind.Rollback;

        if (ctx.IsTargetLineReached)
            _advanceState.ClearSeek();

        _advanceState.SetLineAutoAdvance(
            !rollbackTarget && VNLineAdvanceTag.IsAuto(ctx.Line.Metadata));

        // Phase: VisualRunStarted
        ctx.Run = beginRun();
        SetPhase(ctx, VNLinePresentationPhase.VisualRunStarted);

        DialogueBoxPresentationContext boxCtx = new(
            ctx.Line,
            ctx.Run,
            ctx.ShouldUseImmediateTransition);

        // Phase: BoxTransitioning -> BoxReady
        await _boxPresentation.ShowLineAsync(boxCtx);

        SetPhase(ctx, VNLinePresentationPhase.DialogueSurfaceResolved);

        if (!ctx.Run.IsValid) {
            await CompleteStaleAfterBoxAsync(ctx, waitForAdvance);
            return;
        }

        // Phase: TypewriterReady
        ctx.LineText = _boxPresentation.LineTextTarget;
        _typewriter.SetTextView(ctx.LineText);

        ctx.Text = ctx.Line.TextWithoutCharacterName;
        _typewriter.PrepareForContent(ctx.Text);
        SetPhase(ctx, VNLinePresentationPhase.TypewriterReady);

        // Phase: TypewriterCompleted
        try
        {
            await _typewriter
                .RunTypewriter(
                    ctx.Text,
                    ctx.Token.HurryUpToken,
                    ctx.Token.NextContentToken,
                    _lineHurrySpeed.Enter)
                .SuppressCancellationThrow();
        }
        finally
        {
            _lineHurrySpeed.Exit();
        }

        SetPhase(ctx, VNLinePresentationPhase.TypewriterCompleted);

        if (!ctx.Run.IsValid) {
            await CompleteStaleAfterTypewriterAsync(ctx, waitForAdvance);
            return;
        }

        // Phase: DisplayCommitted
        _advanceState.MarkLineDisplayCompleted(ctx.Meta, "normal");
        _typewriter.ContentWillDismiss();
        SetPhase(ctx, VNLinePresentationPhase.DisplayCommitted);

        LineDisplayed?.Invoke();

        // Phase: WaitingForAdvance -> Completed
        SetPhase(ctx, VNLinePresentationPhase.WaitingForAdvance);
        await waitForAdvance(ctx.Token);

        SetPhase(ctx, VNLinePresentationPhase.Completed);
    }

    private void RunSeekPassThrough(VNLinePresentationContext ctx)
    {
        SetPhase(ctx, VNLinePresentationPhase.SeekPassThrough);

        _boxPresentation.CloseAll();
        _advanceState.MarkLineDisplayCompleted(ctx.Meta, "passThrough");
    }

    private async YarnTask CompleteStaleAfterBoxAsync(
        VNLinePresentationContext ctx,
        Func<LineCancellationToken, YarnTask> waitForAdvance)
    {
        SetPhase(ctx, VNLinePresentationPhase.Stale);

        _boxPresentation.CleanupStale();
        _advanceState.MarkLineTornDown(ctx.Meta, "StaleAfterBox");

        SetPhase(ctx, VNLinePresentationPhase.WaitingForAdvance);
        await waitForAdvance(ctx.Token);

        SetPhase(ctx, VNLinePresentationPhase.Completed);
    }

    private async YarnTask CompleteStaleAfterTypewriterAsync(
        VNLinePresentationContext ctx,
        Func<LineCancellationToken, YarnTask> waitForAdvance)
    {
        SetPhase(ctx, VNLinePresentationPhase.Stale);

        _typewriter.ContentWillDismiss();
        _advanceState.MarkLineTornDown(ctx.Meta, "StaleAfterTypewriter");

        SetPhase(ctx, VNLinePresentationPhase.WaitingForAdvance);
        await waitForAdvance(ctx.Token);

        SetPhase(ctx, VNLinePresentationPhase.Completed);
    }

    private void SetPhase(VNLinePresentationContext ctx, VNLinePresentationPhase phase)
    {
        ctx.Phase = phase;
        CurrentPhase = phase;
    }
}
