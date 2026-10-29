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
            case MiniPlaceSpec place:
                if (!TrySlot(place.slot, out MiniStage.Slot placed) || !TryPosition(place.position, out float x))
                    return false;

                command = new MiniPlaceCommand(_stage, placed, x, place, Register(place));
                return true;

            case MiniFadeSpec fade:
                if (!TrySlot(fade.slot, out MiniStage.Slot faded))
                    return false;

                command = new MiniFadeCommand(_stage, faded, fade, Register(fade));
                return true;

            case MiniFaceSpec face:
                if (!TrySlot(face.slot, out MiniStage.Slot faced))
                    return false;

                command = new MiniFaceCommand(_stage, faced, face, Register(face));
                return true;

            case MiniPauseSpec pause:
                command = new MiniPauseCommand(_stage, pause, _trace.Register(pause.ToString(), MiniTrace.Lifetime.Wait));
                return true;

            case MiniBgmSpec bgm:
                command = new MiniBgmCommand(_stage, bgm, _trace.Register(bgm.ToString(), MiniTrace.Lifetime.Run));
                return true;

            case MiniStopBgmSpec stopBgm:
                command = new MiniStopBgmCommand(_stage, stopBgm, Register(stopBgm));
                return true;

            default:
                return false;
        }
    }

    private MiniTrace.Entry Register(CommandSpecBase spec)
        => _trace.Register(spec.ToString(), spec.wait ? MiniTrace.Lifetime.Wait : MiniTrace.Lifetime.Step);

    private bool TrySlot(string key, out MiniStage.Slot slot)
    {
        if (_stage.TryGet(key, out slot))
            return true;

        Debug.LogWarning($"[MiniCommandFactory] Unknown slot '{key}'.");
        return false;
    }

    private static bool TryPosition(string position, out float x)
    {
        if (MiniStage.TryGetPositionX(position, out x))
            return true;

        Debug.LogWarning($"[MiniCommandFactory] Unknown position '{position}'.");
        return false;
    }
}
