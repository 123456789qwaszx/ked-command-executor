using System;
using System.Globalization;
using UnityEngine;

// YarnCommandBridge 흉내 — 대본 명령의 토큰을 스펙으로 바꿔 드라이버에 쌓는다. 실행하지 않는다.
//
//   <<show A left [duration] [wait]>>     <<hide A [duration] [wait]>>
//   <<move A center [duration] [wait]>>   <<face A smile>>
//   <<pause seconds>>                     <<ambient B>>
public sealed class MiniCommandBridge
{
    private const string WaitToken = "wait";

    private readonly MiniPlaybackDriver _driver;

    public MiniCommandBridge(MiniDialogueRunner runner, MiniPlaybackDriver driver)
    {
        _driver = driver;

        runner.AddCommandHandler("show", a => _driver.Enqueue(new MiniShowSpec
        {
            target = Arg(a, 0), slot = Arg(a, 1), duration = Seconds(a, 2, 0.5f), wait = HasWait(a),
        }));

        runner.AddCommandHandler("hide", a => _driver.Enqueue(new MiniHideSpec
        {
            target = Arg(a, 0), duration = Seconds(a, 1, 0.5f), wait = HasWait(a),
        }));

        runner.AddCommandHandler("move", a => _driver.Enqueue(new MiniMoveSpec
        {
            target = Arg(a, 0), slot = Arg(a, 1), duration = Seconds(a, 2, 1f), wait = HasWait(a),
        }));

        runner.AddCommandHandler("face", a => _driver.Enqueue(new MiniFaceSpec
        {
            target = Arg(a, 0), face = Arg(a, 1) ?? MiniStage.DefaultFace,
        }));

        runner.AddCommandHandler("pause", a => _driver.Enqueue(new MiniPauseSpec
        {
            seconds = Seconds(a, 0, 0.5f), wait = true,
        }));

        runner.AddCommandHandler("ambient", a => _driver.Enqueue(new MiniAmbientSpec
        {
            target = Arg(a, 0),
        }));
    }

    private static string Arg(string[] args, int index)
        => index < args.Length && args[index] != WaitToken ? args[index] : null;

    private static float Seconds(string[] args, int index, float fallback)
    {
        string token = Arg(args, index);

        if (token == null)
            return fallback;

        if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
            return Mathf.Max(0f, seconds);

        Debug.LogWarning($"[MiniCommandBridge] '{token}' is not a duration in seconds - using {fallback}.");
        return fallback;
    }

    private static bool HasWait(string[] args) => Array.IndexOf(args, WaitToken) >= 0;
}
