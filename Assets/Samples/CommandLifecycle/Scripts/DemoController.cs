using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Command Lifecycle Demo의 진입점.
//
// 실행기를 쓰는 바깥(재생)의 자리에 선다. 하는 일은 README "어떻게 쓰나"의 세 줄이 전부다:
//   executor.Initialize(factory)
//   session = new PresentationScopeSession(executor, pace); session.Start()
//   executor.PlaySpecs(specs, session.CurrentScope)
// 나머지는 버튼과 화면이다. 커맨드가 무엇을 하는지는 여기서도 모른다 — 스펙만 만든다.
[DisallowMultipleComponent]
public sealed class DemoController : MonoBehaviour
{
    private const float RefWidth = 1280f;
    private const float RefHeight = 720f;

    private CommandExecutor _executor;
    private PresentationScopeSession _session;
    private readonly DemoPacePolicy _pace = new();
    private readonly DemoTrace _trace = new();
    private DemoStage _stage;

    private Coroutine _scenarioRoutine;
    private int _sessionCount;
    private string _note = Notes.Intro;

    private void Awake()
    {
        EnsureCamera();

        if (!TryGetComponent(out _executor))
            _executor = gameObject.AddComponent<CommandExecutor>();

        _stage = new DemoStage(transform);

        _executor.Initialize(new CompositeCommandFactory(new DemoCommandFactory(_stage, _trace)));
        _session = new PresentationScopeSession(_executor, _pace);
    }

    // Play를 누르면 바로 한 번 돈다.
    private void Start() => PlaySequential();

    private void OnDestroy()
    {
        // 실행기가 먼저 파괴됐으면 그쪽 코루틴도 이미 없다.
        if (_executor != null && _session != null && _session.IsRunning)
            _session.End();
    }

    // ───────────────────────────── 시나리오 ─────────────────────────────

    // ① 대기 커맨드: SequencePlayer가 A를 끝까지 기다린 뒤에야 B에 진입한다.
    private void PlaySequential()
    {
        BeginRun("Sequential", Notes.Sequential);
        PlayStep(
            Move("A", DemoStage.MoveTargetX, 1f, wait: true),
            Fade("B", 0f, 0.8f, wait: true));
    }

    // ② 비대기 커맨드: 둘 다 한 번씩 진입만 시키고 넘어간다. 배경 루틴이 되어 스텝 수명에 묶인다.
    private void PlayParallel()
    {
        BeginRun("Parallel", Notes.Parallel);
        PlayStep(
            Move("A", DemoStage.MoveTargetX, 1f, wait: false),
            Fade("B", 0f, 0.8f, wait: false));
    }

    // ③ 스텝 수명: 5초짜리 배경 이동이 1초 만에 다음 스텝을 만난다 → CleanupStep(Finish).
    private void PlayStepCleanup()
    {
        BeginRun("Step Cleanup", Notes.StepCleanup);
        PlayStep(Move("A", DemoStage.MoveTargetX, 5f, wait: false));

        _scenarioRoutine = StartCoroutine(AdvanceStepAfter(1f, Fade("B", 0f, 0.8f, wait: true)));
    }

    private IEnumerator AdvanceStepAfter(float seconds, params CommandSpecBase[] nextStep)
    {
        yield return new WaitForSeconds(_session.CurrentScope.ScalePresentationDuration(seconds));

        // PlaySpecs는 앞 메인 루틴을 멈추지 않는다(배경 루틴만 스텝 수명으로 정리한다).
        // 대기 커맨드가 아직 돌고 있으면 두 메인 루틴이 겹치므로, 이 데모는 그것이 끝난 뒤에 넘어간다.
        while (_session.CurrentScope.CommandIsPlaying)
            yield return null;

        _scenarioRoutine = null;
        PlayStep(nextStep);
    }

    // ④-a 압축: 다음에 진입하는 커맨드부터 OnSkip으로 최종 상태가 된다.
    private void ToggleCompress()
    {
        _pace.CompressExecution = !_pace.CompressExecution;
        _note = _pace.CompressExecution ? Notes.CompressOn : Notes.CompressOff;
    }

