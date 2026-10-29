using System;
using System.Collections.Generic;

[Serializable]
public struct RollbackPoint
{
    public int historyIndex;
    public string nodeName;
    public string lineId;
    public string rawText;

    // 장면 시작 이후 같은 (nodeName, lineId)의 몇 번째 등장인가 (1부터).
    public int occurrence;

    public RollbackPoint(
        int historyIndex,
        string nodeName,
        string lineId,
        string rawText,
        int occurrence)
    {
        this.historyIndex = historyIndex;
        this.nodeName = nodeName;
        this.lineId = lineId;
        this.rawText = rawText;
        this.occurrence = occurrence;
    }
}

public sealed class RollbackHistory
{
    private readonly List<RollbackPoint> _points = new();
    private int _nextHistoryIndex;

    // (노드, 라인)별 등장 횟수. 시크 좌표 용도(occurrence).
    private readonly Dictionary<(string nodeName, string lineId), int> _seenCount = new();

    // 마지막 롤백 요청의 표적. 리플레이를 여는 쪽이 가져가서 표적 뒤 기록을 정리한다.
    private RollbackPoint _pendingTarget;
    private bool _hasPendingTarget;
    
    public IReadOnlyList<RollbackPoint> Points => _points;
    public bool CanRollbackOneStep => _points.Count >= 2;

    // 마지막으로 쌓인 포인트의 historyIndex. 없으면 -1. 선택 기록의 앵커.
    public int LastHistoryIndex => _points.Count == 0 ? -1 : _points[^1].historyIndex;
    
    public void AddRollbackPoint(YarnLineMeta meta)
    {
        (string, string) key = (meta.nodeName, meta.lineId);

        _seenCount.TryGetValue(key, out int seen);
        int occurrence = seen + 1;
        _seenCount[key] = occurrence;

        _points.Add(new RollbackPoint(
            historyIndex: _nextHistoryIndex++,
            nodeName: meta.nodeName,
            lineId: meta.lineId,
            rawText: meta.rawText,
            occurrence: occurrence));
    }

    // 표적 뒤에 커밋된 라인 수 — 백로그 꼬리를 걷을 폭이다.
    // historyIndex는 장면 안에서 단조증가하므로 비교만으로 충분하다.
    public int CountPointsAfter(in RollbackPoint target)
    {
        int count = 0;

        for (int i = _points.Count - 1; i >= 0; i--)
        {
            if (_points[i].historyIndex <= target.historyIndex)
                break;

            count++;
        }

        return count;
    }

    // 백점프용 — historyIndex로 표적을 찾는다. 장면 진입에서 비워져 0부터 빈틈없이 쌓이므로
    // 목록 인덱스가 곧 historyIndex다. 그래도 값을 대조한다.
    public bool TryGetRollbackPoint(int historyIndex, out RollbackPoint target)
    {
        target = default;

        if (historyIndex < 0 || historyIndex >= _points.Count)
            return false;

        if (_points[historyIndex].historyIndex != historyIndex)
            return false;

        target = _points[historyIndex];
        return true;
    }

    public bool GetRollbackPoint(out RollbackPoint target)
    {
        target = default;

        if (!CanRollbackOneStep)
            return false;

        int targetListIndex = _points.Count - 2;
        target = _points[targetListIndex];
        
        return true;
    }

    
    // Seek 판정
    public void MarkRollbackTarget(in RollbackPoint target)
    {
        _pendingTarget = target;
        _hasPendingTarget = true;
    }

    public bool TakeRollbackTarget(out RollbackPoint target)
    {
        target = _pendingTarget;

        bool had = _hasPendingTarget;
        _hasPendingTarget = false;

        return had;
    }

    public void ClearRollbackPoints()
    {
        _points.Clear();
        _seenCount.Clear();
        _nextHistoryIndex = 0;
    }
}
