using System.Collections.Generic;
using System.Threading;
using MiniYarn;

// ked-presentation-runtime의 CustomLinePresenter를 옮긴 것.
// 바뀐 곳: Yarn.Unity → MiniYarn, 타자기 액션 마크업 핸들러 없음, 기본 LinePresenter 앞에 끼우는 대신 목록 끝에 넣는다.
public interface IVNLineAborter
{
    void AbortCurrentVNLine();
}

public sealed class CustomLinePresenter : DialoguePresenterBase, IVNLineAborter
{
    private VNLinePresentationFlow _vnLinePresentationFlow;

    private DemoTypewriter _typewriter;
    private VNPlaybackRuntimeState _vnPlaybackRuntimeState;

    private string _currentNodeName;

    private int _presenterGeneration;
    private CancellationTokenSource _presenterLifetimeCts = new();
    private CancellationTokenSource _lineVisualCts;

    // 진행 중인 RunLineAsync가 실제로 빠져나올 때까지 기다리기 위한 통로.
    // 이게 없으면 정지 이후에도 라인 태스크가 살아남아,
    // 다음 StartDialogue가 만든 새 dialogueCancellationSource를 보고
    // Dialogue.Continue()를 한 번 더 씀.
    private YarnTaskCompletionSource _lineDrain;

    public void Initialize(
        DialogueRunner dialogueRunner,
        VNLinePresentationFlow vnLinePresentationFlow,
        DemoTypewriter typewriter,
        VNPlaybackRuntimeState vnPlaybackRuntimeState)
    {
        dialogueRunner.onNodeStart?.AddListener(nodeName => _currentNodeName = nodeName);
        RegisterPresenter(dialogueRunner);

        _vnLinePresentationFlow = vnLinePresentationFlow;
        _typewriter = typewriter;
        _vnPlaybackRuntimeState = vnPlaybackRuntimeState;
    }

    public override async YarnTask RunLineAsync(
        LocalizedLine line,
        LineCancellationToken token)
    {
        var drain = new YarnTaskCompletionSource();
        _lineDrain = drain;

        try
        {
            var ctx = new VNLinePresentationContext
            {
                Line = line,
                Token = token,
                NodeName = _currentNodeName,
                ShouldUseImmediateTransition = _vnPlaybackRuntimeState.IsSpeedUpMode
            };

            await _vnLinePresentationFlow.RunAsync(
                ctx,
                beginRun: BeginLinePresentationRun,
                waitForAdvance: WaitForLineAdvanceAsync);
        }
        finally
        {
            drain.TrySetResult();

            if (ReferenceEquals(_lineDrain, drain))
                _lineDrain = null;
        }
    }

    private LinePresentationRun BeginLinePresentationRun()
    {
        CancelLineVisualToken();

        _lineVisualCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                _presenterLifetimeCts.Token);

        return new LinePresentationRun(
            _presenterGeneration,
            () => _presenterGeneration,
            _lineVisualCts.Token);
    }

    private async YarnTask WaitForLineAdvanceAsync(LineCancellationToken token)
    {
        CancellationTokenSource cts = null;

        try
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(
                token.NextContentToken,
                _presenterLifetimeCts.Token);

            await YarnTask
                .WaitUntilCanceled(cts.Token)
                .SuppressCancellationThrow();
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public void AbortCurrentVNLine()
    {
        _presenterGeneration++;
        CancelLineVisualToken();
    }

    public override YarnTask OnDialogueStartedAsync()
    {
        return YarnTask.CompletedTask;
    }

    // DialogueRunner는 Stop()을 끝내기 전에 이 태스크를 기다린 뒤, Task를 닫는다.
    // 순서: 먼저 취소해서 대기를 풀고, 그 다음 Task가 닫히길 기다림..
    public override async YarnTask OnDialogueCompleteAsync()
    {
        CancelLineVisualToken();
        CancelPresenterLifetimeWaiters();

        YarnTaskCompletionSource drain = _lineDrain;

        if (drain != null)
        {
            await drain.Task;

            // 스택 탈출용.
            // drain은 RunLineAsync의 finally에서 닫혀 그 메서드의 SetResult()보다 앞섬.
            // (1) Stop() 반환과 StartDialogue()가 이 스택 안에서 실행,
            // (2) 새 dialogueCancellationSource가 먼저 설치,
            // (3) 뒤늦게 전파된 OnLineReceivedAsync가 그 소스를 보고 Continue() 발사,
            // (4) 새 대화의 라인 하나가 소비됨.
            // 양보하면 그 Continue()는 취소/null 소스를 보고 무시된다.
            await YarnTask.Yield();
        }
    }

    private void CancelPresenterLifetimeWaiters()
    {
        _presenterLifetimeCts?.Cancel();
        _presenterLifetimeCts?.Dispose();
        _presenterLifetimeCts = new CancellationTokenSource();
    }

    private void CancelLineVisualToken()
    {
        if (_lineVisualCts == null)
            return;

        _lineVisualCts.Cancel();
        _lineVisualCts.Dispose();
        _lineVisualCts = null;
    }

    private void RegisterPresenter(DialogueRunner dialogueRunner)
    {
        var presenters =
            new List<DialoguePresenterBase>(dialogueRunner.DialoguePresenters);

        presenters.Remove(this);
        presenters.Add(this);

        dialogueRunner.DialoguePresenters = presenters;
    }

    private void OnDestroy()
    {
        CancelLineVisualToken();

        _presenterLifetimeCts?.Cancel();
        _presenterLifetimeCts?.Dispose();
        _presenterLifetimeCts = null;
    }
}
