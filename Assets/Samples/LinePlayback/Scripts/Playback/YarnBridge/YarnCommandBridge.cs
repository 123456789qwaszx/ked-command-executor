using System.Collections.Generic;
using UnityEngine;

// ked-presentation-runtime의 YarnCommandBridge 자리.
//
// 원본은 명령 이름마다 Yarn에 핸들러를 등록하고, 그 핸들러가 전부 Forward로 모인다.
// 이 샘플의 러너는 명령을 이름 + 문자열 인자로 바로 건네므로 등록 표 없이 Forward 하나로 받는다.
// 뜻(인자 자리 · 생략값 · 단위)은 무대 파서가 정한다 — 여기는 옮기기만 하고, 실행하지 않는다(쌓기만).
public sealed class YarnCommandBridge
{
    private readonly YarnPlaybackDriver _playbackDriver;
    private readonly MiniStageCommandParser _parser;

    // Forward가 쓰는 스크래치 — 커맨드 하나가 스펙 여럿을 낳을 수 있다.
    private readonly List<CommandSpecBase> _parsed = new();

    public YarnCommandBridge(
        MiniYarn.DialogueRunner runner,
        YarnPlaybackDriver playbackDriver,
        MiniStageCommandParser parser)
    {
        _playbackDriver = playbackDriver;
        _parser = parser;

        runner.SetCommandHandler(Forward);
    }

    private void Forward(string commandName, string[] args)
    {
        _parsed.Clear();

        if (!_parser.TryParse(new StageCommand(commandName, args), _parsed))
        {
            Debug.LogError($"[YarnCommandBridge] 무대 파서가 모르는 커맨드 '{commandName}'.");
            return;
        }

        for (int i = 0; i < _parsed.Count; i++)
            _playbackDriver.Enqueue(_parsed[i]);
    }
}
