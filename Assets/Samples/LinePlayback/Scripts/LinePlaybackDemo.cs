using MiniYarn;
using UnityEngine;

// Line Playback Demo의 조립 — ked-presentation-runtime의 PresentationAssembler + PresentationRuntime.AttachHost 자리.
// 조립 순서와 연결은 원본을 그대로 따른다. 다른 것은 무대(MiniStage) · 대본(MiniYarn) · 화면(IMGUI)뿐이다.
//
//   DemoScript ─ DialogueRunner(MiniYarn) ─┬─ YarnCommandBridge ─ YarnPlaybackDriver(쌓기)
//                                          └─ CustomLinePresenter ─ VNLinePresentationFlow ─ PlayCollected → CommandExecutor.PlaySpecs
//   PresentationScopeSession(executor, PresentationSessionContext)   ← 빨리 넘기기 · 시크 = 압축, 속도 모드 = 시간 배율
//   NodePlaybackHost ─ PlaybackUnit ─ ScenePresentation             ← 런 수명(열기 · 정지 · 루트부터 다시)
//   VNAdvanceInputPoller ─ DialogueAdvanceDispatcher(AdvanceGate) · VNFeatureController(롤백 · 스킵 · 오토 · 속도)
[DisallowMultipleComponent]
public sealed partial class LinePlaybackDemo : MonoBehaviour
{
    [Header("Script")]
    [SerializeField] private string startNode = DemoScript.NodeName;

    [Header("Playback")]
    [SerializeField] private VNPlaybackSettings playbackSettings = new();

    [Header("View")]
    [Tooltip("대사 글꼴. 비우면 IMGUI 기본 글꼴(OS 글꼴로 대체)을 쓴다. 한글이 네모로 보이면 한글 글꼴을 넣는다.")]
    [SerializeField] private Font dialogueFont;

    private CommandExecutor _executor;
    private PresentationScopeSession _scopeSession;

    private MiniTrace _trace;
    private MiniStage _stage;
    private MiniLifecycleAudit _audit;
    private LifecycleProbe _probe;

    private VNPlaybackRuntimeState _playbackState;
    private VNLinePresentationState _lineState;
    private RollbackHistory _rollbackHistory;
    private BacklogRecorder _backlog;

    private DialogueRunner _runner;
    private ImguiDialogueBoxView _boxView;
    private VNLinePresentationFlow _lineFlow;

    private DialogueAdvanceDispatcher _dispatcher;
    private AdvanceGate _gate;
    private VNFeatureController _features;
    private NodePlaybackHost _host;

    private int _playRequest;
    private string _status = "idle";

    private void Awake()
    {
        PresentationClock.UnscaledTimeScale = 1f;

        if (!TryGetComponent(out _executor))
            _executor = gameObject.AddComponent<CommandExecutor>();

        // 샘플 무대 · 장부 · 감사
        _trace = new MiniTrace();
        _stage = new MiniStage(_trace);
        _audit = new MiniLifecycleAudit(new MiniStageModel(_stage), _trace);

        _playbackState = new VNPlaybackRuntimeState { PlaybackSettings = playbackSettings };
        _lineState = new VNLinePresentationState();
        _rollbackHistory = new RollbackHistory();
        _backlog = new BacklogRecorder();
        _dispatcher = new DialogueAdvanceDispatcher();

        var episodeSkip = new EpisodeSkipController();
        _probe = new LifecycleProbe(_stage, _trace, _audit, _rollbackHistory);

        // ── BootstrapPresentationSession ──
        _executor.Initialize(new CompositeCommandFactory(new MiniCommandFactory(_stage, _trace)));
        _scopeSession = new PresentationScopeSession(
            _executor,
            new PresentationSessionContext(_playbackState, _lineState));

        // ── BootstrapYarn ──
        _runner = gameObject.AddComponent<DialogueRunner>();
        _runner.SetContents(DemoScript.Contents);

        var playbackDriver = new YarnPlaybackDriver(_executor, _scopeSession, _probe);

        _boxView = new ImguiDialogueBoxView();
        var boxPresentation = new DialogueBoxPresentationController(new DialogueBoxCurrentState(), _boxView);

        _ = new YarnCommandBridge(_runner, playbackDriver, new MiniStageCommandParser());

        var lineBoundary = new VNYarnLineBoundary(_backlog, _rollbackHistory, _lineState);
        var typewriter = new DemoTypewriter();

        _lineFlow = new VNLinePresentationFlow(
            lineBoundary,
            _lineState,
            boxPresentation,
            typewriter,
            playbackDriver,
            new LineHurrySpeedController(typewriter));

        var linePresenter = gameObject.AddComponent<CustomLinePresenter>();
        linePresenter.Initialize(_runner, _lineFlow, typewriter, _playbackState);

        // ── CreateScenePresentation ──
        var scenePresentation = new ScenePresentation(
            new YarnEpisodeNodeRunner(_runner),
            _rollbackHistory,
            linePresenter,
            _stage,
            _scopeSession,
            episodeSkip,
            _lineState,
            playbackDriver,
            _probe);

        // ── BootstrapPlaybackControls ──
        var autoAdvance = new AutoAdvanceScheduler();
        autoAdvance.Initialize(_playbackState, _dispatcher, () => Time.unscaledTimeAsDouble);
        typewriter.Handlers.Add(autoAdvance);

        var rapidSkip = new RapidSkipController(_dispatcher);
        episodeSkip.Initialize(_dispatcher);

        _features = new VNFeatureController(
            _playbackState,
            _lineState,
            typewriter,
            _backlog,
            autoAdvance,
            rapidSkip,
            episodeSkip,
            _rollbackHistory);

        _gate = new AdvanceGate(_playbackState, _lineState, _scopeSession);
        _gate.Rejected += OnAdvanceRejected;
        _dispatcher.Initialize(_gate, _runner, _lineState);

        // ── PresentationRuntime.AttachHost ──
        _host = new NodePlaybackHost(new PlaybackUnit(scenePresentation, _backlog, _rollbackHistory));

        var inputPoller = gameObject.AddComponent<VNAdvanceInputPoller>();
        inputPoller.Initialize(_dispatcher, _features, _host);
    }

