using UnityEngine;

// 화면(IMGUI). 왼쪽은 결과(무대 · 대사창 · 버튼 · 이벤트), 오른쪽은 그 순간의 실행기 안쪽(줄 · 스코프 · 커맨드 · 감사).
// 게임 화면 글자는 기본 폰트에서 깨지지 않도록 영어 · ASCII로만 쓴다.
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

    private GUIStyle _title, _header, _text, _small, _wrap, _speaker, _bodyLabel;

    private void OnGUI()
    {
        EnsureStyles();

        float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
        var offset = new Vector2((Screen.width - RefWidth * scale) * 0.5f, (Screen.height - RefHeight * scale) * 0.5f);
        GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));

        GUI.Label(new Rect(20f, 8f, 1240f, 30f), "COMMAND EXECUTOR - LINE PLAYBACK DEMO", _title);

        DrawStage(new Rect(20f, 44f, 780f, 290f));
        DrawDialogueBox(new Rect(20f, 344f, 780f, 112f));
        DrawControls(new Rect(20f, 466f, 780f, 60f));
        DrawLog(new Rect(20f, 532f, 780f, 180f));

        DrawLineAndScope(new Rect(820f, 44f, 440f, 130f));
        DrawCommands(new Rect(820f, 184f, 440f, 170f));
        DrawAudit(new Rect(820f, 364f, 440f, 348f));
    }

    // ───────────────────────────── 왼쪽 ─────────────────────────────

    private void DrawStage(Rect r)
    {
        Fill(r, Panel);
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 18f), "STAGE  (plain state - the executor never sees it)", _header);
        GUI.Label(new Rect(r.x + 10f, r.y + 22f, r.width - 20f, 18f), _stage.Snapshot(), _small);

        foreach (MiniStage.Character c in _stage.Characters)
        {
            float cx = r.center.x + c.X * r.width * 0.32f;
            var body = new Rect(cx - 55f, r.yMax - 200f + c.Bob * 6f, 110f, 180f);

            // 숨은 캐릭터는 자리만 옅게.
            Outline(body, new Color(1f, 1f, 1f, 0.12f));
            Fill(body, new Color(c.Color.r, c.Color.g, c.Color.b, c.Alpha));

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Max(0.25f, c.Alpha));
            GUI.Label(new Rect(body.x, body.y - 22f, body.width, 20f), c.Ambient ? $"{c.Id}  ~ idle" : c.Id, _speaker);
            GUI.Label(body, c.Face, _bodyLabel);
            GUI.color = prev;
        }
    }

    private void DrawDialogueBox(Rect r)
    {
        Fill(r, new Color(0.08f, 0.09f, 0.11f));
        Outline(r, new Color(1f, 1f, 1f, 0.15f));

        string speaker = _typewriter.Speaker ?? "";
        string body = _typewriter.VisibleText;

        if (_presenter.Phase == MiniLinePhase.SeekPassThrough || _line.IsSeeking)
        {
            speaker = "";
            body = $"(seeking to {_line.SeekTargetId} - lines pass through unseen, commands compressed)";
        }

        GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 22f), speaker, _speaker);
        GUI.Label(new Rect(r.x + 14f, r.y + 32f, r.width - 28f, r.height - 40f), body, _wrap);

        if (_presenter.Phase == MiniLinePhase.WaitingForAdvance && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f)
            GUI.Label(new Rect(r.xMax - 30f, r.yMax - 24f, 20f, 20f), "v", _speaker);

        if (_status == RunStatus.Cancelled)
            GUI.Label(new Rect(r.xMax - 300f, r.y + 8f, 286f, 20f), "<color=#ff6b6b>CANCELLED - left where it stopped</color>", _text);
        else if (_status == RunStatus.NodeComplete)
            GUI.Label(new Rect(r.xMax - 300f, r.y + 8f, 286f, 20f), "<color=#6bd68a>NODE COMPLETE</color>", _text);
    }

    private void DrawControls(Rect r)
    {
        const float w = 104f, h = 32f, gap = 8f;
        float x = r.x;

        if (Button(new Rect(x, r.y, w, h), "Advance", false)) _controls.Advance();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Fast-fwd", _modes.IsFastForward))
            _modes.FastForwardToggled = !_modes.FastForwardToggled;
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Skip", _modes.SkipActive)) _controls.RequestSkip();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Rollback", false)) Rollback();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Cancel", false)) Cancel();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Restart", false)) Restart();
        x += w + gap;

        if (Button(new Rect(x, r.y, w, h), "Speed x4", _modes.SpeedUp)) _modes.SpeedUp = !_modes.SpeedUp;

        GUI.Label(new Rect(r.x, r.y + h + 6f, r.width, 18f),
            "Space advance  |  hold Ctrl fast-forward  |  R rollback  -  refused requests show up in EVENTS", _small);
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

    private void DrawLineAndScope(Rect r)
    {
        float y = r.y;

        GUI.Label(new Rect(r.x, y, r.width, 18f), "LINE", _header);
        y += 18f;
        GUI.Label(new Rect(r.x, y, r.width, 18f),
            $"{_presenter.CurrentLineId}   phase <b>{_presenter.Phase}</b>   fully shown {YesNo(_line.IsLineFullyShown)}", _text);
        y += 18f;
        GUI.Label(new Rect(r.x, y, r.width, 18f),
            _line.IsSeeking ? $"<color=#5ccdeb>seeking to {_line.SeekTargetId} (rollback)</color>" : "not seeking", _small);
        y += 24f;

        GUI.Label(new Rect(r.x, y, r.width, 18f), "SCOPE", _header);
        y += 18f;

        if (!_session.IsRunning)
        {
            GUI.Label(new Rect(r.x, y, r.width, 18f), "no CommandRunScope (session ended)", _text);
            return;
        }

        CommandRunScope scope = _session.CurrentScope;
        GUI.Label(new Rect(r.x, y, r.width, 18f),
            $"session #{_sessionCount}   step {_trace.CurrentStep} [{_trace.CurrentStepLabel}]   status {_status}", _text);
        y += 18f;
        GUI.Label(new Rect(r.x, y, r.width, 18f),
            $"main routine {(scope.CommandIsPlaying ? "<color=#fbcc59>playing</color>" : "idle")}   " +
            $"compress {OnOff(scope.ShouldCompressCommandExecution)}   x{scope.TimeScale:0}   " +
            $"skip {OnOff(_modes.SkipActive)}", _text);
    }

    private void DrawCommands(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "COMMANDS  (this step + anything still alive)", _header);

        float y = r.y + 20f;
        int shown = 0;

        foreach (MiniTrace.Entry e in _trace.Entries)
        {
            bool relevant = e.Step == _trace.CurrentStep || e.IsRunning;

            if (!relevant || shown >= 8)
                continue;

            string state = e.IsRunning ? $"Running {e.Progress * 100f:0}%" : e.State.ToString();
            GUI.Label(new Rect(r.x, y, 70f, 18f), $"[{e.StepLabel}]", _small);
            GUI.Label(new Rect(r.x + 44f, y, 190f, 18f), e.Label, _small);
            GUI.Label(new Rect(r.x + 236f, y, 50f, 18f), $"<color=#8c93a3>{e.Lifetime}</color>", _small);
            GUI.Label(new Rect(r.x + 290f, y, 150f, 18f), Paint(state, StateColor(e.State)), _small);

            y += 17f;
            shown++;
        }

        if (shown == 0)
            GUI.Label(new Rect(r.x, y, r.width, 18f), "-", _small);
    }

    private void DrawAudit(Rect r)
    {
        GUI.Label(new Rect(r.x, r.y, r.width, 18f), "AUDIT  (checked at every step boundary)", _header);

        float y = r.y + 20f;
        GUI.Label(new Rect(r.x, y, r.width, 18f), "<color=#8c93a3>line     settled == model     replay == first     settles</color>", _small);
        y += 17f;

        foreach (MiniLifecycleAudit.LineResult result in _audit.Results)
        {
            GUI.Label(new Rect(r.x, y, 60f, 18f), result.Key, _small);
            GUI.Label(new Rect(r.x + 60f, y, 140f, 18f), Verdict(result.ModelOk), _small);
            GUI.Label(new Rect(r.x + 200f, y, 140f, 18f), Verdict(result.ReplayOk), _small);
            GUI.Label(new Rect(r.x + 340f, y, 60f, 18f), result.Settles.ToString(), _small);
            y += 17f;
        }

        y = Mathf.Max(y + 8f, r.y + 200f);

        Counter(ref y, r, "overlapping steps", _audit.Overlaps);
        Counter(ref y, r, "late writes (old step, same session)", _stage.LateWrites);
        Counter(ref y, r, "stale writes (ended session)", _stage.StaleWrites);
        Counter(ref y, r, "model mismatches", _audit.ModelMismatches);
        Counter(ref y, r, "replay mismatches", _audit.ReplayMismatches);

        if (_audit.LastMismatch != null)
            GUI.Label(new Rect(r.x, y + 4f, r.width, 40f), _audit.LastMismatch, _small);
    }

    private void Counter(ref float y, Rect r, string label, int count)
    {
        GUI.Label(new Rect(r.x, y, 260f, 18f), label, _small);
        GUI.Label(new Rect(r.x + 260f, y, 80f, 18f), Paint(count.ToString(), count == 0 ? Good : Bad), _small);
        y += 17f;
    }

    // ───────────────────────────── 도구 ─────────────────────────────

    private static string Verdict(bool? ok) => ok switch
    {
        true => Paint("ok", Good),
        false => Paint("FAIL", Bad),
        _ => "<color=#8c93a3>-</color>",
    };

    private static string YesNo(bool value) => value ? "yes" : "no";
    private static string OnOff(bool value) => value ? "<color=#fbcc59>ON</color>" : "off";

    private static string Paint(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    // 로그 줄의 위반 · 경계 낱말에 색을 입힌다.
    private static string Colorize(string line)
    {
        if (line.Contains("OVERLAP") || line.Contains("LATE WRITE") || line.Contains("STALE WRITE") || line.Contains("MISMATCH"))
            return Paint(line, Bad);

        if (line.Contains("refused"))
            return Paint(line, Accent);

        if (line.Contains("session") || line.Contains("seek"))
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

        _wrap = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        _wrap.normal.textColor = Color.white;

        _speaker = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _speaker.normal.textColor = Accent;

        _bodyLabel = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _bodyLabel.normal.textColor = new Color(0.08f, 0.08f, 0.1f);
    }
}
