using System;

public enum VNSeekKind
{
    None = 0,
    Rollback = 1,
    Load = 2,
}

public sealed class VNSeekState
{
    public VNSeekKind Kind { get; private set; } = VNSeekKind.None;
    public bool IsSeeking { get; private set; }

    public string TargetNodeName { get; private set; }
    public string TargetLineId { get; private set; }

    // SeekTargetLine의 장면 내 등장 순번 (1부터). RollbackPoint.occurrence와 같은 좌표계.
    public int TargetOccurrence { get; private set; }

    // 시크 시작 이후 (노드, 라인)이 일치한 횟수.
    // 리플레이가 시작에피소드에서부터 같은 길을 루트를 타기 때문에, N번째 일치 = 원래 그 라인.
    private int _matchedCount;

    public void Begin(VNSeekKind kind, string nodeName, string lineId, int occurrence)
    {
        Kind = kind;
        IsSeeking = true;

        TargetNodeName = nodeName;
        TargetLineId = lineId;

        TargetOccurrence = occurrence < 1 ? 1 : occurrence;
        _matchedCount = 0;
    }

    public bool IsCurrentTarget(YarnLineMeta meta)
    {
        if (!IsSeeking)
            return false;

        if (!string.Equals(meta.nodeName, TargetNodeName, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrWhiteSpace(TargetLineId))
            return false;

        if (!string.Equals(meta.lineId, TargetLineId, StringComparison.Ordinal))
            return false;

        _matchedCount++;
        return _matchedCount == TargetOccurrence;
    }

    public void Clear()
    {
        Kind = VNSeekKind.None;
        IsSeeking = false;

        TargetNodeName = null;
        TargetLineId = null;

        TargetOccurrence = 0;
        _matchedCount = 0;
    }
}