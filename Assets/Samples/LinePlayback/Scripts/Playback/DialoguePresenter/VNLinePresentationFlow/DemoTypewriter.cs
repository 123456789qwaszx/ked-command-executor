using System;
using System.Collections.Generic;
using System.Threading;
using MiniYarn;
using UnityEngine;

// TMP_Text 자리 — 타자기는 보이는 글자 수만 만진다(원본: maxVisibleCharacters).
public sealed class DialogueTextView
{
    public string Text { get; private set; } = string.Empty;
    public int MaxVisibleCharacters { get; set; }
    public int CharacterCount => Text.Length;

    public string VisibleText =>
        Text.Substring(0, Mathf.Clamp(MaxVisibleCharacters, 0, Text.Length));

    public void SetText(string text)
    {
        Text = text ?? string.Empty;
    }
}

// ActionMarkupHandler 자리 — 이 샘플은 줄이 다 보였다는 알림만 쓴다(AutoAdvanceScheduler).
public interface ITypewriterHandler
{
    void OnLineDisplayBegin();
    void OnLineDisplayComplete();
    void OnLineWillDismiss();
}

// EllipsisBreathTypewriter를 줄인 것. 원본과 같은 약속을 지킨다:
//   hurryToken      — 모두 펼치기가 아니라 "이 줄 끝까지 가속"(onHurryStarted → LineHurrySpeedController.Enter)
//   hardCancelToken — 이 줄의 표시 자체가 무효가 됐다. 펼치지 않고 바로 멈춘다
//   runId           — 새 줄이 시작되면 앞 줄의 루프는 손을 뗀다
// 바뀐 곳: 글자마다 한 프레임씩 기다리지 않고 지연을 누적해, 가속하면 한 프레임에 여러 글자가 나온다.
public sealed class DemoTypewriter
{
    private const float UnitsPerSecond = 30f;
    private const float NormalDelayCap = 0.35f;
    private const float SpeedupDelayCap = 0.08f;

    private DialogueTextView _text;
    private float _speedMultiplier = 1f;
    private int _runId;

    public List<ITypewriterHandler> Handlers { get; } = new();

    public float SpeedMultiplier => _speedMultiplier;

    public void SetSpeedMultiplier(float multiplier) =>
        _speedMultiplier = Mathf.Clamp(multiplier, 0.05f, 32f);

    public void SetTextView(DialogueTextView textView) => _text = textView;

    public void PrepareForContent(MarkupParseResult line)
    {
        if (_text == null)
        {
            Debug.LogError("[Typewriter] PrepareForContent: no text view is available.");
            return;
        }

        _text.SetText(line.Text);
        _text.MaxVisibleCharacters = 0;
    }

    public async YarnTask RunTypewriter(
        MarkupParseResult line,
        CancellationToken hurryToken,
        CancellationToken hardCancelToken,
        Action onHurryStarted)
    {
        if (_text == null)
            return;

        int myRunId = ++_runId;
        bool hurryActive = false;
        double debt = 0;

        void ActivateHurry()
        {
            if (hurryActive)
                return;

            hurryActive = true;
            debt = 0;

            try
            {
                onHurryStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        CancellationTokenRegistration hurryRegistration =
            hurryToken.CanBeCanceled ? hurryToken.Register(ActivateHurry) : default;

        try
        {
            InvokeHandlers(h => h.OnLineDisplayBegin());

            int total = _text.CharacterCount;

            for (int visible = 0; visible < total; visible++)
            {
                if (myRunId != _runId || hardCancelToken.IsCancellationRequested)
                    return;

                if (hurryToken.IsCancellationRequested)
                    ActivateHurry();

                _text.MaxVisibleCharacters = visible + 1;

                double delay = Math.Min(SecondPerChar() * DelayMultiplier(_text.Text[visible]), CurrentDelayCap());
                debt += delay;

                while (debt > 0 && myRunId == _runId && !hardCancelToken.IsCancellationRequested)
                {
                    double t0 = Time.timeAsDouble;
                    await YarnTask.Yield();
                    debt -= Time.timeAsDouble - t0;
                }
            }

            if (myRunId != _runId || hardCancelToken.IsCancellationRequested)
                return;

            _text.MaxVisibleCharacters = total;
            InvokeHandlers(h => h.OnLineDisplayComplete());
        }
        finally
        {
            hurryRegistration.Dispose();
        }
    }

    public void ContentWillDismiss()
    {
        InvokeHandlers(h => h.OnLineWillDismiss());
    }

    private double SecondPerChar()
    {
        float charsPerSecond = UnitsPerSecond * Mathf.Max(0.01f, _speedMultiplier);
        return 1.0 / charsPerSecond;
    }

    private double CurrentDelayCap() => _speedMultiplier > 1.01f ? SpeedupDelayCap : NormalDelayCap;

    // 숨 — 구두점에서 조금 쉰다.
    private static double DelayMultiplier(char c)
    {
        switch (c)
        {
            case ' ': return 0.35;
            case ',': return 1.7;
            case '.': case '?': case '!': return 3.2;
            case '…': return 5.2;
            default: return 1.0;
        }
    }

    private void InvokeHandlers(Action<ITypewriterHandler> action)
    {
        for (int i = 0; i < Handlers.Count; i++)
        {
            try
            {
                action(Handlers[i]);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }
}
