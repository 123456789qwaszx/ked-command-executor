using System;
using System.Threading;
using UnityEngine.Events;

namespace MiniYarn
{
    // Yarn.Markup.MarkupParseResult 흉내 — 이 샘플은 마크업을 쓰지 않으므로 글자만 갖는다.
    public readonly struct MarkupParseResult
    {
        public readonly string Text;

        public MarkupParseResult(string text)
        {
            Text = text ?? string.Empty;
        }
    }

    // LocalizedLine 흉내 — 줄 ID · 화자 · 대사 · 태그(#auto · #boxhide …).
    public sealed class LocalizedLine
    {
        public string TextID { get; }
        public string CharacterName { get; }
        public MarkupParseResult TextWithoutCharacterName { get; }
        public string[] Metadata { get; }

        public LocalizedLine(string textId, string characterName, string text, string[] metadata = null)
        {
            TextID = textId;
            CharacterName = characterName;
            TextWithoutCharacterName = new MarkupParseResult(text);
            Metadata = metadata ?? Array.Empty<string>();
        }
    }

    // Yarn Spinner의 LineCancellationToken과 같다.
    // HurryUpToken은 NextContentToken에 묶여 있다 — 다음 줄 요청은 서두르기를 겸한다.
    public struct LineCancellationToken
    {
        public CancellationToken NextContentToken;
        public CancellationToken HurryUpToken;
    }

    [Serializable]
    public class UnityEventString : UnityEvent<string> { }
}
