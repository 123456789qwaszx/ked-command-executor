using System;
using System.Collections.Generic;

// 대본 안의 한 줄의 좌표. 같은 line ID가 반복될 수 있어 런 안 등장 순번까지 쓴다(RollbackPoint.occurrence와 같은 좌표계).
public readonly struct MiniLineKey
{
    public readonly string LineId;
    public readonly int Occurrence;

    public MiniLineKey(string lineId, int occurrence)
    {
        LineId = lineId;
        Occurrence = occurrence;
    }

    public string Id => Occurrence > 1 ? $"{LineId}#{Occurrence}" : LineId;

    public bool Matches(MiniLineKey other) =>
        Occurrence == other.Occurrence && string.Equals(LineId, other.LineId, StringComparison.Ordinal);
}

// VNLinePresentationState + VNSeekState 흉내 — 지금 줄이 다 보였나, 시크 중인가.
public sealed class MiniLineState
{
    private MiniLineKey _seekTarget;

    public bool IsLineFullyShown { get; private set; }
    public bool IsSeeking { get; private set; }
    public string SeekTargetId => IsSeeking ? _seekTarget.Id : null;

    public void BeginRollbackSeek(MiniLineKey target)
    {
        _seekTarget = target;
        IsSeeking = true;
        IsLineFullyShown = false;
    }

    public bool IsSeekTarget(MiniLineKey line) => IsSeeking && _seekTarget.Matches(line);

    public void ClearSeek() => IsSeeking = false;

    public void MarkLineEntered() => IsLineFullyShown = false;

    // 줄이 안정된 경계까지 갔다(시크 패스스루도 여기로 끝난다).
    public void MarkLineDisplayed() => IsLineFullyShown = true;

    // 줄이 정상 경계 전에 무너졌다 — 롤백 · 취소로 잘렸다.
    public void MarkLineTornDown() => IsLineFullyShown = false;
}

// RollbackHistory 흉내 — 런 안에서 들어간 줄을 차례로 적는다. 한 걸음 되감기의 표적은 지금 줄의 바로 앞 줄이다.
// 롤백은 런을 정지하며 이 기록을 비우고, 루트부터 다시 달리는 시크가 지나가는 줄마다 다시 쌓는다.
public sealed class MiniRollbackHistory
{
    private readonly List<MiniLineKey> _points = new();
    private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

    public int Count => _points.Count;

    public MiniLineKey Add(string lineId)
    {
        _seen.TryGetValue(lineId, out int seen);
        _seen[lineId] = ++seen;

        var key = new MiniLineKey(lineId, seen);
        _points.Add(key);
        return key;
    }

    public bool TryGetOneStepBack(out MiniLineKey target)
    {
        target = default;

        if (_points.Count < 2)
            return false;

        target = _points[_points.Count - 2];
        return true;
    }

    public void Clear()
    {
        _points.Clear();
        _seen.Clear();
    }
}
