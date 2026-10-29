using System;

// 원본과 같은 일을 한다. 바뀐 곳: Yarn ActionMarkupHandler(MonoBehaviour) 대신 타자기 알림(ITypewriterHandler).
// 줄이 다 보이면 자동 진행 타이머를 건다.
public sealed class AutoAdvanceScheduler : ITypewriterHandler
{
    private VNPlaybackRuntimeState _playbackSettings;
    private DialogueAdvanceDispatcher _dialogueAdvanceDispatcher;
    private Func<double> _getNow;

    private double _nextAutoAdvanceAt = double.PositiveInfinity;

    public void Initialize(
        VNPlaybackRuntimeState vnPlaybackSettings,
        DialogueAdvanceDispatcher dialogueAdvanceDispatcher,
        Func<double> getNow)
    {
        _playbackSettings = vnPlaybackSettings;
        _dialogueAdvanceDispatcher = dialogueAdvanceDispatcher;
        _getNow = getNow;
        Disarm();
    }

    public void OnLineDisplayBegin()
    {
        Disarm();
    }

    public void OnLineDisplayComplete()
    {
        ArmAutoAdvance();
    }

    public void OnLineWillDismiss() { }

    public void Tick()
    {
        if (_dialogueAdvanceDispatcher == null || _getNow == null)
            return;

        if (_getNow() < _nextAutoAdvanceAt)
            return;

        // dispatch 실행 전 예약.
        // 하지만 dispatch가 no-op이면(표시 중인 라인 뷰가 없어 hurry-up이 무시되는 경우)
        // 사전에 예약된 것을 활용 해 Auto 복구.
        ArmAutoAdvance();
        _dialogueAdvanceDispatcher.DispatchAutoAdvance();
    }

    public void ResetAutoAdvanceTimer()
    {
        ArmAutoAdvance();
    }

    // #auto 줄 — 자동 진행의 켜짐 · 대기와 무관하게 바로 넘긴다. 막혔으면 false(Tick이 다음 프레임에 다시).
    public bool TryDispatchLineAutoAdvance()
    {
        return _dialogueAdvanceDispatcher != null &&
               _dialogueAdvanceDispatcher.TryDispatchLineAutoAdvance();
    }

    private void ArmAutoAdvance()
    {
        if (_playbackSettings == null || _getNow == null)
        {
            Disarm();
            return;
        }

        _nextAutoAdvanceAt = _getNow() + _playbackSettings.AutoAdvanceDelay;
    }

    private void Disarm()
    {
        _nextAutoAdvanceAt = double.PositiveInfinity;
    }
}
