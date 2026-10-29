using System.Threading;
using System.Threading.Tasks;

// 원본과 같다. 바뀐 곳: 이어하기(재개 예약) 없음, 진행 호스트용 표면(LastHistoryIndex · IsSeeking · AbandonSeek) 없음.

/// <summary>
/// 재생 단위 하나의 수명 — 열기, 재생, 정지, 루트부터 다시 달리기.
///
/// 이 저장소는 재생 단위의 루트를 하나만 둔다(PLAN_playback-unit E3·E4).
/// 롤백 기록·백로그의 장면 경계·줄 시크는 전부 "지금 단위의 루트"를 기준으로 움직이고,
/// 그 기준이 서고 무너지는 순서를 이 타입 하나가 갖는다.
///
/// 호스트가 말하는 것은 둘뿐이다.
/// - 이 단위에서 무엇을 재생할지 — PlayAsync(nodeName)
/// - 되감을 때 자기 기록을 어디까지 자를지 — RestartFromRootAsync가 돌려주는 롤백 표적 색인
///
/// 순서:
///   열기     정지(이전 단위 정리) → BeginScene → 백로그 장면 경계
///   다시     정지 완료 대기 → PrepareReplay → 롤백 표적 소비
///   정지     재생 구간(열기 또는 다시부터)마다 한 번. 진행 중인 정지가 있으면 그것을 돌려준다.
///
/// 정지를 한 구간에 한 번으로 묶는 이유: ScenePresentation.StopAsync는 스코프를 끝낸다.
/// 리플레이 요청과 끝내기 요청이 겹치면 두 번 끝나 버린다.
/// 롤백 기록은 정지에서 비워지므로, 단위를 열 때 정지가 빠지면 롤백 좌표와 백로그 좌표가 어긋난다.
/// </summary>
public sealed class PlaybackUnit
{
    private readonly IPlaybackStage _stage;
    private readonly BacklogRecorder _backlog;
    private readonly RollbackHistory _rollbackHistory;

    private Task _stopTask = Task.CompletedTask;
    private bool _stopIssued;

    internal PlaybackUnit(
        IPlaybackStage stage,
        BacklogRecorder backlog,
        RollbackHistory rollbackHistory)
    {
        _stage = stage;
        _backlog = backlog;
        _rollbackHistory = rollbackHistory;
    }

    /// <summary>새 단위를 연다. 이전 단위가 남긴 무대를 먼저 정리한다.</summary>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        _stopIssued = false;
        await StopAsync();

        // 정리하는 사이 끝내기 요청이 왔다면 열지 않는다.
        cancellationToken.ThrowIfCancellationRequested();

        _stopIssued = false;

        _stage.BeginScene();
        _backlog.MarkSceneBoundary();
    }

    /// <summary>이 단위에서 노드 하나를 재생한다. 정지되면 끝난다.</summary>
    public Task PlayAsync(string nodeName) => _stage.PlayEpisodeAsync(nodeName);

    /// <summary>재생을 멈춘다. 한 구간에 한 번만 실제로 멈추고, 겹친 요청은 같은 작업을 기다린다.</summary>
    public Task StopAsync()
    {
        if (_stopIssued)
            return _stopTask;

        _stopIssued = true;
        _stopTask = _stage.StopAsync();
        return _stopTask;
    }

    /// <summary>
    /// 같은 단위를 루트부터 다시 달릴 준비를 한다. 롤백 표적이 있으면 그 색인을 돌려준다(없으면 null).
    /// 호스트는 그 색인 뒤의 자기 기록을 자르고 루트 노드부터 다시 재생한다.
    /// 표적 줄까지는 줄 시크가 데려간다.
    /// </summary>
    public async Task<int?> RestartFromRootAsync(CancellationToken cancellationToken = default)
    {
        // 리플레이 요청이 건 정지가 끝날 때까지 기다린다. 아직 안 걸렸다면 여기서 건다.
        await StopAsync();

        cancellationToken.ThrowIfCancellationRequested();

        _stage.PrepareReplay();
        _stopIssued = false;

        return _rollbackHistory.TakeRollbackTarget(out RollbackPoint target)
            ? target.historyIndex
            : null;
    }
}
