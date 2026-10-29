using System.Threading;
using MiniYarn;

// ked-presentation-runtime의 DialogueBoxPresentationController(+ .Command 부분)를 옮긴 것.
// 바뀐 곳: 화자 정책 DB · 표면 레이아웃 프리셋 없음(박스 종류는 태그로만 정한다), 뷰는 하나.
//
// 줄에 묶인 전환(ShowLineAsync)은 줄 실행(LinePresentationRun)이 유효할 때만 상태를 커밋한다 —
// 롤백 · 취소로 줄이 무효가 되면 페이드는 그 자리에서 손을 떼고, 박스 상태는 앞 줄의 것으로 남는다.
// box_hide / box_show는 줄과 무관한 즉시 명령이라 컨트롤러가 가진 가시성 세대로 보호한다.
public sealed class DialogueBoxPresentationController
{
    private const DialogueBoxKind DefaultLineBoxKind = DialogueBoxKind.Surface;

    private const float FadeUpDuration = 0.25f;
    private const float FadeDownDuration = 0.1f;

    private readonly IPresentationDialogueBoxView _box;
    private readonly DialogueBoxCurrentState _boxState;

    private int _visibilityTransitionEpoch;
    private CancellationTokenSource _visibilityTransitionCts;

    public DialogueBoxPresentationController(
        DialogueBoxCurrentState dialogueBoxState,
        IPresentationDialogueBoxView box)
    {
        _boxState = dialogueBoxState;
        _box = box;
    }

    // 타이프라이터가 글자를 채워 넣을 대상.
    // ShowLineAsync가 레이아웃을 얹은 뒤에 읽어야 함.
    public DialogueTextView LineTextTarget => _box.GetLineText();

    public async YarnTask ShowLineAsync(
        DialogueBoxPresentationContext ctx)
    {
        InvalidateVisibilityTransition();

        DialogueBoxKind nextBoxKind = ResolveNextBoxKind(ctx);
        DialogueBoxTransitionKind transitionKind = ResolveTransitionKind(ctx, _boxState.BoxKind, nextBoxKind);

        await ApplyTransitionAsync(
            transitionKind,
            nextBoxKind,
            ctx.CharacterName,
            ctx);

        // Only the still-valid run is allowed to commit the current box state.
        if (ctx.Run.IsValid)
            _boxState.Commit(nextBoxKind, _box, transitionKind);
    }

    private static DialogueBoxKind ResolveNextBoxKind(DialogueBoxPresentationContext ctx)
    {
        if (DialogueBoxTagResolver.TryResolveBoxKind(
                ctx.Metadata,
                out DialogueBoxKind metadataBoxKind))
            return metadataBoxKind;

        return DefaultLineBoxKind;
    }

    private DialogueBoxTransitionKind ResolveTransitionKind(
        DialogueBoxPresentationContext ctx,
        DialogueBoxKind? currentBoxKind,
        DialogueBoxKind nextBoxKind)
    {
        if (ctx.UseImmediateTransition)
            return DialogueBoxTransitionKind.Cut;

        if (DialogueBoxTagResolver.TryResolveTransitionKind(ctx.Metadata,
                out DialogueBoxTransitionKind metadataTransition))
            return metadataTransition;

        if (!_boxState.IsVisible || currentBoxKind.HasValue == false)
            return DialogueBoxTransitionKind.FadeIn;

        if (currentBoxKind.Value == nextBoxKind)
            return DialogueBoxTransitionKind.Keep;

        return DialogueBoxTransitionKind.FadeOutIn;
    }

    private void ApplyContent(string displayCharacterName, DialogueBoxPresentationContext ctx)
    {
        _box.PrimeText(
            ctx.Text,
            displayCharacterName,
            ctx.HasCharacterName);
    }

