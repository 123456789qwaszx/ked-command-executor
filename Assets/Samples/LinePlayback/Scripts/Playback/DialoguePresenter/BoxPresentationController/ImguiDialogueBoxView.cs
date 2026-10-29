using System;
using MiniYarn;
using UnityEngine;

// 대사창 뷰 — 상태만 갖고, 그리기는 LinePlaybackDemo.View(IMGUI)가 한다.
// 페이드는 원본처럼 "unscaled 시간 × 서두르기 배율"로 흐른다(PresentationClock — DOTween.unscaledTimeScale 자리).
public sealed class ImguiDialogueBoxView : IPresentationDialogueBoxView
{
    private readonly DialogueTextView _line = new();

    public bool Visible { get; private set; }
    public float Alpha { get; private set; }
    public string CharacterName { get; private set; }

    public DialogueTextView GetLineText() => _line;

    public void PrimeText(string text, string characterName, bool hasCharacterName)
    {
        _line.SetText(text);
        _line.MaxVisibleCharacters = 0;
        CharacterName = hasCharacterName ? characterName : null;
    }

    public void SetVisibleImmediate(bool visible)
    {
        Visible = visible;
        Alpha = visible ? 1f : 0f;
    }

    public void PrepareHidden()
    {
        Visible = true;
        Alpha = 0f;
    }

    public YarnTask FadeInAsync(float duration, LinePresentationRun run) => Fade(1f, duration, () => run.IsValid);
    public YarnTask FadeOutAsync(float duration, LinePresentationRun run) => Fade(0f, duration, () => run.IsValid);

    private async YarnTask Fade(float to, float duration, Func<bool> live)
    {
        float from = Alpha;

        for (float t = 0f; t < duration; t += PresentationClock.DeltaTime)
        {
            if (!live())
                return;

            Alpha = Mathf.Lerp(from, to, t / duration);
            await YarnTask.Yield();
        }

        if (live())
            Alpha = to;
    }
}
