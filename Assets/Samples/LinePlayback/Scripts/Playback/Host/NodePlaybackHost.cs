using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 연출 단독 예제의 재생 호스트. 재생 단위는 Yarn 노드 하나다.
///
/// 진행·저장을 모른다. 노드 하나를 처음부터 끝까지 돌리고, 리플레이 요청이 오면
/// 같은 노드를 루트부터 다시 시작한다. 표적 라인까지는 시크가 데려간다.
/// 노드 안의 jump·detour는 Yarn이 처리한다 — 이 호스트는 시작 노드 이름만 안다.
///
/// 단위의 수명(열기·정지·다시 달리기)은 PlaybackUnit이 갖는다. 이 호스트가 정하는 것은
/// "무엇을 재생할지(노드 이름)"와 "끝까지 간 노드는 마지막 화면을 남긴다"는 것뿐이다.
///
/// 이어하기도 따로 모른다. 연출 기억을 되돌려 두면(PlaybackMemory.Restore) 단위가 열릴 때
/// 재개 시크가 걸린다 — 이 호스트는 평소처럼 노드를 재생할 뿐이다.
///
/// 한 실행에 호스트는 하나다(PresentationRuntime.AttachHost).
/// </summary>
public sealed class NodePlaybackHost : IPlaybackHost
{
    private readonly PlaybackUnit _unit;

    private CancellationTokenSource _exit;
    private bool _replayRequested;

    public NodePlaybackHost(PlaybackUnit unit)
    {
        _unit = unit;
    }

    public bool IsRunning { get; private set; }

    private bool ExitRequested => _exit != null && _exit.IsCancellationRequested;

    /// <summary>
    /// 노드 하나를 재생한다. 노드 끝까지 갔으면 true, 끝내기 요청으로 끊겼거나 시작하지 못했으면 false.
    /// </summary>
    public async Task<bool> PlayAsync(string nodeName)
    {
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            Debug.LogError("[노드 재생] 재생할 노드 이름이 비어 있다.");
            return false;
        }

        if (IsRunning)
        {
            Debug.LogWarning($"[노드 재생] 이미 재생 중이라 '{nodeName}' 요청을 거부한다.");
            return false;
        }

        IsRunning = true;
        _replayRequested = false;
        _exit = new CancellationTokenSource();

        bool finished = false;
        bool completed = false;

        try
        {
            await _unit.OpenAsync(_exit.Token);

            while (true)
            {
                _replayRequested = false;

                await _unit.PlayAsync(nodeName);

                if (ExitRequested || !_replayRequested)
                    break;

                // 노드 하나에는 되돌릴 진행 기록이 없으므로 롤백 표적 색인은 쓰지 않는다.
                // (진행 호스트는 여기서 그 색인 뒤의 진행 선택을 자른다.)
                await _unit.RestartFromRootAsync(_exit.Token);
            }

            finished = true;
            completed = !ExitRequested;

            if (completed)
                Debug.Log($"[노드 재생] '{nodeName}' 끝.");
        }
        catch (OperationCanceledException) when (ExitRequested)
        {
            // 열거나 다시 달리기 직전에 끝내기가 왔다.
        }
        finally
        {
            // 끝까지 간 노드는 마지막 화면을 그대로 둔다 — 진행 층이 챕터 끝에서 하는 것과 같다.
            // 끝내기 요청이나 예외로 끊긴 경우만 멈춤을 마무리한다.
            if (!finished || ExitRequested)
                await _unit.StopAsync();

            _exit.Dispose();
            _exit = null;
            IsRunning = false;
        }

        return completed;
    }

    public Task RequestReplayAsync()
    {
        if (!IsRunning || _replayRequested || ExitRequested)
            return Task.CompletedTask;

        _replayRequested = true;

        return _unit.StopAsync();
    }

    public Task ExitAsync()
    {
        if (!IsRunning)
            return Task.CompletedTask;

        _exit.Cancel();

        return _unit.StopAsync();
    }
}