    // ④-b 취소: 완료가 아니라 중단이다. 커맨드에는 어떤 훅도 불리지 않는다.
    private void Cancel()
    {
        if (!_session.IsRunning)
            return;

        StopScenarioRoutine();
        _session.End();
        _trace.CancelUnfinished();
        _note = Notes.Cancel;
    }

    private void ResetDemo()
    {
        StopScenarioRoutine();

        if (_session.IsRunning)
            _session.End();

        _stage.ResetAll();
        _trace.Clear();
        _note = Notes.Intro;
    }

    // 시나리오마다 새 세션을 연다. 취소된 CommandRunScope는 IsCancelled가 남아 다시 쓸 수 없고,
    // 이전 시나리오의 배경 루틴이 새 시나리오의 시작 상태를 건드리지 않게 하려는 것도 있다.
    private void BeginRun(string name, string note)
    {
        StopScenarioRoutine();

        if (_session.IsRunning)
            _session.End();

        _stage.ResetAll();
        _trace.BeginRun(name);
        _note = note;

        _session.Start();
        _sessionCount++;
    }

    private void PlayStep(params CommandSpecBase[] specs)
    {
        _trace.BeginStep();
        _executor.PlaySpecs(specs, _session.CurrentScope);
    }

    private void StopScenarioRoutine()
    {
        if (_scenarioRoutine != null)
            StopCoroutine(_scenarioRoutine);

        _scenarioRoutine = null;
    }

    private static DemoMoveSpec Move(string target, float x, float duration, bool wait)
        => new() { target = target, x = x, duration = duration, wait = wait };

    private static DemoFadeSpec Fade(string target, float alpha, float duration, bool wait)
        => new() { target = target, alpha = alpha, duration = duration, wait = wait };

