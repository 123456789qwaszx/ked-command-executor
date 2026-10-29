using UnityEngine;

// 무대 상태를 바꾸는 네 커맨드. 모양은 MiniCommandBase가 갖고, 여기는 "무엇을 쓰는가"만 있다.
// 각 Commit은 MiniStageModel.Apply와 같은 최종 상태를 쓴다 — 둘이 어긋나면 감사가 잡는다.

public sealed class MiniShowCommand : MiniCommandBase
{
    private readonly MiniStage.Character _target;
    private readonly float _x;
    private readonly float _duration;
    private float _fromAlpha;

    public MiniShowCommand(MiniStage stage, MiniStage.Character target, float x, MiniShowSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _target = target;
        _x = x;
        _duration = spec.duration;
    }

    protected override float Duration => _duration;

    protected override void Begin()
    {
        _fromAlpha = _target.Alpha;
        Write(() => _target.X = _x);
    }

    protected override void Apply(float t) => Write(() => _target.Alpha = Mathf.Lerp(_fromAlpha, 1f, t));

    protected override void Commit() => Write(() =>
    {
        _target.X = _x;
        _target.Alpha = 1f;
    });
}

public sealed class MiniHideCommand : MiniCommandBase
{
    private readonly MiniStage.Character _target;
    private readonly float _duration;
    private float _fromAlpha;

    public MiniHideCommand(MiniStage stage, MiniStage.Character target, MiniHideSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _target = target;
        _duration = spec.duration;
    }

    protected override float Duration => _duration;

    protected override void Begin() => _fromAlpha = _target.Alpha;

    protected override void Apply(float t) => Write(() => _target.Alpha = Mathf.Lerp(_fromAlpha, 0f, t));

    protected override void Commit() => Write(() => _target.Alpha = 0f);
}

public sealed class MiniMoveCommand : MiniCommandBase
{
    private readonly MiniStage.Character _target;
    private readonly float _x;
    private readonly float _duration;
    private float _fromX;

    public MiniMoveCommand(MiniStage stage, MiniStage.Character target, float x, MiniMoveSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _target = target;
        _x = x;
        _duration = spec.duration;
    }

    protected override float Duration => _duration;

    protected override void Begin() => _fromX = _target.X;

    protected override void Apply(float t) => Write(() => _target.X = Mathf.Lerp(_fromX, _x, Mathf.SmoothStep(0f, 1f, t)));

    protected override void Commit() => Write(() => _target.X = _x);
}

public sealed class MiniFaceCommand : MiniCommandBase
{
    private readonly MiniStage.Character _target;
    private readonly string _face;

    public MiniFaceCommand(MiniStage stage, MiniStage.Character target, MiniFaceSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _target = target;
        _face = spec.face;
    }

    protected override float Duration => 0f;

    protected override void Commit() => Write(() => _target.Face = _face);
}

// 무대를 바꾸지 않고 시간만 든다. 언제나 기다리는 명령이라 메인 루틴을 붙든다 —
// 그동안 사용자의 넘기기는 거절된다(MiniPlaybackControls). 압축되면 OnSkip으로 바로 끝난다.
public sealed class MiniPauseCommand : MiniCommandBase
{
    private readonly float _seconds;

    public MiniPauseCommand(MiniStage stage, MiniPauseSpec spec, MiniTrace.Entry trace)
        : base(stage, wait: true, trace)
    {
        _seconds = spec.seconds;
    }

    protected override float Duration => _seconds;

    protected override void Commit() { }
}
