public interface ICommandRunScopeProvider
{
    CommandRunScope CurrentScope { get; }
}

// 재생 흐름(Playback)이 연출 세션으로부터 빌려 쓰는 것은
// "CommandRunScope를 만들고(Start), 멈추고 정리하는(End)" 생애주기뿐이다.
public sealed class PresentationScopeSession : ICommandRunScopeProvider
{
    private readonly ICommandPacePolicy _pace;

    private readonly CommandExecutor _executor;

    private CommandRunScope _sessionScope;
    public CommandRunScope CurrentScope => _sessionScope;

    public bool IsRunning => _sessionScope != null;

    public PresentationScopeSession(
        CommandExecutor executor,
        ICommandPacePolicy pace)
    {
        _executor = executor;
        _pace = pace;
    }

    public void Start()
    {
        _sessionScope = new CommandRunScope(_pace);
    }

    public void End()
    {
        _executor.Stop(CleanupPolicy.Cancel);

        _sessionScope = null;
    }
}