using DG.Tweening;
using UnityEngine;

/// <summary>
/// CanvasGroup 페이드의 공통 수명 — FadeIn/FadeOut의 Char/Bg 4종이 이것 하나다.
/// 변형은 셋뿐이다: 대상 해석(리그 종류) · 목표 alpha(1/0) · 커밋 후 interactable.
/// </summary>
public abstract class CanvasFadeCommandBase : ClaimTweenCommandBase
{
    private CanvasGroup _canvasGroup;
    private float _startAlpha;

    protected abstract Ease FadeEase { get; }
    protected abstract float TargetAlpha { get; }
    protected abstract bool InteractableAfterCommit { get; }

    /// <summary>페이드할 CanvasGroup이 붙은 rect. 리그 종류별 해석은 파생이 안다.</summary>
    protected abstract RectTransform ResolveFadeRect(CommandRunScope scope);

    protected override void ResolveTargets(CommandRunScope scope)
    {
        RectTransform target = ResolveFadeRect(scope);

        _canvasGroup = GetOrAddCanvasGroup(target);
    }

    protected override void ClaimTarget(CommandRunScope scope)
    {
        _canvasGroup.DOKill(true);
        _startAlpha = _canvasGroup.alpha;
    }

    protected override Tween CreateTween(float duration)
        => _canvasGroup
            .DOFade(TargetAlpha, duration)
            .SetEase(FadeEase)
            .SetTarget(_canvasGroup);

    protected override void OnCommitFinalState()
    {
        _canvasGroup.alpha = TargetAlpha;
        _canvasGroup.interactable = InteractableAfterCommit;
        _canvasGroup.blocksRaycasts = InteractableAfterCommit;
    }

    protected override float MeasureRemainingRatio()
        => RemainingRatio(
            Mathf.Abs(TargetAlpha - _startAlpha),
            Mathf.Abs(TargetAlpha - _canvasGroup.alpha));

    private CanvasGroup GetOrAddCanvasGroup(RectTransform rect)
    {
        if (rect.TryGetComponent(out CanvasGroup group))
            return group;

        Debug.LogWarning($"[{GetType().Name}] CanvasGroup missing. Added automatically: {rect.name}", rect);
        return rect.gameObject.AddComponent<CanvasGroup>();
    }
}