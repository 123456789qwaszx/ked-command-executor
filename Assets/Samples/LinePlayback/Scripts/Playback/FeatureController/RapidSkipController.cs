using UnityEngine;

public sealed class RapidSkipController
{
    // 첫 입력 후, 연속 반복이 시작되기 전까지의 초기 대기.
    // 이후의 연속 반복 속도는 AdvanceGate의 rapidSkipAdvanceRateLimitSec가 담당.
    private const float InitialRepeatDelaySeconds = 0.38f;

    private readonly DialogueAdvanceDispatcher _dispatcher;

    private bool _isHeld;

    private bool _hasFiredFirst;
    private double _repeatHoldoffUntilUnscaled;

    public RapidSkipController(DialogueAdvanceDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void SetHeld(bool held)
    {
        if (held == _isHeld)
            return;

        _isHeld = held;

        if (held)
            BeginHoldCycle();
    }

    public void Tick()
    {
        if (!_isHeld)
            return;

        if (!_hasFiredFirst)
        {
            _dispatcher.DispatchRapidSkipAdvance();

            _hasFiredFirst = true;
            _repeatHoldoffUntilUnscaled = Time.unscaledTimeAsDouble + InitialRepeatDelaySeconds;
            return;
        }

        if (Time.unscaledTimeAsDouble < _repeatHoldoffUntilUnscaled)
            return;

        _dispatcher.DispatchRapidSkipAdvance();
    }

    private void BeginHoldCycle()
    {
        _hasFiredFirst = false;
        _repeatHoldoffUntilUnscaled = double.NegativeInfinity;
    }
}