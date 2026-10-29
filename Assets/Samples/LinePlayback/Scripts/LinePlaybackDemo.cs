using UnityEngine;

// Line Playback Demo의 호스트 — NodePlaybackHost + PlaybackUnit + ScenePresentation의 역할을 한 곳에 모았다.
//
// 조립(Awake):
//   대본 ─ MiniDialogueRunner ─ MiniCommandBridge ─ MiniPlaybackDriver ─ CommandExecutor.PlaySpecs
//                         └──── MiniLinePresenter(줄 진입 = 스텝 경계 · 시크 · 타자기)
//   PresentationScopeSession(executor, MiniPacePolicy)  ← 빨리 감기 · 시크 = 압축, 속도 모드 = 시간 배율
//
// 세션 하나 = 런 하나. 런의 수명은 여기서만 열고 닫는다:
//   다시 시작  정지 → 무대 비우기 → 새 세션 → 노드를 루트부터
//   롤백      표적(앞 줄)을 정하고 시크를 건다 → 정지 → 새 세션 → 루트부터, 표적까지 압축으로 지나간다
//   취소      정지만 한다. 무대는 멈춘 자리에 남는다
//   노드 끝   마지막 줄도 스텝 경계를 맞는다. 세션은 열어 둔다(런 수명 명령이 계속 돈다)
[DisallowMultipleComponent]
public sealed partial class LinePlaybackDemo : MonoBehaviour
{
    private enum RunStatus { Idle, Running, NodeComplete, Cancelled }

    private CommandExecutor _executor;
    private PresentationScopeSession _session;

    private MiniTrace _trace;
    private MiniStage _stage;
    private MiniLifecycleAudit _audit;
    private MiniLineState _line;
    private MiniRollbackHistory _history;
    private MiniPlaybackModes _modes;
    private MiniTypewriter _typewriter;

    private MiniDialogueRunner _runner;
    private MiniLinePresenter _presenter;
    private MiniPlaybackDriver _driver;
    private MiniPlaybackControls _controls;

    private int _sessionCount;
    private RunStatus _status = RunStatus.Idle;

    private void Awake()
    {
        if (!TryGetComponent(out _executor))
            _executor = gameObject.AddComponent<CommandExecutor>();

        _trace = new MiniTrace();
        _stage = new MiniStage(_trace);
        _audit = new MiniLifecycleAudit(new MiniStageModel(_stage), _trace);
        _line = new MiniLineState();
        _history = new MiniRollbackHistory();
        _modes = new MiniPlaybackModes();
        _typewriter = new MiniTypewriter();

        _executor.Initialize(new CompositeCommandFactory(new MiniCommandFactory(_stage, _trace)));
        _session = new PresentationScopeSession(_executor, new MiniPacePolicy(_modes, _line));

        _driver = new MiniPlaybackDriver(_executor, _session, _stage, _trace, _audit);
        _presenter = new MiniLinePresenter(_line, _history, _driver, _typewriter, _modes, _trace);
        _runner = new MiniDialogueRunner(this, MiniScriptParser.Parse(DemoScript.Source), _presenter);
        _ = new MiniCommandBridge(_runner, _driver);
        _controls = new MiniPlaybackControls(_modes, _line, _runner, _session, _trace);

        _runner.NodeCompleted += OnNodeCompleted;
    }

    private void Start() => Restart();

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            _controls.Advance();

        // 누르는 동안만. 롤백이 내린 뒤에는 다시 눌러야 켜진다.
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl))
            _modes.FastForwardHeld = true;

        if (Input.GetKeyUp(KeyCode.LeftControl) || Input.GetKeyUp(KeyCode.RightControl))
            _modes.FastForwardHeld = false;

        if (Input.GetKeyDown(KeyCode.R))
            Rollback();

        _controls.Tick();
    }

    private void OnDestroy()
    {
        if (_runner != null)
            _runner.NodeCompleted -= OnNodeCompleted;

        // 실행기가 먼저 파괴됐으면 그쪽 코루틴도 이미 없다.
        if (_executor != null && _session != null && _session.IsRunning)
            _session.End();
    }

    // ───────────────────────────── 런 수명 ─────────────────────────────

    private void Restart()
    {
        StopRun("restart");

        _line.ClearSeek();
        _modes.DisableAll();
        _audit.ResetAll();
        _stage.ResetCounters();

        OpenRun("from the root");
    }

    // 한 걸음 되감기 — 루트부터 다시 달려 앞 줄까지 시크한다(무대를 되돌리는 것이 아니라 다시 만든다).
    private void Rollback()
    {
        if (!_session.IsRunning || _line.IsSeeking)
        {
            _trace.Write("rollback refused - " + (_line.IsSeeking ? "seek active" : "no session"));
            return;
        }

        if (!_history.TryGetOneStepBack(out MiniLineKey target))
        {
            _trace.Write("rollback refused - nothing before this line");
            return;
        }

        _line.BeginRollbackSeek(target);
        _modes.DisableAll();

        StopRun($"rollback to {target.Id}");
        OpenRun($"replay, seek to {target.Id}");
    }

    private void Cancel()
    {
        if (!_session.IsRunning)
            return;

        StopRun("cancel");

        _line.ClearSeek();
        _modes.DisableAll();
        _status = RunStatus.Cancelled;
    }

    private void OpenRun(string reason)
    {
        _stage.Clear();
        _history.Clear();
        _typewriter.Clear();

        _session.Start();
        _sessionCount++;

        _stage.ActiveScope = _session.CurrentScope;
        _stage.CurrentStep = 0;
        _trace.BeginRun(_sessionCount, reason);
        _audit.BeginRun();

        _status = RunStatus.Running;
        _runner.StartNode();
    }

    // ScenePresentation.StopAsync와 같은 순서 — Yarn 정지 → 쌓인 명령 버리기 → 줄 무효화 → 세션 끝(Stop(Cancel)).
    private void StopRun(string reason)
    {
        if (!_session.IsRunning)
            return;

        _runner.Stop();

        if (_driver.TryDiscardPending(out int count, out string summary))
            _trace.Write($"discarded {count} collected command(s) that never got a line: {summary}");

        _presenter.AbortCurrentLine();
        _session.End();
        _trace.CancelUnfinished(reason);

        // 이 뒤로 이전 스코프로 들어오는 쓰기는 전부 stale write다.
        _stage.ActiveScope = null;
    }

    private void OnNodeCompleted()
    {
        _driver.SettleFinal();

        if (_driver.TryDiscardPending(out int count, out string summary))
            _trace.Write($"node ended - {count} command(s) after the last line were discarded, never run: {summary}");

        _modes.SkipActive = false;
        _status = RunStatus.NodeComplete;
        _trace.Write("node complete - session stays open, run-scoped commands keep running");
    }
}
