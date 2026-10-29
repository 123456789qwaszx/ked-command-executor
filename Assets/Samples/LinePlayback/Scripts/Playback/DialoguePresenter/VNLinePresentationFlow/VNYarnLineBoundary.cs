using MiniYarn;

public readonly struct YarnLineMeta
{
    public readonly string nodeName;
    public readonly string lineId;
    public readonly string charName;
    public readonly string rawText;

    public YarnLineMeta(string nodeName, string lineId, string charName, string rawText)
    {
        this.nodeName = nodeName;
        this.lineId = lineId;
        this.charName = charName;
        this.rawText = rawText;
    }
}

// 원본과 같다.
public sealed class VNYarnLineBoundary
{
    private readonly BacklogRecorder _backlogRecorder;
    private readonly RollbackHistory _rollbackHistory;
    private readonly VNRuntimeStateProvider _runtimeStateProvider;
    private readonly VNLinePresentationState _advanceState;

    public VNYarnLineBoundary(
        BacklogRecorder backlogRecorder,
        RollbackHistory rollbackHistory,
        VNRuntimeStateProvider runtimeStateProvider,
        VNLinePresentationState advanceState)
    {
        _backlogRecorder = backlogRecorder;
        _rollbackHistory = rollbackHistory;
        _runtimeStateProvider = runtimeStateProvider;
        _advanceState = advanceState;
    }

    public YarnLineMeta BuildLineMeta(LocalizedLine line, string nodeName)
    {
        return new YarnLineMeta(
            nodeName,
            line.TextID,
            line.CharacterName,
            line.TextWithoutCharacterName.Text);
    }

    public void CommitLineEntered(YarnLineMeta meta)
    {
        _runtimeStateProvider.UpdateCurrentLineMeta(meta);

        // 시크 패스스루 동안 두 기록의 규칙이 갈린다:
        //   롤백 포인트 — 다시 쌓는다. 장면 시작에서 지워졌고, occurrence 재계산과
        //                 다음 롤백의 근거가 이 재적재다.
        //   백로그    — **롤백** 시크에서만 안 쌓는다. 롤백 리플레이는 이미 백로그에
        //                 있는 라인을 다시 지나가는 것이라 또 적으면 중복이다
        //                 (롤백이 지우는 것은 표적 뒤 꼬리뿐 — TruncateFromEnd).
        //                 반면 Load 시크는 빈 백로그에서 시작하므로 다시 적는 게 맞다 —
        //                 로드 후 백로그 = 장면 처음부터 위치까지.
        bool suppressBacklog =
            _advanceState.IsSeekingActive &&
            _advanceState.SeekKind == VNSeekKind.Rollback;

        if (!suppressBacklog)
            _backlogRecorder.Record(meta);

        _rollbackHistory.AddRollbackPoint(meta);
    }
}
