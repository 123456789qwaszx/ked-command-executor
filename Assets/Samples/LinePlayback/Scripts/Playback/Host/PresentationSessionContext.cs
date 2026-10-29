using System;

/// <summary>
/// VN 재생 상태를 실행기의 정책(ICommandPacePolicy)으로 접는 어댑터.
/// 빨리 넘기기·시크는 "압축"으로, 속도 올리기는 "시간 배율"로 — 실행기는 그 둘만 본다.
/// </summary>
[Serializable]
public sealed class PresentationSessionContext : ICommandPacePolicy
{
    private readonly VNPlaybackRuntimeState _playback;
    private readonly ISeekStateQuery _seek;

    public PresentationSessionContext(VNPlaybackRuntimeState playback, ISeekStateQuery seek)
    {
        _playback = playback;
        _seek = seek;
    }

    public bool CompressExecution =>
        (_playback != null && _playback.IsRapidSkipMode) ||
        (_seek != null && _seek.IsSeekingActive);

    // 속도 모드가 아닐 때 재생 상태의 TimeScale은 1로 되돌아가 있지만(VNFeatureController.ApplySpeedUpModeState),
    // 규약을 여기서도 못박는다 — 실행기는 "속도 모드"라는 말을 모른다.
    public float TimeScale =>
        _playback != null && _playback.IsSpeedUpMode ? _playback.TimeScale : 1f;
}
