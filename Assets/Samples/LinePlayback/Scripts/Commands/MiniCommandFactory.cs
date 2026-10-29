using UnityEngine;

// 스펙 → 커맨드. 실행기가 모르는 것(무대의 대상, 장부)을 커맨드 생성자로 넘기는 자리다.
// 스펙의 wait를 WaitForCompletion으로 옮기는 것도, 장부에 수명(Wait · Step · Run)을 적는 것도 여기서 한다.
public sealed class MiniCommandFactory : INodeCommandFactory
{
    private readonly MiniStage _stage;
    private readonly MiniTrace _trace;

    public MiniCommandFactory(MiniStage stage, MiniTrace trace)
    {
        _stage = stage;
        _trace = trace;
    }

    public bool TryCreate(CommandSpecBase spec, out ISequenceCommand command)
    {
        command = null;

        switch (spec)
        {
            case MiniShowSpec show:
                if (!TryTarget(show.target, out MiniStage.Character shown) || !TrySlot(show.slot, out float showX))
                    return false;

                command = new MiniShowCommand(_stage, shown, showX, show, Register(show));
                return true;

            case MiniHideSpec hide:
                if (!TryTarget(hide.target, out MiniStage.Character hidden))
                    return false;

                command = new MiniHideCommand(_stage, hidden, hide, Register(hide));
                return true;

            case MiniMoveSpec move:
                if (!TryTarget(move.target, out MiniStage.Character moved) || !TrySlot(move.slot, out float moveX))
                    return false;

                command = new MiniMoveCommand(_stage, moved, moveX, move, Register(move));
                return true;

            case MiniFaceSpec face:
                if (!TryTarget(face.target, out MiniStage.Character faced))
                    return false;

                command = new MiniFaceCommand(_stage, faced, face, Register(face));
                return true;

            case MiniPauseSpec pause:
                command = new MiniPauseCommand(_stage, pause, _trace.Register(pause.ToString(), MiniTrace.Lifetime.Wait));
                return true;

            case MiniAmbientSpec ambient:
                if (!TryTarget(ambient.target, out MiniStage.Character idler))
                    return false;

                command = new MiniAmbientCommand(_stage, idler, _trace.Register(ambient.ToString(), MiniTrace.Lifetime.Run));
                return true;

            default:
                return false;
        }
    }

    private MiniTrace.Entry Register(CommandSpecBase spec)
        => _trace.Register(spec.ToString(), spec.wait ? MiniTrace.Lifetime.Wait : MiniTrace.Lifetime.Step);

    private bool TryTarget(string id, out MiniStage.Character character)
    {
        if (_stage.TryGet(id, out character))
            return true;

        Debug.LogWarning($"[MiniCommandFactory] Unknown target '{id}'.");
        return false;
    }

    private static bool TrySlot(string slot, out float x)
    {
        if (MiniStage.TryGetSlotX(slot, out x))
            return true;

        Debug.LogWarning($"[MiniCommandFactory] Unknown slot '{slot}' (left | center | right).");
        return false;
    }
}
