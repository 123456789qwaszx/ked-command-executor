using UnityEngine;

// DOTween.unscaledTimeScale 자리. 원본의 연출 트윈은 unscaled 시간으로 돌고, 줄을 서두르면
// LineHurrySpeedController가 이 배율을 30으로 올려 — 타자기만이 아니라 그 순간 돌던 명령까지 — 가속한다.
// 이 샘플의 커맨드와 대사창 페이드는 이 시계로 흐른다.
public static class PresentationClock
{
    public static float UnscaledTimeScale { get; set; } = 1f;

    public static float DeltaTime => Time.unscaledDeltaTime * UnscaledTimeScale;
}
