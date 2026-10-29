using System.Collections.Generic;
using System.Text;

// "명령이 전부 끝났다면 무대는 어디에 있어야 하는가"를 시간 없이 접는 순수 모델.
// ked-presentation-runtime의 Core(Reduce)와 등가성 하네스를 아주 작게 흉내 낸 것이다.
//
// 감사는 스텝 경계마다 실제 무대 사진을 이 모델과 맞춰 본다. 어떤 길로 왔든 — 보통 재생 · 서두르기 ·
// 스텝 경계 Finish · 압축(OnSkip) · 롤백 시크 — 줄이 정리된 뒤의 무대는 같아야 한다.
public sealed class MiniStageModel
{
    private sealed class State
    {
        public string Id;
        public float X;
        public float Alpha;
        public string Face;
        public bool Ambient;
    }

    private readonly List<State> _states = new();
    private readonly Dictionary<string, State> _byId = new();

    public MiniStageModel(MiniStage stage)
    {
        foreach (MiniStage.Character c in stage.Characters)
        {
            var s = new State { Id = c.Id };
            _states.Add(s);
            _byId.Add(c.Id, s);
        }

        Reset();
    }

    public void Reset()
    {
        foreach (State s in _states)
        {
            s.X = 0f;
            s.Alpha = 0f;
            s.Face = MiniStage.DefaultFace;
            s.Ambient = false;
        }
    }

    public void Apply(CommandSpecBase spec)
    {
        switch (spec)
        {
            case MiniShowSpec show when TryGet(show.target, out State s) && MiniStage.TryGetSlotX(show.slot, out float x):
                s.X = x;
                s.Alpha = 1f;
                break;

            case MiniHideSpec hide when TryGet(hide.target, out State s):
                s.Alpha = 0f;
                break;

            case MiniMoveSpec move when TryGet(move.target, out State s) && MiniStage.TryGetSlotX(move.slot, out float x):
                s.X = x;
                break;

            case MiniFaceSpec face when TryGet(face.target, out State s):
                s.Face = face.face;
                break;

            case MiniAmbientSpec ambient when TryGet(ambient.target, out State s):
                s.Ambient = true;
                break;

            // pause는 무대를 바꾸지 않는다.
        }
    }

    public string Snapshot()
    {
        var sb = new StringBuilder();

        foreach (State s in _states)
            MiniStage.AppendState(sb, s.Id, s.X, s.Alpha, s.Face, s.Ambient);

        return sb.ToString();
    }

    private bool TryGet(string id, out State state)
    {
        state = null;
        return id != null && _byId.TryGetValue(id, out state);
    }
}
