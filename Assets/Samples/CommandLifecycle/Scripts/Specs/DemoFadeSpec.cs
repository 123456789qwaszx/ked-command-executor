using System;

// "B의 alpha를 duration초 동안 alpha까지 바꾼다."
[Serializable]
public sealed class DemoFadeSpec : CommandSpecBase
{
    public string target;
    public float alpha;
    public float duration;

    public override string ToString() => $"Fade({target}, a={alpha:0.#}, {duration:0.0#}s)";
}
