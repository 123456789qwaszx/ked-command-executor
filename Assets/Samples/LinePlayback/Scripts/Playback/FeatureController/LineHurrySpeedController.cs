// 원본과 같다. 바뀐 곳: DOTween.unscaledTimeScale → PresentationClock.UnscaledTimeScale, 타자기 타입.
// 서두르기는 타자기만이 아니라 그 순간 돌던 연출 명령까지 가속한다(원본은 DOTween 전체 배율을 올린다).
public sealed class LineHurrySpeedController
{
    private const float HurrySpeedMultiplier = 30f;

    private readonly DemoTypewriter _typewriter;

    private bool _active;
    private float _restoreTypewriterMultiplier = 1f;
    private float _restoreDotweenUnscaledTimeScale = 1f;

    public LineHurrySpeedController(DemoTypewriter typewriter)
    {
        _typewriter = typewriter;
    }

    public void Enter()
    {
        if (_active)
            return;

        _restoreTypewriterMultiplier = _typewriter.SpeedMultiplier;
        _restoreDotweenUnscaledTimeScale = PresentationClock.UnscaledTimeScale;

        _typewriter.SetSpeedMultiplier(
            _restoreTypewriterMultiplier * HurrySpeedMultiplier);

        PresentationClock.UnscaledTimeScale = HurrySpeedMultiplier;

        _active = true;
    }

    public void Exit()
    {
        if (!_active)
            return;

        _typewriter.SetSpeedMultiplier(_restoreTypewriterMultiplier);
        PresentationClock.UnscaledTimeScale = _restoreDotweenUnscaledTimeScale;

        _active = false;
    }
}