using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// 이 샘플의 "무대" — PresentationStage · 캐릭터 리그 자리. 슬롯 셋(위치 · 투명도 · 표정)과 BGM 하나의 상태뿐이고,
// 그리기는 화면(IMGUI)이 한다. 리그와 연출 자체는 ked-stage-runtime의 몫이고, 여기서 보려는 것은
// 이 상태에 **누가 언제 썼는가**다.
//
// 모든 쓰기는 Write를 거친다. 무대는 쓰는 쪽의 스코프와 스텝을 보고 두 가지 위반을 센다:
//   stale write — 이미 끝난 세션(이전 CommandRunScope)의 커맨드가 썼다. 롤백 · 종료 뒤에 살아남은 루틴.
//   late write  — 지금 세션이지만 이미 지나간 스텝의 커맨드가 썼다. 스텝 경계를 넘어 살아남은 메인 루틴.
//                 런 수명 커맨드(BGM)는 원래 경계를 넘으므로 제외한다.
public sealed class MiniStage
{
    public sealed class Slot
    {
        public string Key { get; }
        public string DisplayName { get; }
        public Color Color { get; }

        public float X;        // left -1 · center 0 · right 1
        public float Alpha;
        public string Face;

        internal Slot(string key, string displayName, Color color)
        {
            Key = key;
            DisplayName = displayName;
            Color = color;
            Reset();
        }

        internal void Reset()
        {
            X = 0f;
            Alpha = 0f;
            Face = DefaultFace;
        }
    }

    public const string DefaultFace = "neutral";

    private readonly List<Slot> _slots = new();
    private readonly Dictionary<string, Slot> _byKey = new(StringComparer.Ordinal);
    private readonly MiniTrace _trace;

    public MiniStage(MiniTrace trace)
    {
        _trace = trace;

        Add("s1", "Harin", new Color(0.98f, 0.62f, 0.22f));
        Add("s2", "Sigure", new Color(0.30f, 0.74f, 0.90f));
        Add("s3", "Kasmi", new Color(0.55f, 0.85f, 0.45f));
    }

    public IReadOnlyList<Slot> Slots => _slots;

    // BGM — 런 수명 커맨드가 쓴다.
    public string BgmKey;
    public float BgmVolume;
    public bool BgmStopping;

    // 지금 세션의 스코프. LifecycleProbe가 세션을 열고 닫을 때 맞춘다.
    public CommandRunScope ActiveScope { get; set; }

    // 지금 스텝. LifecycleProbe가 줄에 들어갈 때 맞춘다(MiniTrace.CurrentStep과 같은 값).
    public int CurrentStep { get; set; }

    public int StaleWrites { get; private set; }
    public int LateWrites { get; private set; }

    public bool TryGet(string key, out Slot slot)
    {
        slot = null;
        return key != null && _byKey.TryGetValue(key, out slot);
    }

    public static bool TryGetPositionX(string position, out float x)
    {
        switch (position)
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

    // PresentationStage.Clear 자리 — 새 런(열기 · 리플레이)이 시작될 때.
    public void Clear()
    {
        foreach (Slot slot in _slots)
            slot.Reset();

        BgmKey = null;
        BgmVolume = 0f;
        BgmStopping = false;
    }

    public void ResetCounters()
    {
        StaleWrites = 0;
        LateWrites = 0;
    }

    // 감사가 비교하는 무대 사진. BGM은 무엇이 흐르는지만 본다(볼륨은 화면 전용).
    public string Snapshot()
    {
        var sb = new StringBuilder();

        foreach (Slot s in _slots)
            AppendSlot(sb, s.Key, s.X, s.Alpha, s.Face);

        AppendBgm(sb, BgmKey);
        return sb.ToString();
    }

    public static void AppendSlot(StringBuilder sb, string key, float x, float alpha, string face)
    {
        if (sb.Length > 0)
            sb.Append("  ");

        sb.Append(key)
            .Append("[x").Append(x.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(" a").Append(alpha.ToString("0.00", CultureInfo.InvariantCulture))
            .Append(' ').Append(face)
            .Append(']');
    }

    public static void AppendBgm(StringBuilder sb, string key)
    {
        sb.Append("  bgm[").Append(key ?? "-").Append(']');
    }

    private void Add(string key, string displayName, Color color)
    {
        var slot = new Slot(key, displayName, color);
        _slots.Add(slot);
        _byKey.Add(key, slot);
    }
}
