using System.Collections.Generic;
using MiniYarn;
using UnityEngine;

// ked-presentation-runtime의 YarnCommandBridge를 옮긴 것(이 샘플의 어휘만).
//
// 두 종류의 명령이 있다:
//   쌓이는 명령 — Forward → 무대 파서 → 스펙 → YarnPlaybackDriver.Enqueue. Yarn에게는 즉시 끝난 명령이라
//                 VM은 다음 내용으로 간다. 실제 실행은 다음 줄의 PlayCollected에서.
//   즉시 명령   — box_hide · box_show · box_close. 대사창은 실행기의 스텝이 아니라 줄 흐름의 것이라
//                 그 자리에서 실행되고, Yarn은 반환된 YarnTask가 끝날 때까지 기다린다.
public sealed class YarnCommandBridge
{
    private const int FramePauseAliasMaxFrame = MiniStageCommandParser.FramePauseAliasMaxFrame;

    private readonly YarnPlaybackDriver _playbackDriver;
    private readonly MiniStageCommandParser _parser;
    private readonly DialogueBoxPresentationController _dialogueBoxPresentation;

    // Forward가 쓰는 스크래치 — 커맨드 하나가 스펙 여럿을 낳을 수 있다.
    private readonly List<CommandSpecBase> _parsed = new();

    public YarnCommandBridge(
        DialogueRunner runner,
        YarnPlaybackDriver playbackDriver,
        MiniStageCommandParser parser,
        DialogueBoxPresentationController dialogueBoxPresentation)
    {
        _playbackDriver = playbackDriver;
        _parser = parser;
        _dialogueBoxPresentation = dialogueBoxPresentation;

        BindRunnerCommands(runner);
    }

    private void BindRunnerCommands(DialogueRunner runner)
    {
        RegisterCharFocusPlacementCommands(runner);
        BindControl(runner);
        BindCharRigSetup(runner);
        BindCharRigPresentation(runner);
        BindAudio(runner);
    }

    private void BindControl(DialogueRunner runner)
    {
        BindFramePauseAliases(runner);

        runner.AddCommandHandler<string>(
            "pause", EnqueueWaitSpec);

        runner.AddCommandHandler(
            "box_hide", HideDialogueBox);

        runner.AddCommandHandler(
            "box_show", ShowDialogueBox);

        runner.AddCommandHandler(
            "box_close", CloseDialogueBox);
    }

    // Animator-style frame wait aliases.
    // 24fps basis: <<24fr>> = 1 second.
    private void BindFramePauseAliases(DialogueRunner runner)
    {
        for (int i = 1; i <= FramePauseAliasMaxFrame; i++)
        {
            int frame = i;

            string alias = $"{frame}fr";

            runner.AddCommandHandler(alias, () => Forward(alias));
        }
    }

    private void BindCharRigSetup(DialogueRunner runner)
    {
        runner.AddCommandHandler<string, string>(
            "face", EnqueueSetPortraitFaceSpec);
    }

    private void RegisterCharFocusPlacementCommands(DialogueRunner runner)
    {
        runner.AddCommandHandler<string, string, string, string>(
            "place_left", EnqueueFocusToLeftSpec);

        runner.AddCommandHandler<string, string, string, string>(
            "place_center", EnqueueFocusToCenterSpec);

        runner.AddCommandHandler<string, string, string, string>(
            "place_right", EnqueueFocusToRightSpec);
    }

    private void BindCharRigPresentation(DialogueRunner runner)
    {
        runner.AddCommandHandler<string, string, string>(
            "fade_in", EnqueueFadeInSpec);

        runner.AddCommandHandler<string, string, string>(
            "fade_out", EnqueueFadeOutSpec);
    }

    private void BindAudio(DialogueRunner runner)
    {
        runner.AddCommandHandler<string, string>(
            "bgm", EnqueuePlayBgmSpec);

        runner.AddCommandHandler<string>(
            "stop_bgm", EnqueueStopBgmSpec);
    }

    // ───────────────────────── 즉시 명령 ─────────────────────────

    private YarnTask HideDialogueBox()
        => _dialogueBoxPresentation.HideCurrentAsync();

    private YarnTask ShowDialogueBox()
        => _dialogueBoxPresentation.ShowCurrentAsync();

    // 대사창을 닫고 현재 대사창 상태도 버리는 것.
    // 이후 box_show를 하더라도 켤 대상이 없기에 무시됨.
    // 따라서 다음 대사가 나오면 "현재 열려있는 대사창이 없음"이므로 새로 FadeIn을 함.
    private void CloseDialogueBox()
        => _dialogueBoxPresentation.CloseAll();

    // ───────────────────────── 쌓이는 명령 — 인자는 전부 문자열, 생략은 null ─────────────────────────

    private void EnqueueWaitSpec(string duration = null)
        => Forward("pause", duration);

    private void EnqueueSetPortraitFaceSpec(string slotKey = null, string emotionKey = null)
        => Forward("face", slotKey, emotionKey);

    private void EnqueueFocusToLeftSpec(string roleKey = null, string focus = null, string durationToken = null, string waitToken = null)
        => Forward("place_left", roleKey, focus, durationToken, waitToken);

    private void EnqueueFocusToCenterSpec(string roleKey = null, string focus = null, string durationToken = null, string waitToken = null)
        => Forward("place_center", roleKey, focus, durationToken, waitToken);

    private void EnqueueFocusToRightSpec(string roleKey = null, string focus = null, string durationToken = null, string waitToken = null)
        => Forward("place_right", roleKey, focus, durationToken, waitToken);

    private void EnqueueFadeInSpec(string roleKey = null, string durationToken = null, string waitToken = null)
        => Forward("fade_in", roleKey, durationToken, waitToken);

    private void EnqueueFadeOutSpec(string roleKey = null, string durationToken = null, string waitToken = null)
        => Forward("fade_out", roleKey, durationToken, waitToken);

    private void EnqueuePlayBgmSpec(string clipKey = null, string fadeDurationToken = null)
        => Forward("bgm", clipKey, fadeDurationToken);

    private void EnqueueStopBgmSpec(string fadeDurationToken = null)
        => Forward("stop_bgm", fadeDurationToken);

    /// <summary>
    /// Yarn이 나눠 준 토큰을 StageCommand로 묶어 무대 파서에 넘긴다. 뜻(인자 자리·생략값·단위)은
    /// 파서가 정한다 — 여기는 텍스트를 옮기기만 한다. 생략된 인자는 null로 가고, 파서가 생략값을 채운다.
    /// </summary>
    private void Forward(string commandName, params string[] args)
    {
        _parsed.Clear();

        if (!_parser.TryParse(new StageCommand(commandName, args), _parsed))
        {
            Debug.LogError($"[YarnCommandBridge] 무대 파서가 모르는 커맨드 '{commandName}' — 등록표와 파서 표가 어긋났다.");
            return;
        }

        for (int i = 0; i < _parsed.Count; i++)
            _playbackDriver.Enqueue(_parsed[i]);
    }
}
