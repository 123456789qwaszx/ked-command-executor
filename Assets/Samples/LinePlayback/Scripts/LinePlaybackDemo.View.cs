using System;
using System.Collections.Generic;
using UnityEngine;

// 화면(IMGUI). 왼쪽은 결과(무대 · 대사창 · 버튼 · 이벤트), 오른쪽은 그 순간의 안쪽
// (줄 단계 · 스코프 · 커맨드 · 감사 · 백로그). 화면의 틀 글자는 영어 · ASCII로 둔다 — 대사는 대본 그대로.
public sealed partial class LinePlaybackDemo
{
    private const float RefWidth = 1280f;
    private const float RefHeight = 720f;

    private static readonly Color Panel = new(0.13f, 0.14f, 0.17f);
    private static readonly Color Dim = new(0.55f, 0.58f, 0.64f);
    private static readonly Color Accent = new(0.98f, 0.80f, 0.35f);
    private static readonly Color Good = new(0.42f, 0.84f, 0.54f);
    private static readonly Color Bad = new(1f, 0.42f, 0.42f);
    private static readonly Color Info = new(0.36f, 0.80f, 0.92f);

    // 한 줄 트랜잭션의 정상 경로 — 지금 단계를 이 줄 위에 칠한다.
    private static readonly VNLinePresentationPhase[] PhasePath =
    {
        VNLinePresentationPhase.LineReceived,
        VNLinePresentationPhase.LineEnteredCommitted,
        VNLinePresentationPhase.LineRuntimeStateResolved,
        VNLinePresentationPhase.VisualRunStarted,
        VNLinePresentationPhase.DialogueSurfaceResolved,
        VNLinePresentationPhase.TypewriterReady,
        VNLinePresentationPhase.TypewriterCompleted,
        VNLinePresentationPhase.DisplayCommitted,
        VNLinePresentationPhase.WaitingForAdvance,
        VNLinePresentationPhase.Completed,
    };

    private GUIStyle _title, _header, _text, _small, _smallWrap, _line, _speaker, _bodyLabel;

