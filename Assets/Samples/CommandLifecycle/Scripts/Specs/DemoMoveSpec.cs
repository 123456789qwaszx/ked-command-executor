using System;

// "A를 x까지 duration초 동안 옮긴다." 실행기는 이 필드들을 읽지 않는다 — 읽는 것은 팩토리다.
// 상속받은 wait도 마찬가지로, 커맨드의 WaitForCompletion으로 옮기는 것은 DemoCommandFactory의 몫이다.
[Serializable]
public sealed class DemoMoveSpec : CommandSpecBase
{
    public string target;
    public float x;
    public float duration;

    public override string ToString() => $"Move({target}, x={x:0.#}, {duration:0.0#}s)";
}
