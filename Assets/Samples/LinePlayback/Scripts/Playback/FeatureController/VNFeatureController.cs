using System;
using System.Collections.Generic;
using UnityEngine;

// 원본과 같다. 바뀐 곳: 선택 기록(ChoiceHistory) 없음, 타자기 타입.
public sealed class VNFeatureController
{
    private readonly VNPlaybackRuntimeState _vnPlaybackSettings;

    private readonly DemoTypewriter _typewriter;
    private readonly VNLinePresentationState _linePresentationAdvanceState;

    private readonly BacklogRecorder _backlogRecorder;
    private readonly AutoAdvanceScheduler _autoAdvanceScheduler;
    private readonly RapidSkipController _rapidSkipController;
    private readonly RollbackHistory _rollbackController;
    private readonly EpisodeSkipController _episodeSkipController;

    private bool _speedUpToggled;
    private bool _speedUpHeld;
    private bool _rapidSkipHeld;

    private bool IsAuto => _vnPlaybackSettings.IsAutoMode;

    private bool LineFullyShown => _linePresentationAdvanceState.IsLineFullyShown;

    public IReadOnlyList<DialogueLogEntry> Backlogs => _backlogRecorder.Entries;

    public VNFeatureController(
        VNPlaybackRuntimeState vnPlaybackSettings,
        VNLinePresentationState linePresentationAdvanceState,
        DemoTypewriter ellipsisBreathTypewriter,
        BacklogRecorder backlogRecorder,
        AutoAdvanceScheduler autoAdvanceScheduler,
        RapidSkipController rapidSkipController,
        EpisodeSkipController episodeSkipController,
        RollbackHistory rollbackController)
    {
        _vnPlaybackSettings = vnPlaybackSettings;
        _linePresentationAdvanceState = linePresentationAdvanceState;
        _typewriter = ellipsisBreathTypewriter;

        _backlogRecorder = backlogRecorder;
        _autoAdvanceScheduler = autoAdvanceScheduler;
        _rapidSkipController = rapidSkipController;
        _episodeSkipController = episodeSkipController;

        _rollbackController = rollbackController;

        ApplySpeedUpModeState();
        ApplyRapidSkipState();
    }

    public void Tick()
    {
        // #auto 줄은 자동 진행보다 먼저 — 켜져 있지 않아도, 대기 없이 넘긴다. 막히면(연출 명령 · 시크) 다음 프레임에 다시.
        if (LineFullyShown && _linePresentationAdvanceState.IsLineAutoAdvancePending)
        {
            if (_autoAdvanceScheduler.TryDispatchLineAutoAdvance())
                _linePresentationAdvanceState.ConsumeLineAutoAdvance();
        }
        else if (IsAuto && LineFullyShown)
        {
            _autoAdvanceScheduler.Tick();
        }

        if (_vnPlaybackSettings.IsSpeedUpMode &&
            LineFullyShown)
        {
            _autoAdvanceScheduler.ResetAutoAdvanceTimer();
        }

        _rapidSkipController.Tick();
        _episodeSkipController.Tick();
    }

    public void ToggleAuto()
    {
        bool next = !IsAuto;

        _vnPlaybackSettings.SetAutoModeEnabled(next);

        if (next && LineFullyShown)
            _autoAdvanceScheduler.ResetAutoAdvanceTimer();
    }

    public void ToggleSpeedUpMode()
    {
        _speedUpToggled = !_speedUpToggled;
        ApplySpeedUpModeState();
    }

    public void BeginSpeedUpMode()
    {
        _speedUpHeld = true;
        ApplySpeedUpModeState();
    }

    public void EndSpeedUpMode()
    {
        _speedUpHeld = false;
        ApplySpeedUpModeState();
    }

    public void BeginRapidSkip()
    {
        _rapidSkipHeld = true;
        ApplyRapidSkipState();
    }

    public void EndRapidSkip()
    {
        _rapidSkipHeld = false;
        ApplyRapidSkipState();
    }

    public bool RequestRollbackOneStep()
    {
        if (_linePresentationAdvanceState.IsSeekingActive)
            return false;

        if (!_rollbackController.GetRollbackPoint(out RollbackPoint target))
            return false;

        return RequestRollbackTo(target);
    }

