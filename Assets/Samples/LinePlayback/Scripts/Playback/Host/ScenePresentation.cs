using System.Threading.Tasks;
using UnityEngine;

// ked-presentation-runtime의 ScenePresentation을 옮긴 것.
// 바뀐 곳: 화면 전환 · 샷 응답 · 선택 기록 · 이어하기 없음. PresentationStage 자리에 MiniStage.
//          [샘플] 표시가 붙은 관측점(LifecycleProbe) — 세션이 열리고 닫히는 순간과 노드 끝을 감사에 알린다.
//
// 재생 단위(노드 하나) 하나에 필요한 Yarn / Presentation 동작.
// 부르는 순서는 PlaybackUnit이 소유한다 — 호스트(NodePlaybackHost)는 이 타입을 직접 부르지 않는다.
public sealed class ScenePresentation : IPlaybackStage
{
    private readonly IEpisodeNodeRunner _nodeRunner;
    private readonly RollbackHistory _rollbackHistory;
    private readonly IVNLineAborter _linePresentationAborter;
    private readonly MiniStage _presentationStage;
    private readonly PresentationScopeSession _presentationScope;
    private readonly EpisodeSkipController _episodeSkipController;
    private readonly VNLinePresentationState _lineState;
    private readonly YarnPlaybackDriver _playbackDriver;
    private readonly LifecycleProbe _probe;

    // 이번 재생 구간에 정지가 걸렸나. 정지 없이 노드가 돌아오면 끝까지 간 것이다.
    private bool _stopRequested;
    private string _currentNodeName;

    public ScenePresentation(
        IEpisodeNodeRunner nodeRunner,
        RollbackHistory rollbackHistory,
        IVNLineAborter linePresentationAborter,
        MiniStage presentationStage,
        PresentationScopeSession presentationScope,
        EpisodeSkipController episodeSkipController,
        VNLinePresentationState lineState,
        YarnPlaybackDriver playbackDriver,
        LifecycleProbe probe)
    {
        _nodeRunner = nodeRunner;
        _rollbackHistory = rollbackHistory;
        _linePresentationAborter = linePresentationAborter;
        _presentationStage = presentationStage;
        _presentationScope = presentationScope;
        _episodeSkipController = episodeSkipController;
        _lineState = lineState;
        _playbackDriver = playbackDriver;
        _probe = probe;
    }

    public void BeginScene()
    {
        _stopRequested = false;

        // 앞 단위가 남긴 명령은 이 단위의 것이 아니다(정지가 이미 버렸어야 한다 — 빠진 길이 있어도 새지 않게).
        DiscardPendingCommands(warn: false);

        // 앞 단위가 시크 도중에 끝났다면 그 표적은 이 단위의 것이 아니다.
        _lineState.ClearSeek();

        _presentationStage.Clear();
        _presentationScope.Start();

        _probe?.OnRunStarted(_presentationScope.CurrentScope, "open"); // [샘플]
    }

    public async Task PlayEpisodeAsync(string nodeName)
    {
        _currentNodeName = nodeName;

        try
        {
            await _nodeRunner.RunNodeAsync(nodeName);

            if (!_stopRequested)
            {
                DiscardPendingCommands(warn: true);
                _probe?.OnNodeCompleted(); // [샘플]
            }
        }
        finally
        {
            // 현재 Yarn node의 수명이 끝났으므로
            // one-shot Episode Skip도 여기서 끝난다.
            _episodeSkipController.Reset();
        }
    }

    public void PrepareReplay()
    {
        // 같은 단위를 루트부터 다시 달린다 — 새 재생 구간이다. 남은 명령은 앞선 정지가 버렸다.
        _stopRequested = false;

        _presentationStage.Clear();
        _presentationScope.Start();

        _probe?.OnRunStarted(_presentationScope.CurrentScope, "replay from the root"); // [샘플]
    }

    public async Task StopAsync()
    {
        _stopRequested = true;
        _episodeSkipController.Reset();

        if (_nodeRunner.IsRunning)
            await _nodeRunner.StopAsync();

        // Yarn이 멈춘 뒤라 더 쌓이지 않는다.
        DiscardPendingCommands(warn: false);

        _linePresentationAborter.AbortCurrentVNLine();
        _rollbackHistory.ClearRollbackPoints();

        bool hadScope = _presentationScope.IsRunning;
        _presentationScope.End();

        if (hadScope)
            _probe?.OnRunEnded(); // [샘플]
    }

    private void DiscardPendingCommands(bool warn)
    {
        if (_playbackDriver == null || !_playbackDriver.TryDiscardPending(out int count, out string summary))
            return;

        string node = string.IsNullOrEmpty(_currentNodeName) ? "(노드 없음)" : $"'{_currentNodeName}'";

        if (warn)
        {
            string message =
                $"[연출 명령] {node} 재생이 끝났는데 다음 줄을 기다리던 명령 {count}개가 남아 실행하지 않고 버린다: {summary}. " +
                "명령은 다음 줄의 앞머리로 실행된다 — 노드를 줄로 끝낸다(연출만이면 #boxhide 줄).";

            Debug.LogWarning(message);
            _probe?.Note($"node ended with {count} command(s) after its last line - discarded, never run: {summary}"); // [샘플]
            return;
        }

        Debug.Log($"[연출 명령] {node} 정지 — 다음 줄을 기다리던 명령 {count}개를 버렸다: {summary}.");
        _probe?.Note($"stop - discarded {count} collected command(s): {summary}"); // [샘플]
    }
}
