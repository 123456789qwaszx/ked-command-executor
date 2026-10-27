using UnityEngine;

/// <summary>
/// Unity 입력 소스 (프레임 기반 펄스 버퍼)
/// - 외부에서 Pulse로만 주입받는다 (Router/Gate를 통해서만 주입).
/// - Pulse와 Consume이 같은 프레임에 일어나지 않도록 보장.
/// - 프레임당 최대 1회 소비 보장.
/// </summary>
public sealed class UnityInputSource : IInputSource
{
    private int _pulseFrame = -1;
    private int _consumeFrame = -1;

    private readonly bool _enableDebugLog;

    public UnityInputSource(bool enableDebugLog = false)
    {
        _enableDebugLog = enableDebugLog;
    }

    public bool ConsumeAdvancePressed()
    {
        int currentFrame = Time.frameCount;

        // 1) 같은 프레임에 pulse가 들어왔으면 다음 프레임까지 대기
        if (_pulseFrame == currentFrame)
        {
            if (_enableDebugLog)
                Debug.Log($"[UnityInputSource] Consume blocked: pulse same frame (frame={currentFrame})");
            return false;
        }

        // 2) 이 프레임에 이미 Consume 했으면 중복 방지
        if (_consumeFrame == currentFrame)
        {
            if (_enableDebugLog)
                Debug.Log($"[UnityInputSource] Consume blocked: already consumed this frame (frame={currentFrame})");
            return false;
        }

        // 3) pulse가 존재하면 소비
        bool hasPulse = _pulseFrame >= 0;
        if (!hasPulse)
            return false;

        _pulseFrame = -1;
        _consumeFrame = currentFrame;

        if (_enableDebugLog)
            Debug.Log($"[UnityInputSource] ConsumeAdvancePressed (source=pulse, frame={currentFrame})");

        return true;
    }

    public void Reset()
    {
        _pulseFrame = -1;
        _consumeFrame = -1;

        if (_enableDebugLog)
            Debug.Log("[UnityInputSource] Reset");
    }

    public bool HasPendingPulse()
    {
        return _pulseFrame >= 0 && _pulseFrame < Time.frameCount;
    }
}