    // 백점프 — 백로그 항목의 라인으로 되돌아간다. 현재 장면 항목만.
    // 이전 장면 항목은 그 장면의 루트에서만 시작할 수 있는데, 그것은 되감기가 아니라 확정된
    // 장면을 다시 여는 일이라 여기 기전이 아니다(후속 — scene-future-plan §1).
    public bool CanJumpTo(in DialogueLogEntry entry) =>
        !_linePresentationAdvanceState.IsSeekingActive && TryResolveJumpTarget(entry, out _);

    public bool RequestBacklogJump(in DialogueLogEntry entry)
    {
        if (_linePresentationAdvanceState.IsSeekingActive)
            return false;

        if (!TryResolveJumpTarget(entry, out RollbackPoint target))
        {
            Debug.Log(
                _backlogRecorder.IsInCurrentScene(entry)
                    ? "[백로그] 지금 라인으로는 되돌아갈 것이 없다."
                    : "[백로그] 이전 장면의 라인 — 장면 루트 점프는 아직 없다.");
            return false;
        }

        return RequestRollbackTo(target);
    }

    // 항목 → 롤백 포인트. 순번 - 장면 시작 = historyIndex. 마지막 포인트(지금 라인)는 표적이 아니다.
    private bool TryResolveJumpTarget(in DialogueLogEntry entry, out RollbackPoint target)
    {
        target = default;

        int historyIndex = _backlogRecorder.GetCurrentSceneHistoryIndex(entry);

        if (historyIndex < 0 || historyIndex >= _rollbackController.LastHistoryIndex)
            return false;

        if (!_rollbackController.TryGetRollbackPoint(historyIndex, out target))
            return false;

        // 두 좌표계가 어긋났다면(있어서는 안 된다) 엉뚱한 라인으로 가느니 거부한다.
        return string.Equals(target.lineId, entry.lineId, StringComparison.Ordinal);
    }

    // 되감기의 본체. 표적을 정한 뒤의 순서는 한 걸음 롤백과 백점프가 같다.
    private bool RequestRollbackTo(in RollbackPoint target)
    {
        // 리플레이를 여는 쪽(장면 루프)이 표적 뒤의 진행 선택 기록을 정리하는 데 사용.
        _rollbackController.MarkRollbackTarget(target);

        // 백로그는 세션 연속 - 지우고 다시 쌓는 게 아니라 표적 뒤 꼬리만 걷는다.
        // 리플레이 패스스루는 백로그를 다시 적지 않으므로(VNYarnLineBoundary) 표적까지의 기록이 그대로 남는다.
        _backlogRecorder.TruncateFromEnd(_rollbackController.CountPointsAfter(target));

        _rollbackController.ClearRollbackPoints();
        _linePresentationAdvanceState.BeginRollbackSeek(target.nodeName, target.lineId, target.occurrence);

        DisablePlaybackModifiers();

        _autoAdvanceScheduler.ResetAutoAdvanceTimer();

        return true;
    }

    private void DisablePlaybackModifiers()
    {
        _vnPlaybackSettings.SetAutoModeEnabled(false);

        _speedUpToggled = false;
        _speedUpHeld = false;
        _rapidSkipHeld = false;

        _rapidSkipController.SetHeld(false);

        ApplySpeedUpModeState();
        ApplyRapidSkipState();

        _autoAdvanceScheduler.ResetAutoAdvanceTimer();
    }

    private void ApplySpeedUpModeState()
    {
        bool speedUp = _speedUpToggled || _speedUpHeld;

        float multiplier = 1f;

        if (speedUp)
        {
            multiplier = Mathf.Clamp(
                _vnPlaybackSettings.PlaybackSettings.speedupModeMultiplier,
                _vnPlaybackSettings.PlaybackSettings.speedupModeMinMultiplier,
                _vnPlaybackSettings.PlaybackSettings.speedupModeMaxMultiplier);
        }

        _vnPlaybackSettings.SetSpeedUpModeEnabled(speedUp);
        _vnPlaybackSettings.SetTimeScale(multiplier);

        _typewriter.SetSpeedMultiplier(multiplier);
    }

    private void ApplyRapidSkipState()
    {
        _vnPlaybackSettings.SetRapidSkipModeEnabled(_rapidSkipHeld);
        _rapidSkipController.SetHeld(_rapidSkipHeld);
    }
    
    public bool RequestSkipCurrentEpisode()
    {
        // Load / Rollback replay 중에는 별도 Skip을 열지 않는다.
        if (_linePresentationAdvanceState.IsSeekingActive)
            return false;

        if (!_episodeSkipController.Request())
            return false;

        DisablePlaybackModifiers();

        return true;
    }
}