// 원본에서 선택 기록(ChoiceHistory)만 뺐다.
public sealed class VNRuntimeStateProvider
{
    private readonly RollbackHistory _rollbackHistory;

    private YarnLineMeta _currentLineMeta;
    private bool _hasCurrentLineMeta;

    public VNRuntimeStateProvider(RollbackHistory rollbackHistory)
    {
        _rollbackHistory = rollbackHistory;
    }

    public string CurrentNodeName
    {
        get
        {
            if (TryGetCurrentSavePoint(out RollbackPoint point))
                return point.nodeName;

            return _hasCurrentLineMeta ? _currentLineMeta.nodeName : "";
        }
    }

    public string CurrentLineId
    {
        get
        {
            if (TryGetCurrentSavePoint(out RollbackPoint point))
                return point.lineId;

            return _hasCurrentLineMeta ? _currentLineMeta.lineId : "";
        }
    }

    public string CurrentCharacterKey
    {
        get
        {
            return _hasCurrentLineMeta ? _currentLineMeta.charName : "";
        }
    }

    public int CurrentVisitedIndex
    {
        get
        {
            if (TryGetCurrentSavePoint(out RollbackPoint point))
                return point.historyIndex;

            return -1;
        }
    }

    public int CurrentLineVisitCountInNode
    {
        get
        {
            return 0;
        }
    }

    public string CurrentChapterLabel
    {
        get
        {
            string nodeName = CurrentNodeName;

            if (string.IsNullOrWhiteSpace(nodeName))
                return "";

            return nodeName;
        }
    }

    public string CurrentLinePreview
    {
        get
        {
            if (TryGetCurrentSavePoint(out RollbackPoint point))
                return point.rawText;

            return _hasCurrentLineMeta ? _currentLineMeta.rawText : "";
        }
    }

    public void UpdateCurrentLineMeta(YarnLineMeta meta)
    {
        _currentLineMeta = meta;
        _hasCurrentLineMeta = true;
    }


    private bool TryGetCurrentSavePoint(out RollbackPoint point)
    {
        if (_rollbackHistory.Points.Count == 0)
        {
            point = default;
            return false;
        }
        
        point =  _rollbackHistory.Points[^1];
        return true;
    }
}