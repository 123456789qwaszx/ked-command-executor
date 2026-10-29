using System;
using System.Globalization;

// 무대 파서가 낳는 스펙들. 실행기는 이 필드들을 읽지 않는다 — 팩토리와 순수 모델(MiniStageModel)만 읽는다.
// 상속받은 wait는 팩토리가 커맨드의 WaitForCompletion으로 옮긴다.

[Serializable]
public sealed class MiniPlaceSpec : CommandSpecBase
{
    public string slot;
    public string position;
    public float duration;

    public override string ToString() => $"place_{position} {slot} {Fmt.Seconds(duration)}{Fmt.Wait(wait)}";
}

[Serializable]
public sealed class MiniFadeSpec : CommandSpecBase
{
    public string slot;
    public float alpha;
    public float duration;

    public override string ToString() => $"{(alpha > 0f ? "fade_in" : "fade_out")} {slot} {Fmt.Seconds(duration)}{Fmt.Wait(wait)}";
}

[Serializable]
public sealed class MiniFaceSpec : CommandSpecBase
{
    public string slot;
    public string face;

    public override string ToString() => $"face {slot} {face}";
}

// 언제나 기다린다 — 메인 루틴을 붙드는 명령의 대표(원본 WaitCommand).
[Serializable]
public sealed class MiniPauseSpec : CommandSpecBase
{
    public float seconds;

    public override string ToString() => $"pause {Fmt.Seconds(seconds)}";
}

// 런 수명 — 줄 경계를 넘어 런이 끝날 때까지(또는 stop_bgm까지) 흐른다.
[Serializable]
public sealed class MiniBgmSpec : CommandSpecBase
{
    public string key;
    public float fade;

    public override string ToString() => $"bgm {key} {Fmt.Seconds(fade)}";
}

[Serializable]
public sealed class MiniStopBgmSpec : CommandSpecBase
{
    public float fade;

    public override string ToString() => $"stop_bgm {Fmt.Seconds(fade)}";
}

internal static class Fmt
{
    public static string Seconds(float s) => s.ToString("0.##", CultureInfo.InvariantCulture) + "s";
    public static string Wait(bool wait) => wait ? " wait" : "";
}
