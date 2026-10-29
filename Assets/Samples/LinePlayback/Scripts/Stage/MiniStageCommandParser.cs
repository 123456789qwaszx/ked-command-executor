using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// StageCommandParser 흉내 — StageCommand(이름 + 문자열 인자) → CommandSpec. 대본 문법의 뜻을 정하는 한 곳이다.
// 브리지는 이름과 인자 수만 안다. 인자 자리 · 생략값 · 단위는 이 표가 갖는다.
//
// 시간 토큰: "14fr"(24fps 프레임) · "2s"(초) · "0.5"(초).
public sealed class MiniStageCommandParser
{
    public const int FramePauseAliasMaxFrame = 48;
    private const float FramesPerSecond = 24f;

    private delegate void Handler(MiniStageCommandParser parser, StageCommand c);

    private static readonly Dictionary<string, Handler> Table = BuildTable();

    private List<CommandSpecBase> _output;

    private static Dictionary<string, Handler> BuildTable()
    {
        var table = new Dictionary<string, Handler>(StringComparer.Ordinal)
        {
            ["pause"] = (p, c) => p.Emit(new MiniPauseSpec { seconds = Seconds(c, 0, "0.18s"), wait = true }),
            ["face"] = (p, c) => p.Emit(new MiniFaceSpec { slot = c.Arg(0), face = c.Arg(1, MiniStage.DefaultFace) }),
            ["place_left"] = (p, c) => p.EmitPlace(c, "left"),
            ["place_center"] = (p, c) => p.EmitPlace(c, "center"),
            ["place_right"] = (p, c) => p.EmitPlace(c, "right"),
            ["fade_in"] = (p, c) => p.Emit(new MiniFadeSpec { slot = c.Arg(0), alpha = 1f, duration = Seconds(c, 1, "14fr"), wait = c.HasWaitToken() }),
            ["fade_out"] = (p, c) => p.Emit(new MiniFadeSpec { slot = c.Arg(0), alpha = 0f, duration = Seconds(c, 1, "14fr"), wait = c.HasWaitToken() }),
            ["bgm"] = (p, c) => p.Emit(new MiniBgmSpec { key = c.Arg(0, "untitled"), fade = Seconds(c, 1, "1s") }),
            ["stop_bgm"] = (p, c) => p.Emit(new MiniStopBgmSpec { fade = Seconds(c, 0, "1s") }),
        };

        // Animator 식 프레임 대기 별칭 — 24fps 기준 <<24fr>> = 1초, <<48fr>>까지.
        for (int frame = 1; frame <= FramePauseAliasMaxFrame; frame++)
        {
            float seconds = frame / FramesPerSecond;
            table[$"{frame}fr"] = (p, c) => p.Emit(new MiniPauseSpec { seconds = seconds, wait = true });
        }

        return table;
    }

    // 커맨드 하나를 스펙(0개 이상)으로 풀어 output 뒤에 붙인다. 모르는 이름이면 false — 호출부가 소리 낸다.
    public bool TryParse(StageCommand command, List<CommandSpecBase> output)
    {
        if (!Table.TryGetValue(command.Name, out Handler handler))
            return false;

        _output = output;

        try
        {
            handler(this, command);
        }
        finally
        {
            _output = null;
        }

        return true;
    }

    private void Emit(CommandSpecBase spec) => _output.Add(spec);

    // place_* <slot> [focus=bust] [duration=0fr] [wait]
    private void EmitPlace(StageCommand c, string position)
    {
        Emit(new MiniPlaceSpec
        {
            slot = c.Arg(0),
            position = position,
            duration = Seconds(c, 2, "0fr"),
            wait = c.HasWaitToken(),
        });
    }

    private static float Seconds(StageCommand c, int index, string fallback)
    {
        string token = c.Arg(index);

        if (token == null || token == "wait")
            token = fallback;

        if (TryParseDuration(token, out float seconds))
            return seconds;

        Debug.LogWarning($"[MiniStageCommandParser] <<{c.Name}>> 인자 {index}가 시간이 아니다('{token}'). {fallback}으로 간다.");
        TryParseDuration(fallback, out seconds);
        return seconds;
    }

    public static bool TryParseDuration(string token, out float seconds)
    {
        seconds = 0f;

        if (string.IsNullOrEmpty(token))
            return false;

        float scale = 1f;
        string number = token;

        if (token.EndsWith("fr", StringComparison.Ordinal))
        {
            number = token.Substring(0, token.Length - 2);
            scale = 1f / FramesPerSecond;
        }
        else if (token.EndsWith("s", StringComparison.Ordinal))
        {
            number = token.Substring(0, token.Length - 1);
        }

        if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return false;

        seconds = Mathf.Max(0f, value * scale);
        return true;
    }
}
