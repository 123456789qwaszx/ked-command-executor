using UnityEngine;

// VN 재생의 유일한 프레임 입력 구동자.
public sealed class VNAdvanceInputPoller : MonoBehaviour
{
    [SerializeField] private VNAdvanceInputBindings _bindings = new();

    private DialogueAdvanceDispatcher _dialogueAdvanceDispatcher;
    private VNFeatureController _featureController;
    private IPlaybackHost _playbackHost;

    private bool _rapidSkipHeld;
    private bool _speedUpHeld;

    public void Initialize(
        DialogueAdvanceDispatcher dialogueAdvanceDispatcher,
        VNFeatureController featureController,
        IPlaybackHost playbackHost)
    {
        _dialogueAdvanceDispatcher = dialogueAdvanceDispatcher;
        _featureController = featureController;
        _playbackHost = playbackHost;
    }

    private void Update()
    {
        PollAdvance();
        PollRapidSkip();
        PollSpeedUpMode();
        PollFeatureToggles();

        _featureController.Tick();
    }

    private void PollAdvance()
    {
        if (_bindings.IsAdvancePressed())
            _dialogueAdvanceDispatcher.DispatchAdvance();
    }

    private void PollRapidSkip()
    {
        bool held = _bindings.IsRapidSkipHeld();

        if (held && !_rapidSkipHeld)
            _featureController.BeginRapidSkip();

        if (!held && _rapidSkipHeld)
            _featureController.EndRapidSkip();

        _rapidSkipHeld = held;
    }

    private void PollSpeedUpMode()
    {
        bool held = _bindings.IsSpeedUpHeld();

        if (held && !_speedUpHeld)
            _featureController.BeginSpeedUpMode();

        if (!held && _speedUpHeld)
            _featureController.EndSpeedUpMode();

        _speedUpHeld = held;

        if (_bindings.IsSpeedUpTogglePressed())
            _featureController.ToggleSpeedUpMode();
    }

    private async void PollFeatureToggles()
    {
        if (_bindings.IsAutoTogglePressed())
            _featureController.ToggleAuto();

        if (!_bindings.IsRollbackPressed())
            return;

        // 다시 돌릴 쪽이 없으면 표적도 정하지 않는다 — 시크만 걸려 남는 상태를 만들지 않는다.
        if (!_playbackHost.IsRunning)
            return;

        if (!_featureController.RequestRollbackOneStep())
            return;

        await _playbackHost.RequestReplayAsync();
    }
}