    private void OnGUI()
    {
        if (_host == null)
            return;

        EnsureStyles();

        float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
        var offset = new Vector2((Screen.width - RefWidth * scale) * 0.5f, (Screen.height - RefHeight * scale) * 0.5f);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));

        GUI.Label(new Rect(20f, 8f, 1240f, 30f), "COMMAND EXECUTOR - LINE PLAYBACK DEMO", _title);

        DrawStage(new Rect(20f, 44f, 780f, 250f));
        DrawDialogueBox(new Rect(20f, 302f, 780f, 110f));
        DrawControls(new Rect(20f, 420f, 780f, 56f));
        DrawLog(new Rect(20f, 482f, 780f, 230f));

        DrawLine(new Rect(820f, 44f, 440f, 74f));
        DrawScope(new Rect(820f, 122f, 440f, 52f));
        DrawCommands(new Rect(820f, 180f, 440f, 140f));
        DrawAudit(new Rect(820f, 326f, 440f, 200f));
        DrawBacklog(new Rect(820f, 532f, 440f, 180f));
    }

    // ───────────────────────────── 왼쪽 ─────────────────────────────

    private void DrawStage(Rect r)
    {
        Fill(r, Panel);
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 18f), "STAGE  (plain state - the executor never sees it)", _header);
        GUI.Label(new Rect(r.x + 10f, r.y + 22f, r.width - 20f, 18f), _stage.Snapshot(), _small);

        foreach (MiniStage.Slot slot in _stage.Slots)
        {
            float cx = r.center.x + slot.X * r.width * 0.32f;
            var body = new Rect(cx - 55f, r.yMax - 175f, 110f, 160f);

            // 숨은 슬롯은 자리만 옅게.
            Outline(body, new Color(1f, 1f, 1f, 0.10f));
            Fill(body, new Color(slot.Color.r, slot.Color.g, slot.Color.b, slot.Alpha));

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Max(0.25f, slot.Alpha));
            GUI.Label(new Rect(body.x, body.y - 20f, body.width, 18f), $"{slot.Key} {slot.DisplayName}", _speaker);
            GUI.Label(body, slot.Face, _bodyLabel);
            GUI.color = prev;
        }

        // BGM — 런 수명 커맨드.
        var bgm = new Rect(r.xMax - 230f, r.y + 6f, 220f, 16f);
        GUI.Label(bgm, _stage.BgmKey != null ? $"BGM  {_stage.BgmKey}" : "BGM  -", _small);
        var bar = new Rect(bgm.x + 130f, bgm.y + 4f, 90f, 8f);
        Fill(bar, new Color(1f, 1f, 1f, 0.08f));
        Fill(new Rect(bar.x, bar.y, bar.width * _stage.BgmVolume, bar.height), Info);
    }

    private void DrawDialogueBox(Rect r)
    {
        Outline(r, new Color(1f, 1f, 1f, 0.08f));

        if (_boxView.Alpha > 0.001f)
        {
            Fill(r, new Color(0.08f, 0.09f, 0.11f, _boxView.Alpha));

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _boxView.Alpha);
            GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 22f), _boxView.CharacterName ?? "", _speaker);
            GUI.Label(new Rect(r.x + 14f, r.y + 32f, r.width - 28f, r.height - 40f), _boxView.GetLineText().VisibleText, _line);
            GUI.color = prev;

            if (_lineFlow.CurrentPhase == VNLinePresentationPhase.WaitingForAdvance && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f)
                GUI.Label(new Rect(r.xMax - 30f, r.yMax - 24f, 20f, 20f), "v", _speaker);
        }
        else
        {
            GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 20f), "(dialogue box hidden)", _small);
        }

        if (_lineState.IsSeekingActive)
            GUI.Label(new Rect(r.x + 14f, r.yMax - 22f, r.width - 28f, 18f), "<color=#5ccdeb>seeking - lines pass through unseen, their commands are compressed</color>", _small);
    }

    private void DrawControls(Rect r)
    {
        const float w = 91f, h = 30f, gap = 7f;
        float x = r.x;

        if (Button(new Rect(x, r.y, w, h), "Advance", false)) _dispatcher.DispatchAdvance();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Rapid skip", _playbackState.IsRapidSkipMode))
        {
            if (_playbackState.IsRapidSkipMode) _features.EndRapidSkip();
            else _features.BeginRapidSkip();
        }
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Speed", _playbackState.IsSpeedUpMode)) _features.ToggleSpeedUpMode();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Auto", _playbackState.IsAutoMode)) _features.ToggleAuto();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Skip", false)) RequestSkip();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Rollback", false)) RequestRollback();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Exit", false)) Exit();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Restart", false)) Restart();

        GUI.Label(new Rect(r.x, r.y + h + 4f, r.width, 18f),
            "Space advance | hold Ctrl rapid skip | S speed | A auto | K skip | R rollback | Esc exit", _small);
    }

    private void DrawLog(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "EVENTS", _header);

        float y = r.y + 20f;
        foreach (string line in _trace.Log)
        {
            GUI.Label(new Rect(r.x, y, r.width, 18f), Colorize(line), _small);
            y += 17f;
        }
    }

    // ───────────────────────────── 오른쪽 ─────────────────────────────

    private void DrawLine(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "LINE  (VNLinePresentationFlow)", _header);

        VNLinePresentationPhase phase = _lineFlow.CurrentPhase;
        GUI.Label(new Rect(r.x, r.y + 18f, r.width, 18f),
            $"<b>{phase}</b>   fully shown {YesNo(_lineState.IsLineFullyShown)}   #auto pending {YesNo(_lineState.IsLineAutoAdvancePending)}", _text);

        // 정상 경로 위의 위치. 경로 밖 단계(SeekPassThrough · Stale)는 따로 칠한다.
        var sb = new System.Text.StringBuilder();
        foreach (VNLinePresentationPhase step in PhasePath)
        {
            string name = Short(step);
            sb.Append(step == phase ? Paint(name, Accent) : $"<color=#5b606b>{name}</color>").Append(' ');
        }

        if (phase == VNLinePresentationPhase.SeekPassThrough)
            sb.Append(Paint("SeekPassThrough", Info));
        else if (phase == VNLinePresentationPhase.Stale)
            sb.Append(Paint("Stale", Bad));

        GUI.Label(new Rect(r.x, r.y + 38f, r.width, 34f), sb.ToString(), _smallWrap);
    }

    private void DrawScope(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "SCOPE  (PresentationScopeSession)", _header);

        if (!_scopeSession.IsRunning)
        {
            GUI.Label(new Rect(r.x, r.y + 18f, r.width, 18f), $"no CommandRunScope   host {_status}", _text);
            return;
        }

        CommandRunScope scope = _scopeSession.CurrentScope;
        GUI.Label(new Rect(r.x, r.y + 18f, r.width, 18f),
            $"session #{_probe.SessionCount}   step {_trace.CurrentStep} [{_trace.CurrentStepLabel}]   host {_status}", _text);
        GUI.Label(new Rect(r.x, r.y + 35f, r.width, 18f),
            $"main routine {(scope.CommandIsPlaying ? "<color=#fbcc59>playing</color>" : "idle")}   " +
            $"compress {OnOff(scope.ShouldCompressCommandExecution)}   x{scope.TimeScale:0.#}   " +
            $"seek {OnOff(_lineState.IsSeekingActive)}", _text);
    }

    private void DrawCommands(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "COMMANDS  (this step + anything still alive)", _header);

        float y = r.y + 19f;
        int shown = 0;

        foreach (MiniTrace.Entry e in _trace.Entries)
        {
            bool relevant = e.Step == _trace.CurrentStep || e.IsRunning;

            if (!relevant || shown >= 7)
                continue;

            string state = e.IsRunning ? $"Running {e.Progress * 100f:0}%" : e.State.ToString();
            GUI.Label(new Rect(r.x, y, 44f, 18f), $"[{e.StepLabel}]", _small);
            GUI.Label(new Rect(r.x + 44f, y, 196f, 18f), e.Label, _small);
            GUI.Label(new Rect(r.x + 242f, y, 44f, 18f), $"<color=#8c93a3>{e.Lifetime}</color>", _small);
            GUI.Label(new Rect(r.x + 290f, y, 150f, 18f), Paint(state, StateColor(e.State)), _small);

            y += 16f;
            shown++;
        }

        if (shown == 0)
            GUI.Label(new Rect(r.x, y, r.width, 18f), "-", _small);
    }

    private void DrawAudit(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "AUDIT  (checked at every step boundary)", _header);

        float y = r.y + 18f;
        GUI.Label(new Rect(r.x, y, r.width, 18f), "<color=#8c93a3>line      settled == model      replay == first      settles</color>", _small);
        y += 15f;

        foreach (MiniLifecycleAudit.LineResult result in _audit.Results)
        {
            if (y > r.yMax - 52f)
                break;

            GUI.Label(new Rect(r.x, y, 60f, 18f), result.Key, _small);
            GUI.Label(new Rect(r.x + 60f, y, 140f, 18f), Verdict(result.ModelOk), _small);
            GUI.Label(new Rect(r.x + 200f, y, 140f, 18f), Verdict(result.ReplayOk), _small);
            GUI.Label(new Rect(r.x + 340f, y, 60f, 18f), result.Settles.ToString(), _small);
            y += 14f;
        }

        y = r.yMax - 50f;
        GUI.Label(new Rect(r.x, y, r.width, 18f),
            $"overlap {Count(_audit.Overlaps)}   late write {Count(_stage.LateWrites)}   stale write {Count(_stage.StaleWrites)}   " +
            $"model {Count(_audit.ModelMismatches)}   replay {Count(_audit.ReplayMismatches)}", _small);

        if (_audit.LastMismatch != null)
            GUI.Label(new Rect(r.x, y + 16f, r.width, 34f), _audit.LastMismatch, _smallWrap);
    }

    private void DrawBacklog(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "BACKLOG  (jump = rollback to that line)", _header);

        IReadOnlyList<DialogueLogEntry> entries = _features.Backlogs;
        int first = Math.Max(0, entries.Count - 6);
        float y = r.y + 20f;

        for (int i = first; i < entries.Count; i++)
        {
            DialogueLogEntry entry = entries[i];
            bool canJump = _host.IsRunning && _features.CanJumpTo(entry);

            GUI.enabled = canJump;
            if (GUI.Button(new Rect(r.x, y, 44f, 20f), "jump"))
                RequestBacklogJump(entry);
            GUI.enabled = true;

            GUI.Label(new Rect(r.x + 50f, y + 1f, r.width - 50f, 18f), $"<color=#8c93a3>{entry.lineId}</color>  {entry.rawText}", _small);
            y += 25f;
        }
    }

    // ───────────────────────────── 도구 ─────────────────────────────

    private static string Short(VNLinePresentationPhase phase) => phase switch
    {
        VNLinePresentationPhase.LineReceived => "Received",
        VNLinePresentationPhase.LineEnteredCommitted => "Entered",
        VNLinePresentationPhase.LineRuntimeStateResolved => "Resolved",
        VNLinePresentationPhase.VisualRunStarted => "VisualRun",
        VNLinePresentationPhase.DialogueSurfaceResolved => "Surface",
        VNLinePresentationPhase.TypewriterReady => "TypeReady",
        VNLinePresentationPhase.TypewriterCompleted => "Typed",
        VNLinePresentationPhase.DisplayCommitted => "Displayed",
        VNLinePresentationPhase.WaitingForAdvance => "Waiting",
        VNLinePresentationPhase.Completed => "Completed",
        _ => phase.ToString(),
    };

    private static string Verdict(bool? ok) => ok switch
    {
        true => Paint("ok", Good),
        false => Paint("FAIL", Bad),
        _ => "<color=#8c93a3>-</color>",
    };

    private static string Count(int count) => Paint(count.ToString(), count == 0 ? Good : Bad);
    private static string YesNo(bool value) => value ? "yes" : "no";
    private static string OnOff(bool value) => value ? "<color=#fbcc59>ON</color>" : "off";

    private static string Paint(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    private static string Colorize(string line)
    {
        if (line.Contains("OVERLAP") || line.Contains("LATE WRITE") || line.Contains("STALE WRITE") || line.Contains("MISMATCH"))
            return Paint(line, Bad);

        if (line.Contains("refused") || line.Contains("discarded"))
            return Paint(line, Accent);

        if (line.Contains("session") || line.Contains("rollback") || line.Contains("jump"))
            return Paint(line, Info);

        return line;
    }

    private static Color StateColor(MiniTrace.CommandState state) => state switch
    {
        MiniTrace.CommandState.Running => Accent,
        MiniTrace.CommandState.Completed => Good,
        MiniTrace.CommandState.Skipped => new Color(0.74f, 0.58f, 0.98f),
        MiniTrace.CommandState.Finished => Info,
        MiniTrace.CommandState.Cancelled => Bad,
        _ => Dim,
    };

    private static void Fill(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    private static void Outline(Rect r, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, 1f), c);
        Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
        Fill(new Rect(r.x, r.y, 1f, r.height), c);
        Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
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

        _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        _title.normal.textColor = Color.white;

        _header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
        _header.normal.textColor = Dim;

        _text = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, clipping = TextClipping.Clip };
        _text.normal.textColor = Color.white;

        _small = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true, clipping = TextClipping.Clip };
        _small.normal.textColor = new Color(0.85f, 0.87f, 0.9f);

        _smallWrap = new GUIStyle(_small) { wordWrap = true };

        _line = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
        _line.normal.textColor = Color.white;

        _speaker = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _speaker.normal.textColor = Accent;

        _bodyLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _bodyLabel.normal.textColor = new Color(0.08f, 0.08f, 0.1f);

        if (dialogueFont != null)
        {
            _line.font = dialogueFont;
            _speaker.font = dialogueFont;
            _small.font = dialogueFont;
            _smallWrap.font = dialogueFont;
        }
    }
}
