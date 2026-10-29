public sealed class VNLinePresentationState : ISeekStateQuery
{
    private readonly VNSeekState _seekState = new ();
    public VNSeekKind SeekKind => _seekState.Kind;
    public bool IsSeekingActive => _seekState.IsSeeking;
    
    public bool IsLineFullyShown { get; private set; } = true;
    
    // 지금 줄이 #auto이고 아직 넘기지 않았다(VNLineAdvanceTag). 다 보이면 재생 입력의 프레임이 넘기고 내린다.
    // 줄에 들어갈 때 · 무너질 때 · 시크를 걸 때 내린다 — 다음 줄로 새지 않는다.
    public bool IsLineAutoAdvancePending { get; private set; }

    public void BeginRollbackSeek(string nodeName, string lineId, int occurrence)
        => BeginSeek(VNSeekKind.Rollback, nodeName, lineId, occurrence);

    // 로드 — 장면 루트에서 표적까지. 롤백과 달리 백로그를 다시 적는다(VNYarnLineBoundary).
    public void BeginLoadSeek(string nodeName, string lineId, int occurrence)
        => BeginSeek(VNSeekKind.Load, nodeName, lineId, occurrence);

    private void BeginSeek(VNSeekKind kind, string nodeName, string lineId, int occurrence)
    {
        IsLineFullyShown = false;
        IsLineAutoAdvancePending = false;
        _seekState.Begin(kind, nodeName, lineId, occurrence);
    }

    public bool IsSeekTargetLine(YarnLineMeta meta) => _seekState.IsCurrentTarget(meta);

    public void ClearSeek()
    {
        _seekState.Clear();
    }
    
    public void MarkLineEntered()
    {
        IsLineFullyShown = false;
        IsLineAutoAdvancePending = false;
    }
    
    // 줄의 넘김 태그를 정한 뒤(시크 판정 다음) 한 번. 시크로 지나가는 줄에는 부르지 않는다.
    public void SetLineAutoAdvance(bool autoAdvance)
    {
        IsLineAutoAdvancePending = autoAdvance;
    }

    // 넘김 요청이 받아들여졌다 — 같은 줄에서 두 번 넘기지 않는다.
    public void ConsumeLineAutoAdvance()
    {
        IsLineAutoAdvancePending = false;
    }

    // 라인이 안정된 경계까지 갔다(= Ready).
    public void MarkLineDisplayCompleted(YarnLineMeta meta, string reason)
    {
        IsLineFullyShown = true;
    }

    // 라인이 정상 경계 전에 무너짐.(= Released). 롤백 · 정지 · 선점으로 잘린 경우.
    public void MarkLineTornDown(YarnLineMeta meta, string reason)
    {
        IsLineFullyShown = false;
        IsLineAutoAdvancePending = false;
    }
}