    private static void EnsureCamera()
    {
        if (Camera.main != null)
            return;

        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        go.transform.position = new Vector3(0f, 0f, -10f);

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.09f, 0.10f, 0.12f);
    }

    // ───────────────────────────── 화면 (IMGUI) ─────────────────────────────

    private GUIStyle _title, _subtitle, _header, _text, _small, _wrap, _shapeLabel;
    private float _guiScale;
    private Vector2 _guiOffset;

    private static readonly Color Dim = new(0.55f, 0.58f, 0.64f);
    private static readonly Color Accent = new(0.98f, 0.80f, 0.35f);
    private static readonly float[] Speeds = { 1f, 2f, 4f };

    private void OnGUI()
    {
        EnsureStyles();

        _guiScale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
        _guiOffset = new Vector2((Screen.width - RefWidth * _guiScale) * 0.5f, (Screen.height - RefHeight * _guiScale) * 0.5f);
        GUI.matrix = Matrix4x4.TRS(_guiOffset, Quaternion.identity, new Vector3(_guiScale, _guiScale, 1f));

        GUI.Label(new Rect(0f, 10f, RefWidth, 32f), "COMMAND EXECUTOR — LIFECYCLE DEMO", _title);
        GUI.Label(new Rect(0f, 40f, RefWidth, 20f),
            "The executor never knows what a command does. It owns order, waiting, lifetime, cancellation and compression.",
            _subtitle);

        DrawShapeLabels();
        DrawControls(new Rect(20f, 325f, 420f, 380f));
        DrawState(new Rect(460f, 325f, 800f, 380f));
    }

    private void DrawShapeLabels()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        foreach (KeyValuePair<string, DemoStage.Shape> pair in _stage.Shapes)
        {
            Vector3 sp = cam.WorldToScreenPoint(pair.Value.Transform.position);
            var gui = new Vector2((sp.x - _guiOffset.x) / _guiScale, (Screen.height - sp.y - _guiOffset.y) / _guiScale);
            GUI.Label(new Rect(gui.x - 20f, gui.y - 12f, 40f, 24f), pair.Key, _shapeLabel);
        }

        Vector3 area = cam.WorldToScreenPoint(new Vector3(0f, DemoStage.AreaY - 1.45f, 0f));
        float y = (Screen.height - area.y - _guiOffset.y) / _guiScale;
        GUI.Label(new Rect(0f, y - 10f, RefWidth, 20f), "Command Execution Area", _subtitle);
    }

    private void DrawControls(Rect r)
    {
        float x = r.x, y = r.y;
        const float w = 134f, h = 34f, gap = 9f;

        GUI.Label(new Rect(x, y, r.width, 20f), "SCENARIO", _header);
        y += 24f;
        if (Button(new Rect(x, y, w, h), "Sequential", _trace.RunName == "Sequential")) PlaySequential();
        if (Button(new Rect(x + (w + gap), y, w, h), "Parallel", _trace.RunName == "Parallel")) PlayParallel();
        if (Button(new Rect(x + (w + gap) * 2f, y, w, h), "Step Cleanup", _trace.RunName == "Step Cleanup")) PlayStepCleanup();

        y += h + 14f;
        GUI.Label(new Rect(x, y, r.width, 20f), "CONTROL", _header);
        y += 24f;
        if (Button(new Rect(x, y, w, h), _pace.CompressExecution ? "Compress: ON" : "Compress: OFF", _pace.CompressExecution)) ToggleCompress();
        GUI.enabled = _session.IsRunning;
        if (Button(new Rect(x + (w + gap), y, w, h), "Cancel", false)) Cancel();
        GUI.enabled = true;
        if (Button(new Rect(x + (w + gap) * 2f, y, w, h), "Reset", false)) ResetDemo();

        y += h + 14f;
        GUI.Label(new Rect(x, y, r.width, 20f), "SPEED  (ICommandPacePolicy.TimeScale)", _header);
        y += 24f;
        for (int i = 0; i < Speeds.Length; i++)
        {
            float s = Speeds[i];
            if (Button(new Rect(x + (w + gap) * i, y, w, h), $"{s:0}x", Mathf.Approximately(_pace.TimeScale, s)))
            {
                _pace.TimeScale = s;
                _note = Notes.Speed;
            }
        }

        y += h + 14f;
        GUI.Label(new Rect(x, y, r.width, r.yMax - y), _note, _wrap);
    }

    private void DrawState(Rect r)
    {
        float x = r.x, y = r.y;

        GUI.Label(new Rect(x, y, r.width, 20f), "CURRENT COMMAND", _header);
        y += 22f;

        int shown = 0;
        foreach (DemoTrace.Entry e in _trace.Entries)
        {
            if (!e.IsRunning || shown >= 3)
                continue;

            float progress = e.PlannedDuration > 0f ? Mathf.Clamp01((_trace.Now - e.StartTime) / e.PlannedDuration) : 1f;
            string where = e.Wait ? "waited by the main routine" : "background routine · StepLifetime";
            GUI.Label(new Rect(x, y, r.width, 20f), $"<b>{e.Label}</b>   {progress * 100f:0}%   <color=#8c93a3>{where}</color>", _text);
            y += 20f;
            shown++;
        }

        if (shown == 0)
        {
            GUI.Label(new Rect(x, y, r.width, 20f), "—", _text);
            y += 20f;
        }

        y = Mathf.Max(y, r.y + 86f);
        GUI.Label(new Rect(x, y, r.width, 20f), $"<color=#8c93a3>STATUS</color>   {StatusText()}", _text);
        y += 22f;
        GUI.Label(new Rect(x, y, r.width, 20f), $"<color=#8c93a3>SCOPE</color>    {ScopeText()}", _text);
        y += 32f;

        GUI.Label(new Rect(x, y, r.width, 20f), "TIMELINE", _header);
        y += 22f;
        y = DrawTimeline(new Rect(x, y, r.width, 120f));

        y += 8f;
        GUI.Label(new Rect(x, y, r.width, 20f), "EVENTS", _header);
        y += 20f;
        foreach (string line in _trace.Log)
        {
            GUI.Label(new Rect(x, y, r.width, 18f), line, _small);
            y += 16f;
        }
    }

    private string StatusText()
    {
        if (!_session.IsRunning)
            return _trace.WasCancelled ? "<color=#ff6b6b>Cancelled</color> — session ended" : "Idle";

        int background = _trace.CountRunning(wait: false);

        if (_session.CurrentScope.CommandIsPlaying)
            return "<color=#fbcc59>Running</color> — main routine is waiting on a command";

        if (background > 0 || _scenarioRoutine != null)
            return $"<color=#fbcc59>Running</color> — main routine done, {background} background routine(s) alive";

        return "<color=#6bd68a>Done</color> — nothing running";
    }

    private string ScopeText()
    {
        if (!_session.IsRunning)
            return "no CommandRunScope";

        return $"session #{_sessionCount} · step {_trace.CurrentStep} · " +
               $"compress {(_pace.CompressExecution ? "ON" : "OFF")} · ×{_pace.TimeScale:0}";
    }

    // TIME ──▶ 위에 커맨드마다 한 줄. 옅은 막대는 계획된 길이, 진한 막대는 실제로 돈 구간.
    private float DrawTimeline(Rect r)
    {
        const float labelW = 250f, stateW = 120f, rowH = 22f;
        float barX = r.x + labelW;
        float barW = r.width - labelW - stateW;

        float now = _trace.Now;
        float horizon = 3f;
        foreach (DemoTrace.Entry e in _trace.Entries)
        {
            if (e.StartTime >= 0f)
                horizon = Mathf.Max(horizon, e.StartTime + e.PlannedDuration, e.EndTime);
        }
        if (_session.IsRunning)
            horizon = Mathf.Max(horizon, now);
        horizon += 0.3f;

        float ToX(float t) => barX + barW * Mathf.Clamp01(t / horizon);

        float y = r.y;
        foreach (DemoTrace.Entry e in _trace.Entries)
        {
            string wait = e.Wait ? "wait" : "bg";
            GUI.Label(new Rect(r.x, y, labelW - 8f, rowH), $"<color=#8c93a3>S{e.Step}</color>  {e.Label}  <color=#8c93a3>{wait}</color>", _small);

            Fill(new Rect(barX, y + rowH * 0.5f - 0.5f, barW, 1f), new Color(1f, 1f, 1f, 0.06f));

            if (e.StartTime >= 0f)
            {
                Color c = StateColor(e.State);

                if (e.PlannedDuration > 0f)
                    Fill(Span(ToX(e.StartTime), ToX(e.StartTime + e.PlannedDuration), y, rowH), new Color(c.r, c.g, c.b, 0.18f));

                float end = e.EndTime >= 0f ? e.EndTime : now;
                Fill(Span(ToX(e.StartTime), Mathf.Max(ToX(end), ToX(e.StartTime) + 3f), y, rowH), c);
            }

            GUI.Label(new Rect(barX + barW + 10f, y, stateW - 10f, rowH), $"<color=#{ColorUtility.ToHtmlStringRGB(StateColor(e.State))}>{e.State}</color>", _small);
            y += rowH;
        }

        float rowsBottom = y;

        // 스텝 경계와 현재 시각.
        for (int i = 0; i < _trace.StepTimes.Count; i++)
        {
            float sx = ToX(_trace.StepTimes[i]);
            Fill(new Rect(sx, r.y - 2f, 1f, rowsBottom - r.y + 6f), new Color(1f, 1f, 1f, 0.35f));
            GUI.Label(new Rect(sx + 3f, rowsBottom + 2f, 40f, 16f), $"S{i + 1}", _small);
        }

        if (_session.IsRunning)
            Fill(new Rect(ToX(now), r.y - 2f, 1f, rowsBottom - r.y + 6f), Accent);

        // 축.
        Fill(new Rect(barX, rowsBottom + 2f, barW, 1f), new Color(1f, 1f, 1f, 0.2f));
        float tick = horizon > 8f ? 2f : 1f;
        for (float t = 0f; t <= horizon; t += tick)
            GUI.Label(new Rect(ToX(t) - 2f, rowsBottom + 16f, 40f, 16f), $"{t:0}s", _small);

        return rowsBottom + 34f;
    }

    private static Rect Span(float x0, float x1, float y, float rowH) => new(x0, y + 5f, x1 - x0, rowH - 10f);

    private static Color StateColor(DemoTrace.CommandState state) => state switch
    {
        DemoTrace.CommandState.Running => Accent,
        DemoTrace.CommandState.Completed => new Color(0.42f, 0.84f, 0.54f),
        DemoTrace.CommandState.Skipped => new Color(0.74f, 0.58f, 0.98f),
        DemoTrace.CommandState.FinishedByStep => new Color(0.36f, 0.80f, 0.92f),
        DemoTrace.CommandState.Cancelled => new Color(1f, 0.42f, 0.42f),
        _ => Dim,
    };

    private static void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static bool Button(Rect r, string label, bool selected)
    {
        Color prev = GUI.backgroundColor;
        if (selected)
            GUI.backgroundColor = Accent;

        bool clicked = GUI.Button(r, label);
        GUI.backgroundColor = prev;
        return clicked;
    }

    private void EnsureStyles()
    {
        if (_title != null)
            return;

        _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _title.normal.textColor = Color.white;

        _subtitle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        _subtitle.normal.textColor = Dim;

        _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
        _header.normal.textColor = Dim;

        _text = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
        _text.normal.textColor = Color.white;

        _small = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true, clipping = TextClipping.Clip };
        _small.normal.textColor = new Color(0.85f, 0.87f, 0.9f);

        _wrap = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
        _wrap.normal.textColor = new Color(0.85f, 0.87f, 0.9f);

        _shapeLabel = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _shapeLabel.normal.textColor = new Color(0.08f, 0.08f, 0.1f);
    }

    // 화면 왼쪽 아래 설명. 게임 화면 텍스트는 기본 폰트로 깨지지 않도록 영어로 둔다.
    private static class Notes
    {
        public const string Intro =
            "Pick a scenario. Try <b>Cancel</b> while Sequential runs, then run it again with <b>Compress: ON</b> and compare.";

        public const string Sequential =
            "<b>Sequential</b> — both specs have <b>wait = true</b>. SequencePlayer yields on Move(A), so Fade(B) enters only after A arrives.\n\n" +
            "CommandSpecBase.wait is copied onto the command's WaitForCompletion by DemoCommandFactory; the framework does not wire it for you.";

        public const string Parallel =
            "<b>Parallel</b> — both specs have <b>wait = false</b>. SequencePlayer enters each command once and moves on, " +
            "so they run side by side as background routines.\n\n" +
            "Background routines are bound to the <b>Step</b> lifetime (CommandBase implements IStepScopedCommand): the next step boundary will clean them up.";

        public const string StepCleanup =
            "<b>Step Cleanup</b> — step 1 starts a 5s background Move(A). After 1s the demo calls PlaySpecs for step 2, " +
            "which first runs <b>CleanupStep(Finish)</b>: the routine is stopped and OnStepLifetimeFinished snaps A to its target.\n\n" +
            "Plain CommandBase does nothing there by default — DemoMoveCommand implements it. (ClaimTweenCommandBase speeds the tween up instead.)";

        public const string CompressOn =
            "<b>Compress: ON</b> — CommandRunScope.ShouldCompressCommandExecution is now true. Commands that enter from now on " +
            "with SkipPolicy.CompleteImmediately call <b>OnSkip</b> and land on the final state at once; nothing is waited.\n\n" +
            "A command that is already running is not affected. Run a scenario to see it.";

        public const string CompressOff =
            "<b>Compress: OFF</b> — commands entering from now on run normally again.";

        public const string Cancel =
            "<b>Cancel</b> — session.End() → CommandExecutor.Stop(CleanupPolicy.Cancel). Routines stop where they are, " +
            "no completion hook is called, and commands that had not entered never run.\n\n" +
            "Compare with Compress: compression <i>completes</i> commands, cancellation <i>interrupts</i> them. " +
            "What a cancelled command leaves behind is the command's own choice — these leave shapes where they stopped.";

        public const string Speed =
            "<b>Speed</b> — ICommandPacePolicy.TimeScale. Commands read it once on entry through " +
            "CommandRunScope.ScalePresentationDuration, so a change applies to the next command, not the running one.";
    }
}
