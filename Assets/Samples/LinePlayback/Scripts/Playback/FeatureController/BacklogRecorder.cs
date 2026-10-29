using System;
using System.Collections.Generic;

[Serializable]
public struct DialogueLogEntry
{
    public string lineId;
    public int lineSequence;
    public string nodeName;
    public string rawText;
    public double timestamp;
}

// 백로그는 playthrough 전체에서 유지한다.
// Scene 전환이나 replay에서는 지우지 않고,
// rollback 시 현재 Scene의 표적 뒤 로그만 제거한다.
public sealed class BacklogRecorder
{
    private const int MAXLOGCOUNT = 300;

    private readonly List<DialogueLogEntry> _entries = new(MAXLOGCOUNT);

    private int _nextLineSequence;

    // 현재 위치부터 다시 재생할 수 있도록,
    // 다음 라인의 sequence를 Scene 시작 경계로 저장.
    private int _sceneStartLineSequence;

    public IReadOnlyList<DialogueLogEntry> Entries => _entries;

    // 다음에 기록되는 라인이 받을 회차(playthrough) 전체 순번.
    public int NextLineSequence => _nextLineSequence;

    
    public void MarkSceneBoundary() =>
        _sceneStartLineSequence = _nextLineSequence;
    
    public bool IsInCurrentScene(in DialogueLogEntry entry) => 
        entry.lineSequence >= _sceneStartLineSequence;
    
    // 백로그 전체 좌표를 현재 Scene의 rollback 좌표로 변환한다.
    // 현재 Scene의 항목이 아니면 -1.
    public int GetCurrentSceneHistoryIndex(in DialogueLogEntry entry)
    { 
        return IsInCurrentScene(entry)
            ? entry.lineSequence - _sceneStartLineSequence
            : -1;
    }

    
    public void Record(YarnLineMeta meta)
    {
        Add(new DialogueLogEntry
        {
            lineId = meta.lineId,
            lineSequence = _nextLineSequence++,
            nodeName = meta.nodeName,
            rawText = meta.rawText,
        });
    }
    
    public void Restore(IReadOnlyList<DialogueLogEntry> entries)
    {
        _entries.Clear();

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
                Add(entries[i]);
        }

        _nextLineSequence = _entries.Count == 0
            ? 0
            : _entries[^1].lineSequence + 1;

        _sceneStartLineSequence = _nextLineSequence;
    }
    
    public void TruncateFromEnd(int count)
    {
        if (count <= 0)
            return;

        int removed = Math.Min(count, _entries.Count);

        if (removed >= _entries.Count)
            _entries.Clear();
        else
            _entries.RemoveRange(_entries.Count - removed, removed);

        _nextLineSequence -= removed;
    }


    private void Add(in DialogueLogEntry entry)
    {
        _entries.Add(entry);

        if (_entries.Count > MAXLOGCOUNT)
            _entries.RemoveAt(0);
    }
}