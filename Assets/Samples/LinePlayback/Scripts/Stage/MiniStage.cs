using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// 이 샘플의 "무대" — 캐릭터 둘의 상태(위치 · 투명도 · 표정 · 대기 루프)뿐이다. 그리기는 화면(IMGUI)이 한다.
// 리그나 연출 자체는 ked-stage-runtime의 몫이고, 여기서 보려는 것은 이 상태에 **누가 언제 썼는가**다.
//
// 모든 쓰기는 Write를 거친다. 무대는 쓰는 쪽의 스코프와 스텝을 보고 두 가지 위반을 센다:
//   stale write — 이미 끝난 세션(이전 CommandRunScope)의 커맨드가 썼다. 롤백 · 취소 뒤에 살아남은 루틴.
//   late write  — 지금 세션이지만 이미 지나간 스텝의 커맨드가 썼다. 스텝 경계를 넘어 살아남은 메인 루틴.
//                 런 수명 커맨드는 원래 경계를 넘으므로 제외한다.
public sealed class MiniStage
{
    public sealed class Character
    {
        public string Id { get; }
        public Color Color { get; }

        public float X;        // 슬롯 좌표: left -1 · center 0 · right 1
        public float Alpha;
        public string Face;
        public bool Ambient;   // 런 수명 대기 루프가 돌고 있다
        public float Bob;      // 대기 루프의 출렁임 — 화면 전용, 감사 대상이 아니다

        internal Character(string id, Color color)
        {
            Id = id;
            Color = color;
            Reset();
        }

        internal void Reset()
        {
            X = 0f;
            Alpha = 0f;
            Face = DefaultFace;
            Ambient = false;
            Bob = 0f;
        }
    }

    public const string DefaultFace = "neutral";

    private readonly List<Character> _characters = new();
    private readonly Dictionary<string, Character> _byId = new(StringComparer.Ordinal);
    private readonly MiniTrace _trace;

    public MiniStage(MiniTrace trace)
    {
        _trace = trace;

        Add("A", new Color(0.98f, 0.62f, 0.22f));
        Add("B", new Color(0.30f, 0.74f, 0.90f));
    }

    public IReadOnlyList<Character> Characters => _characters;

    // 지금 세션의 스코프. 호스트가 세션을 열고 닫을 때 맞춘다.
    public CommandRunScope ActiveScope { get; set; }

    // 지금 스텝. 드라이버가 줄에 들어갈 때 맞춘다(MiniTrace.CurrentStep과 같은 값).
    public int CurrentStep { get; set; }

    public int StaleWrites { get; private set; }
    public int LateWrites { get; private set; }

    public bool TryGet(string id, out Character character)
    {
        character = null;
        return id != null && _byId.TryGetValue(id, out character);
    }

    public static bool TryGetSlotX(string slot, out float x)
    {
        switch (slot)
        {
            case "left": x = -1f; return true;
            case "center": x = 0f; return true;
            case "right": x = 1f; return true;
            default: x = 0f; return false;
        }
    }

    public void Write(MiniTrace.Entry writer, CommandRunScope scope, Action apply)
    {
        if (scope != ActiveScope)
        {
            if (!writer.FlaggedStale)
            {
                writer.FlaggedStale = true;
                StaleWrites++;
                _trace.Write($"STALE WRITE - [{writer.StepLabel}] {writer.Label} wrote after its session ended");
            }
        }
        else if (writer.Lifetime != MiniTrace.Lifetime.Run && writer.Step < CurrentStep)
        {
            if (!writer.FlaggedLate)
            {
                writer.FlaggedLate = true;
                LateWrites++;
                _trace.Write($"LATE WRITE - [{writer.StepLabel}] {writer.Label} wrote during a later step");
            }
        }

        // 위반이어도 쓴다 — 화면과 감사에 그 결과가 그대로 드러나도록.
        apply();
    }

    public void Clear()
    {
        foreach (Character c in _characters)
            c.Reset();
    }

    public void ResetCounters()
    {
        StaleWrites = 0;
        LateWrites = 0;
    }

    // 감사가 비교하는 무대 사진. Bob은 빼고, 대기 루프는 돌고 있는지만 본다.
    public string Snapshot()
    {
        var sb = new StringBuilder();

        foreach (Character c in _characters)
            AppendState(sb, c.Id, c.X, c.Alpha, c.Face, c.Ambient);

        return sb.ToString();
    }

    public static void AppendState(StringBuilder sb, string id, float x, float alpha, string face, bool ambient)
    {
        if (sb.Length > 0)
            sb.Append("  ");

        sb.Append(id)
            .Append("[x").Append(x.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" a").Append(alpha.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(' ').Append(face);

        if (ambient)
            sb.Append(" ~");

        sb.Append(']');
    }

    private void Add(string id, Color color)
    {
        var c = new Character(id, color);
        _characters.Add(c);
        _byId.Add(id, c);
    }
}
