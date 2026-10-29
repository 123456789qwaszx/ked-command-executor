using System;
using System.Globalization;

// 대본 명령 하나가 낳는 스펙들. 실행기는 이 필드들을 읽지 않는다 — 팩토리와 순수 모델(MiniStageModel)만 읽는다.
// 상속받은 wait는 팩토리가 커맨드의 WaitForCompletion으로 옮긴다.

[Serializable]
public sealed class MiniShowSpec : CommandSpecBase
{
    public string target;
    public string slot;
    public float duration;

    public override string ToString() => $"show {target} {slot} {Seconds(duration)}{WaitSuffix(wait)}";

    internal static string Seconds(float s) => s.ToString("0.##", CultureInfo.InvariantCulture) + "s";
    internal static string WaitSuffix(bool wait) => wait ? " wait" : "";
}

[Serializable]
public sealed class MiniHideSpec : CommandSpecBase
{
    public string target;
    public float duration;

    public override string ToString() => $"hide {target} {MiniShowSpec.Seconds(duration)}{MiniShowSpec.WaitSuffix(wait)}";
}

[Serializable]
public sealed class MiniMoveSpec : CommandSpecBase
{
    public string target;
    public string slot;
    public float duration;

    public override string ToString() => $"move {target} {slot} {MiniShowSpec.Seconds(duration)}{MiniShowSpec.WaitSuffix(wait)}";
}

[Serializable]
public sealed class MiniFaceSpec : CommandSpecBase
{
    public string target;
    public string face;

    public override string ToString() => $"face {target} {face}";
}

// 언제나 기다린다 — 메인 루틴을 붙드는 명령의 대표.
[Serializable]
public sealed class MiniPauseSpec : CommandSpecBase
{
    public float seconds;

    public override string ToString() => $"pause {MiniShowSpec.Seconds(seconds)}";
}

// 런 수명 — 줄 경계를 넘어 런이 끝날 때까지 산다.
[Serializable]
public sealed class MiniAmbientSpec : CommandSpecBase
{
    public string target;

    public override string ToString() => $"ambient {target}";
}
