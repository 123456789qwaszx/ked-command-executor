using UnityEngine;

// 무대 상태를 바꾸는 커맨드들. 모양은 MiniCommandBase가 갖고, 여기는 "무엇을 쓰는가"만 있다.
// 각 Commit은 MiniStageModel.Apply와 같은 최종 상태를 쓴다 — 둘이 어긋나면 감사가 잡는다.

public sealed class MiniPlaceCommand : MiniCommandBase
{
    private readonly MiniStage.Slot _target;
    private readonly float _x;
    private readonly float _duration;
    private float _fromX;

    public MiniPlaceCommand(MiniStage stage, MiniStage.Slot target, float x, MiniPlaceSpec spec, MiniTrace.Entry trace)
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

public sealed class MiniFadeCommand : MiniCommandBase
{
    private readonly MiniStage.Slot _target;
    private readonly float _alpha;
    private readonly float _duration;
    private float _fromAlpha;

    public MiniFadeCommand(MiniStage stage, MiniStage.Slot target, MiniFadeSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _target = target;
        _alpha = spec.alpha;
        _duration = spec.duration;
    }

    protected override float Duration => _duration;

    protected override void Begin() => _fromAlpha = _target.Alpha;

    protected override void Apply(float t) => Write(() => _target.Alpha = Mathf.Lerp(_fromAlpha, _alpha, t));

    protected override void Commit() => Write(() => _target.Alpha = _alpha);
}

public sealed class MiniFaceCommand : MiniCommandBase
{
    private readonly MiniStage.Slot _target;
    private readonly string _face;

    public MiniFaceCommand(MiniStage stage, MiniStage.Slot target, MiniFaceSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _target = target;
        _face = spec.face;
    }

    protected override float Duration => 0f;

    protected override void Commit() => Write(() => _target.Face = _face);
}

// 원본 WaitCommand 자리. 무대를 바꾸지 않고 시간만 든다. 언제나 기다리는 명령이라 메인 루틴을 붙든다 —
// 그동안 사용자의 넘기기는 AdvanceGate가 거절한다(cps_node_busy). 압축되면 OnSkip으로 바로 끝난다.
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

// stop_bgm — 스텝 수명. 흐르던 BGM(런 수명 커맨드)을 내린다. 볼륨을 줄이고, 끝에 BGM 키를 비운다.
// BGM 커맨드는 키가 비는 것을 보고 스스로 끝난다.
public sealed class MiniStopBgmCommand : MiniCommandBase
{
    private readonly float _fade;
    private float _fromVolume;

    public MiniStopBgmCommand(MiniStage stage, MiniStopBgmSpec spec, MiniTrace.Entry trace)
        : base(stage, spec.wait, trace)
    {
        _fade = spec.fade;
    }

    protected override float Duration => _fade;

    protected override void Begin()
    {
        _fromVolume = Stage.BgmVolume;
        Write(() => Stage.BgmStopping = true);
    }

    protected override void Apply(float t) => Write(() => Stage.BgmVolume = Mathf.Lerp(_fromVolume, 0f, t));

    protected override void Commit() => Write(() =>
    {
        Stage.BgmKey = null;
        Stage.BgmVolume = 0f;
        Stage.BgmStopping = false;
    });
}
