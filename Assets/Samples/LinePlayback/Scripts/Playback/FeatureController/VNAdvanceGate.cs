using UnityEngine;

public enum AdvanceRequestKind
{
    User,
    Auto,
    RapidSkip,
}

// 원본과 같다. 바뀐 곳: [샘플] 거절 알림(Rejected) — 원본은 같은 자리에 로그를 주석으로 두었다.
public sealed class AdvanceGate
{
    // [샘플] 화면의 EVENTS가 받는다.
    public event System.Action<AdvanceRequestKind, string> Rejected;

    private const int AdvanceRequestKindCount = 3;

    private readonly VNPlaybackRuntimeState _vnPlaybackSettings;
    private readonly ISeekStateQuery _lineState;
    private readonly ICommandRunScopeProvider _scopeProvider;

    // null이면 세션 미시작 — 방어가 아니라 도메인 상태다.
    private CommandRunScope CurrentScope => _scopeProvider.CurrentScope;

    private readonly double[] _lastAcceptedAt = new double[AdvanceRequestKindCount];
    private double _cooldownUntilUnscaled = double.NegativeInfinity;

    public AdvanceGate(
        VNPlaybackRuntimeState vnPlaybackSettings,
        ISeekStateQuery lineState,
        ICommandRunScopeProvider scopeProvider)
    {
        _vnPlaybackSettings = vnPlaybackSettings;
        _lineState = lineState;
        _scopeProvider = scopeProvider;

        for (int i = 0; i < _lastAcceptedAt.Length; i++)
            _lastAcceptedAt[i] = double.NegativeInfinity;
    }

    public void AddCooldownSeconds(float seconds)
    {
        if (seconds <= 0f)
            return;

        double until = Time.unscaledTimeAsDouble + seconds;
        if (until > _cooldownUntilUnscaled)
            _cooldownUntilUnscaled = until;
    }

    public bool TryAccept(AdvanceRequestKind kind)
    {
        if (_lineState.IsSeekingActive)
            return Reject(kind, "seek_active");

        if (kind != AdvanceRequestKind.RapidSkip)
            if (CurrentScope != null && CurrentScope.CommandIsPlaying)
                return Reject(kind, "cps_node_busy");
        
        double unscaledNow = Time.unscaledTimeAsDouble;

        if (unscaledNow < _cooldownUntilUnscaled)
        {
            return Reject(
                kind,
                "cooldown",
                $"now={unscaledNow:0.000}, until={_cooldownUntilUnscaled:0.000}");
        }

        if (!PassRateLimit(kind, unscaledNow))
            return Reject(kind, "rate_limit", $"now={unscaledNow:0.000}");
        
        return true;
    }

    private bool PassRateLimit(AdvanceRequestKind kind, double currentTime)
    {
        int index = (int)kind;
        float cooldown = GetRateLimitSeconds(kind);

        if (currentTime - _lastAcceptedAt[index] < cooldown)
            return false;

        _lastAcceptedAt[index] = currentTime;
        return true;
    }

    private float GetRateLimitSeconds(AdvanceRequestKind kind)
    {
        switch (kind)
        {
            case AdvanceRequestKind.Auto:
                return _vnPlaybackSettings.PlaybackSettings.autoAdvanceRateLimitSec;

            case AdvanceRequestKind.RapidSkip:
                return _vnPlaybackSettings.PlaybackSettings.rapidSkipAdvanceRateLimitSec;

            case AdvanceRequestKind.User:
            default:
                return _vnPlaybackSettings.PlaybackSettings.userAdvanceCooldownSec;
        }
    }

    public float GetCooldownAfterHurryUp(AdvanceRequestKind kind)
    {
        switch (kind)
        {
            case AdvanceRequestKind.RapidSkip:
                return _vnPlaybackSettings.PlaybackSettings.rapidSkipCooldownAfterHurryUpSec;

            case AdvanceRequestKind.Auto:
            case AdvanceRequestKind.User:
            default:
                return _vnPlaybackSettings.PlaybackSettings.cooldownAfterHurryUpSec;
        }
    }

    public float GetCooldownAfterNextLine(AdvanceRequestKind kind)
    {
        switch (kind)
        {
            case AdvanceRequestKind.RapidSkip:
                return _vnPlaybackSettings.PlaybackSettings.rapidSkipCooldownAfterNextLineSec;

            case AdvanceRequestKind.Auto:
            case AdvanceRequestKind.User:
            default:
                return _vnPlaybackSettings.PlaybackSettings.cooldownAfterNextLineSec;
        }
    }

    private bool Reject(AdvanceRequestKind kind, string reason, string note = null)
    {
        Rejected?.Invoke(kind, reason);

        // string detail = string.IsNullOrWhiteSpace(note) 
        //     ? $"kind={kind}, reason={reason}" 
        //     : $"kind={kind}, reason={reason}, {note}";
        //

        return false;
    }
}