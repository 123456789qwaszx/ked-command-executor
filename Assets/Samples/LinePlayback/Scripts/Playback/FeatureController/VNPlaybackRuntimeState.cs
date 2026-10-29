using UnityEngine;

public sealed class VNPlaybackRuntimeState
{
    public VNPlaybackSettings PlaybackSettings { get; set; } = new();
    
    private bool _isAutoMode;
    private bool _isSpeedUpMode;
    private bool _isRapidSkipMode;
    private float _timeScale = 1f;

    public bool IsAutoMode => _isAutoMode;
    public bool IsSpeedUpMode => _isSpeedUpMode;
    public bool IsRapidSkipMode => _isRapidSkipMode;

    public float TimeScale => _timeScale;
    public float AutoAdvanceDelay => PlaybackSettings != null
        ? PlaybackSettings.autoModeDelaySeconds
        : 1.5f;

    public void SetAutoModeEnabled(bool enabled)
    {
        _isAutoMode = enabled;
    }

    public void SetSpeedUpModeEnabled(bool enabled)
    {
        _isSpeedUpMode = enabled;
    }

    public void SetRapidSkipModeEnabled(bool enabled)
    {
        _isRapidSkipMode = enabled;
    }

    public void SetTimeScale(float timeScale)
    {
        _timeScale = Mathf.Max(0.0001f, timeScale);
    }

    public void ResetPlaybackModifiers()
    {
        _isAutoMode = false;
        _isSpeedUpMode = false;
        _isRapidSkipMode = false;
        _timeScale = 1f;
    }
}