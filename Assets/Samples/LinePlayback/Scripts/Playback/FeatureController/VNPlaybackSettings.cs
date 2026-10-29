using System;
using UnityEngine;

[Serializable]
public class VNPlaybackSettings
{
    [Header("SpeedUp Mode")]
    [Min(2f)] public float speedupModeMultiplier = 12f;
    [Min(2f)] public float speedupModeMinMultiplier = 2f;
    [Min(2f)] public float speedupModeMaxMultiplier = 32f;

    [Header("Rapid Skip")]
    [Tooltip("Alt RapidSkip용 입력 간격. 책갈피를 촤라락 넘기는 용도.")]
    public float rapidSkipAdvanceRateLimitSec = 0.04f;
    public float rapidSkipCooldownAfterHurryUpSec = 0.04f;
    public float rapidSkipCooldownAfterNextLineSec = 0.04f;

    [Header("Auto")]
    public float autoModeDelaySeconds = 1.5f;
    public float autoAdvanceRateLimitSec = 0.13f;

    [Header("Normal Advance")]
    public float userAdvanceCooldownSec = 0.13f;
    public float cooldownAfterHurryUpSec = 0.18f;
    public float cooldownAfterNextLineSec = 0.1f;
}