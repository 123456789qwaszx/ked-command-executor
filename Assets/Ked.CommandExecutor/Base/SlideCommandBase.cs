using DG.Tweening;
using Ked.Presentation.Core;
using UnityEngine;

/// <summary>
/// 슬라이드(등장·퇴장)의 공통 수명 — SlideIn/SlideOut의 Char/Bg 4종이 이것 하나다.
///
/// 네 커맨드의 본체는 원래 글자 단위로 같았고, 다른 건 다섯 가지뿐이었다:
///   대상 해석(리그 종류) · 방향/거리 · ease · punch · 튐이 앞에 오나 뒤에 오나.
///
/// 등장/퇴장의 차이는 "현재 위치가 도착점이냐 출발점이냐" 하나로 환원된다
/// (등장은 화면 밖에서 제자리로, 퇴장은 제자리에서 화면 밖으로).
/// </summary>
public abstract class SlideCommandBase : ClaimTweenCommandBase
{
    private RectTransform _rect;

    private Vector2 _startPos;
    private Vector2 _endPos;
    private Vector2 _slideDir;

    protected abstract Ease SlideEase { get; }
    protected abstract CharRigDirection SlideDirection { get; }
    protected abstract float SlideDistance { get; }

    /// <summary>기본 경로를 벗어나 튀는 양(px). 0이면 밋밋한 직선 슬라이드다.</summary>
    protected abstract float Punch { get; }

    /// <summary>등장이면 true(현재 위치 = 도착점), 퇴장이면 false(현재 위치 = 출발점).</summary>
    protected abstract bool CurrentPositionIsDestination { get; }

    /// <summary>진행률(ease 적용 후)에 대한 튐의 세기. 등장은 끝에서, 퇴장은 출발에서 튄다.</summary>
    protected abstract float Bump(float easedProgress);

    /// <summary>슬라이드할 rect. 리그 종류별 해석은 파생이 안다.</summary>
    protected abstract RectTransform ResolveSlideRect(CommandRunScope scope);

    protected override void ResolveTargets(CommandRunScope scope)
    {
        _rect = ResolveSlideRect(scope);
    }

    protected override void ClaimTarget(CommandRunScope scope)
    {
        _rect.DOKill(true);

        Vector2 current = _rect.anchoredPosition;
        Vector2 offset = DirectionToVector(SlideDirection) * SlideDistance;

        if (CurrentPositionIsDestination)
        {
            _endPos = current;
            _startPos = current + offset;
        }
        else
        {
            _startPos = current;
            _endPos = current + offset;
        }

        Vector2 travel = _endPos - _startPos;

        // 거리가 0이면 진행 방향을 잃는다 — 등장은 들어오는 쪽, 퇴장은 나가는 쪽이 답이다.
        _slideDir = travel.sqrMagnitude > 0f
            ? travel.normalized
            : (CurrentPositionIsDestination ? -DirectionToVector(SlideDirection) : DirectionToVector(SlideDirection));
    }

    protected override Tween CreateTween(float duration)
    {
        // 슬라이드는 출발점에서 시작한다. 퇴장은 출발점이 곧 현재 위치라 무해하다.
        _rect.anchoredPosition = _startPos;

        return DOTween
            .To(() => 0f, ApplyProgress, 1f, duration)
            .SetEase(Ease.Linear)
            .SetTarget(_rect);
    }

    /// <summary>스텝 경계 마무리는 튐을 빼고 도착점까지 직선으로만 붙인다.</summary>
    protected override Tween CreateAcceleratedTween(float duration)
        => _rect
            .DOAnchorPos(_endPos, duration)
            .SetEase(SlideEase)
            .SetTarget(_rect);

    protected override void OnCommitFinalState()
    {
        _rect.anchoredPosition = _endPos;
    }

    protected override float MeasureRemainingRatio()
        => RemainingRatio(
            Vector2.Distance(_startPos, _endPos),
            Vector2.Distance(_rect.anchoredPosition, _endPos));

    private void ApplyProgress(float progress)
    {
        float eased = DOVirtual.EasedValue(0f, 1f, progress, SlideEase);

        Vector2 basePos = Vector2.LerpUnclamped(_startPos, _endPos, eased);
        Vector2 offset = _slideDir * (Punch * Bump(eased));

        _rect.anchoredPosition = basePos + offset;
    }

    // ⚠ 방향 표와 튐 모양의 <b>주인은 코어 SlideMotion 하나</b>다 (2026-08-24). 저작 도구의
    //   프리뷰가 같은 값으로 궤적을 그리기 때문이다 — 여기 사본을 두면 둘이 갈리고, 그
    //   어긋남은 "프리뷰와 게임이 다르다"로만 드러난다. 열거형 → 낱말만 이쪽이 진다
    //   (코어는 CharRigDirection을 모른다). 사슬 전체는 SlideMotionParityTests가 잡는다.

    // public인 이유: 순수 함수이고, 등가성 하네스가 <b>파서 → 열거형 → 벡터</b> 사슬을
    // 코어와 대조해야 한다(SlideMotionParityTests). 상태를 안 들므로 열어도 잃을 것이 없다.
    public static Vector2 DirectionToVector(CharRigDirection direction)
    {
        Vec2 axis = SlideMotion.DirectionVector(CanonicalWord(direction));

        return new Vector2(axis.X, axis.Y);
    }

    /// <summary>열거형 → 파서가 읽는 정본 낱말. 별칭이 아니라 대표 낱말이다.</summary>
    private static string CanonicalWord(CharRigDirection direction) => direction switch
    {
        CharRigDirection.Right => "right",
        CharRigDirection.Up => "up",
        CharRigDirection.Down => "down",
        _ => "left",
    };

    /// <summary>도착 직전에 부풀었다 사그라드는 튐 — 등장용.</summary>
    public static float BumpTowardEnd(float e) => SlideMotion.PunchTowardEnd(e);

    /// <summary>출발 직후에 부풀었다 사그라드는 튐 — 퇴장용.</summary>
    public static float BumpFromStart(float e) => SlideMotion.PunchFromStart(e);
}