    private async YarnTask ApplyTransitionAsync(
        DialogueBoxTransitionKind transitionKind,
        DialogueBoxKind nextBoxKind,
        string displayCharacterName,
        DialogueBoxPresentationContext ctx)
    {
        bool immediate = ctx.UseImmediateTransition;
        LinePresentationRun run = ctx.Run;

        switch (transitionKind)
        {
            // 같은 종류 - 레이아웃이 그대로라 보이는 채로 교체.
            case DialogueBoxTransitionKind.Keep:
            case DialogueBoxTransitionKind.Cut:
                ApplyContent(displayCharacterName, ctx);

                if (immediate || run.IsValid)
                    _box.SetVisibleImmediate(true);

                break;

            // 지금 안 보이는 상태다 - 교체가 화면에 드러나지 않음.
            case DialogueBoxTransitionKind.FadeIn:
                ApplyContent(displayCharacterName, ctx);

                if (immediate)
                    _box.SetVisibleImmediate(true);
                else
                {
                    _box.PrepareHidden();
                    await _box.FadeInAsync(FadeUpDuration, run);
                }

                break;

            // 보이는 중에 종류가 바뀐다 - 교체를 페이드 뒤로 미뤄야 함.
            case DialogueBoxTransitionKind.FadeOutIn:
                if (immediate)
                {
                    ApplyContent(displayCharacterName, ctx);
                    _box.SetVisibleImmediate(true);
                    break;
                }

                await _box.FadeOutAsync(FadeDownDuration, run);

                if (!run.IsValid)
                    break;

                _box.SetVisibleImmediate(false);

                ApplyContent(displayCharacterName, ctx);

                _box.PrepareHidden();
                await _box.FadeInAsync(FadeUpDuration, run);

                break;

            // 감추는 것이 목적이지만 내용은 얹는다 — 타이프라이터가 이 박스의 글자 칸에 계속 쓰기 때문이다.
            case DialogueBoxTransitionKind.Hide:
                if (immediate || !_boxState.IsVisible)
                {
                    ApplyContent(displayCharacterName, ctx);
                    _box.SetVisibleImmediate(false);
                    break;
                }

                await _box.FadeOutAsync(FadeDownDuration, run);

                if (!run.IsValid)
                    break;

                _box.SetVisibleImmediate(false);
                ApplyContent(displayCharacterName, ctx);

                break;
        }
    }

    public void CloseAll()
    {
        InvalidateVisibilityTransition();

        _box.SetVisibleImmediate(false);
        _boxState.Reset();
    }

    // 뷰가 하나이므로 "중단된 전환이 건드린 박스"와 "현재 박스"가 같은 객체
    public void CleanupStale()
    {
        InvalidateVisibilityTransition();

        bool committedVisible = _boxState.IsVisible && _boxState.Box != null;

        _box.SetVisibleImmediate(committedVisible);
    }

    // ───────────────────────── box_hide / box_show (즉시 명령) ─────────────────────────

    private readonly struct VisibilityTransitionRun
    {
        public readonly int Epoch;
        public readonly CancellationToken Token;

        public VisibilityTransitionRun(int epoch, CancellationToken token)
        {
            Epoch = epoch;
            Token = token;
        }
    }

    public async YarnTask HideCurrentAsync()
    {
        VisibilityTransitionRun visibilityRun = BeginVisibilityTransition();

        if (_boxState.Box == null || !_boxState.IsVisible)
        {
            _box.SetVisibleImmediate(false);
            _boxState.MarkHidden();
            return;
        }

        await _box.FadeOutAsync(
            FadeDownDuration,
            visibilityRun.Token);

        if (!IsCurrentVisibilityTransition(visibilityRun))
            return;

        _box.SetVisibleImmediate(false);
        _boxState.MarkHidden();
    }

    public async YarnTask ShowCurrentAsync()
    {
        VisibilityTransitionRun visibilityRun = BeginVisibilityTransition();

        if (_boxState.Box == null)
            return;

        if (_boxState.IsVisible)
        {
            _box.SetVisibleImmediate(true);
            _boxState.TryMarkVisible();
            return;
        }

        _box.PrepareHidden();

        await _box.FadeInAsync(
            FadeUpDuration,
            visibilityRun.Token);

        if (!IsCurrentVisibilityTransition(visibilityRun))
            return;

        _box.SetVisibleImmediate(true);
        _boxState.TryMarkVisible();
    }

    private VisibilityTransitionRun BeginVisibilityTransition()
    {
        InvalidateVisibilityTransition();

        _visibilityTransitionCts = new CancellationTokenSource();

        return new VisibilityTransitionRun(
            _visibilityTransitionEpoch,
            _visibilityTransitionCts.Token);
    }

    private void InvalidateVisibilityTransition()
    {
        _visibilityTransitionEpoch++;

        if (_visibilityTransitionCts == null)
            return;

        _visibilityTransitionCts.Cancel();
        _visibilityTransitionCts = null;
    }

    private bool IsCurrentVisibilityTransition(VisibilityTransitionRun visibilityRun)
    {
        return _visibilityTransitionEpoch == visibilityRun.Epoch &&
               !visibilityRun.Token.IsCancellationRequested;
    }
}