    private void Start() => PlayNode();

    private void Update()
    {
        // VNAdvanceInputPoller가 Space · Ctrl · S · A · R을 읽는다. 원본에서 메뉴가 하는 둘만 여기서.
        if (Input.GetKeyDown(KeyCode.K))
            RequestSkip();

        if (Input.GetKeyDown(KeyCode.Escape))
            Exit();

        _probe.Tick();
    }

    private void OnDestroy()
    {
        if (_gate != null)
            _gate.Rejected -= OnAdvanceRejected;
    }

    // ───────────────────────────── 재생 호스트 ─────────────────────────────

    private async void PlayNode()
    {
        int request = ++_playRequest;
        _status = "playing";

        bool completed = await _host.PlayAsync(startNode);

        if (request == _playRequest)
            _status = completed ? "node complete" : "exited";
    }

    // 원본 VNAdvanceInputPoller.PollFeatureToggles의 롤백과 같은 순서 — 표적을 정한 뒤 호스트가 루트부터 다시 돈다.
    private async void RequestRollback()
    {
        if (!_host.IsRunning)
            return;

        if (!_features.RequestRollbackOneStep())
        {
            _trace.Write("rollback refused - no earlier line, or a seek is already running");
            return;
        }

        _trace.Write("rollback - target picked, replaying from the root");
        await _host.RequestReplayAsync();
    }

    private async void RequestBacklogJump(DialogueLogEntry entry)
    {
        if (!_host.IsRunning || !_features.RequestBacklogJump(entry))
            return;

        _trace.Write($"backlog jump - replaying from the root to {entry.lineId}");
        await _host.RequestReplayAsync();
    }

    private void RequestSkip()
    {
        _trace.Write(_features.RequestSkipCurrentEpisode()
            ? "episode skip - advancing every line until the node ends (step boundaries, no compression)"
            : "episode skip refused - seek active or node not running");
    }

    private async void Exit()
    {
        if (!_host.IsRunning)
            return;

        _trace.Write("exit - NodePlaybackHost.ExitAsync");
        await _host.ExitAsync();
    }

    private bool _restarting;

    private async void Restart()
    {
        if (_restarting)
            return;

        _restarting = true;

        try
        {
            if (_host.IsRunning)
                await _host.ExitAsync();

            while (_host.IsRunning)
                await YarnTask.Yield();

            _audit.ResetAll();
            _stage.ResetCounters();
            PlayNode();
        }
        finally
        {
            _restarting = false;
        }
    }

    private void OnAdvanceRejected(AdvanceRequestKind kind, string reason)
    {
        // 사람의 요청만 적는다(자동 · 빨리 넘기기는 매 프레임 두드린다).
        if (kind == AdvanceRequestKind.User && (reason == "cps_node_busy" || reason == "seek_active"))
            _trace.Write($"advance refused - {reason}");
    }
}
