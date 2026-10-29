using System.Threading;
using MiniYarn;

// 원본과 같은 계약. 바뀐 곳: TMP_Text → DialogueTextView, 레이아웃 프리셋 없음.
public interface IPresentationDialogueBoxView
{
    DialogueTextView GetLineText();

    void PrimeText(string text, string characterName, bool hasCharacterName);

    void SetVisibleImmediate(bool visible);
    void PrepareHidden();

    // Manual box visibility fade.
    // Used by box_hide / box_show.
    // Bound to the controller-owned visibility token.
    YarnTask FadeInAsync(float duration, CancellationToken token);
    YarnTask FadeOutAsync(float duration, CancellationToken token);

    // Line-bound box transition fade.
    // Uses the line run token and must not commit after the run becomes invalid.
    YarnTask FadeInAsync(float duration, LinePresentationRun run);
    YarnTask FadeOutAsync(float duration, LinePresentationRun run);
}
