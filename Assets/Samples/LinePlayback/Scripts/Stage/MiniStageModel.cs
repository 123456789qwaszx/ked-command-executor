using System.Collections.Generic;
using System.Text;

// "명령이 전부 끝났다면 무대는 어디에 있어야 하는가"를 시간 없이 접는 순수 모델.
// ked-presentation-runtime의 Core(Reduce)와 등가성 하네스(StageEquivalenceHarness)를 아주 작게 흉내 낸 것이다.
//
// 감사는 스텝 경계마다 실제 무대 사진을 이 모델과 맞춰 본다. 어떤 길로 왔든 — 보통 재생 · 서두르기 ·
// 스텝 경계 Finish · 압축(OnSkip) · 롤백 시크 — 줄이 정리된 뒤의 무대는 같아야 한다.
public sealed class MiniStageModel
{
    private sealed class SlotState
    {
        public string Key;
        public float X;
        public float Alpha;
        public string Face;
    }

    private readonly List<SlotState> _slots = new();
    private readonly Dictionary<string, SlotState> _byKey = new();
    private string _bgm;

    public MiniStageModel(MiniStage stage)
    {
        foreach (MiniStage.Slot slot in stage.Slots)
        {
            var s = new SlotState { Key = slot.Key };
            _slots.Add(s);
            _byKey.Add(slot.Key, s);
        }

        Reset();
    }

    public void Reset()
    {
        foreach (SlotState s in _slots)
        {
            s.X = 0f;
            s.Alpha = 0f;
            s.Face = MiniStage.DefaultFace;
        }

        _bgm = null;
    }

    public void Apply(CommandSpecBase spec)
    {
        switch (spec)
        {
            case MiniPlaceSpec place when TryGet(place.slot, out SlotState s) && MiniStage.TryGetPositionX(place.position, out float x):
                s.X = x;
                break;

            case MiniFadeSpec fade when TryGet(fade.slot, out SlotState s):
                s.Alpha = fade.alpha;
                break;

            case MiniFaceSpec face when TryGet(face.slot, out SlotState s):
                s.Face = face.face;
                break;

            case MiniBgmSpec bgm:
                _bgm = bgm.key;
                break;

            case MiniStopBgmSpec _:
                _bgm = null;
                break;

            // pause는 무대를 바꾸지 않는다.
        }
    }

    public string Snapshot()
    {
        var sb = new StringBuilder();

        foreach (SlotState s in _slots)
            MiniStage.AppendSlot(sb, s.Key, s.X, s.Alpha, s.Face);

        MiniStage.AppendBgm(sb, _bgm);
        return sb.ToString();
    }

    private bool TryGet(string key, out SlotState state)
    {
        state = null;
        return key != null && _byKey.TryGetValue(key, out state);
    }
}
