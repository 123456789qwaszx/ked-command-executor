// VNPlaybackRuntimeState 흉내 — 재생 쪽의 깃발들. 실행기는 이것을 모른다.
public sealed class MiniPlaybackModes
{
    public const float SpeedUpScale = 4f;

    public bool FastForwardHeld;     // Ctrl을 누르고 있다
    public bool FastForwardToggled;  // 버튼으로 켰다
    public bool SkipActive;          // 노드 끝까지 한 번에(EpisodeSkip)
    public bool SpeedUp;             // 시간 배율

    public bool IsFastForward => FastForwardHeld || FastForwardToggled;

    // 롤백 · 취소 · 다시 시작 때 내린다(VNFeatureController.DisablePlaybackModifiers).
    public void DisableAll()
    {
        FastForwardHeld = false;
        FastForwardToggled = false;
        SkipActive = false;
        SpeedUp = false;
    }
}

// PresentationSessionContext 흉내 — 재생 깃발을 실행기의 두 질문(ICommandPacePolicy)으로 접는다.
//   압축    = 빨리 감기 중이거나 시크 중
//   시간 배율 = 속도 모드면 4, 아니면 1
// 스킵은 여기 들지 않는다: 스킵은 압축이 아니라 "빨리 넘기기"라 명령은 보통대로 진입하고 스텝 경계가 Finish로 끝낸다.
public sealed class MiniPacePolicy : ICommandPacePolicy
{
    private readonly MiniPlaybackModes _modes;
    private readonly MiniLineState _line;

    public MiniPacePolicy(MiniPlaybackModes modes, MiniLineState line)
    {
        _modes = modes;
        _line = line;
    }

    public bool CompressExecution => _modes.IsFastForward || _line.IsSeeking;

    public float TimeScale => _modes.SpeedUp ? MiniPlaybackModes.SpeedUpScale : 1f;
}
