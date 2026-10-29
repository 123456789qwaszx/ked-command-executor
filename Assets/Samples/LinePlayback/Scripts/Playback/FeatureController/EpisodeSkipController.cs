// 현재 Episode의 Yarn node를 끝까지 빠르게 진행하는 one-shot controller.
//
// RapidSkip과 차이:
// - RapidSkip: 입력을 누르고 있는 동안 계속.
// - EpisodeSkip: 한 번 요청하면 현재 node가 끝날 때까지 계속.
// - node 종료는 ScenePlaybackSession이 알려준다.
//
// Yarn 선택지는 이 controller가 선택하지 않는다.
// 선택이 끝나고 line 재생이 재개되면 다시 자동 진행한다.
public sealed class EpisodeSkipController
{
    private DialogueAdvanceDispatcher _dispatcher;

    public bool IsActive { get; private set; }

    public void Initialize(DialogueAdvanceDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public bool Request()
    {
        if (IsActive)
            return true;

        if (!_dispatcher.IsDialogueRunning)
            return false;
        
        IsActive = true;

        return true;
    }

    public void Tick()
    {
        if (!IsActive)
            return;

        _dispatcher.DispatchRapidSkipAdvance();
    }

    public void Reset()
    {
        IsActive = false;
    }
}
