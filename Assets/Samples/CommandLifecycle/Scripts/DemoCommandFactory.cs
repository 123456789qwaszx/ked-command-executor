using UnityEngine;

// 스펙 → 커맨드. 실행기가 모르는 것(대상 도형, 데모 장부)을 커맨드 생성자로 넘기는 자리다.
// CommandSpecBase.wait → WaitForCompletion도 여기서 옮긴다 — 프레임워크가 자동으로 잇지 않는다.
public sealed class DemoCommandFactory : INodeCommandFactory
{
    private readonly DemoStage _stage;
    private readonly DemoTrace _trace;

    public DemoCommandFactory(DemoStage stage, DemoTrace trace)
    {
        _stage = stage;
        _trace = trace;
    }

    public bool TryCreate(CommandSpecBase spec, out ISequenceCommand command)
    {
        command = null;

        switch (spec)
        {
            case DemoMoveSpec move:
                if (!TryResolve(move.target, out DemoStage.Shape mover))
                    return false;

                command = new DemoMoveCommand(
                    mover.Transform, move.x, move.duration, move.wait,
                    _trace.Register(move.ToString(), move.wait));
                return true;

            case DemoFadeSpec fade:
                if (!TryResolve(fade.target, out DemoStage.Shape fader))
                    return false;

                command = new DemoFadeCommand(
                    fader.Renderer, fade.alpha, fade.duration, fade.wait,
                    _trace.Register(fade.ToString(), fade.wait));
                return true;

            default:
                return false;
        }
    }

    private bool TryResolve(string id, out DemoStage.Shape shape)
    {
        if (_stage.TryGet(id, out shape))
            return true;

        Debug.LogWarning($"[DemoCommandFactory] Unknown target '{id}'.");
        return false;
    }
}
