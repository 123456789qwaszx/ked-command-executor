using System;
using System.Threading;
using UnityEngine.Events;

namespace MiniYarn
{
    // Yarn 런타임의 Line · Command 흉내. 컴파일된 노드 안의 내용 하나.
    public sealed class Line
    {
        public string ID;
        public string RawText;
        public string[] Metadata;
    }

    public sealed class Command
    {
        public string Text;
    }

    // Yarn.Markup.MarkupParseResult 흉내 — 이 샘플은 마크업을 쓰지 않으므로 글자만 갖는다.
    public readonly struct MarkupParseResult
    {
        public readonly string Text;

        public MarkupParseResult(string text)
        {
            Text = text ?? string.Empty;
        }
    }

    // LocalizedLine 흉내. "이름: 대사"에서 이름을 떼어 CharacterName으로 둔다(Yarn의 character 속성과 같은 일).
    public sealed class LocalizedLine
    {
        public string TextID { get; }
        public string RawText { get; }
        public string CharacterName { get; }
        public MarkupParseResult Text { get; }
        public MarkupParseResult TextWithoutCharacterName { get; }
        public string[] Metadata { get; }

        public LocalizedLine(Line line)
        {
            TextID = line.ID;
            RawText = line.RawText ?? string.Empty;
            Metadata = line.Metadata ?? Array.Empty<string>();
            Text = new MarkupParseResult(RawText);

            int colon = RawText.IndexOf(':');

            if (colon > 0 && colon <= 24)
            {
                CharacterName = RawText.Substring(0, colon).Trim();
                TextWithoutCharacterName = new MarkupParseResult(RawText.Substring(colon + 1).TrimStart());
            }
            else
            {
                CharacterName = null;
                TextWithoutCharacterName = Text;
            }
        }
    }

    // Yarn Spinner의 LineCancellationToken과 같다.
    // HurryUpToken은 NextContentToken에 묶여 있다 — 다음 줄 요청은 서두르기를 겸한다.
    public struct LineCancellationToken
    {
        public CancellationToken NextContentToken;
        public CancellationToken HurryUpToken;

        public readonly bool IsNextContentRequested => NextContentToken.IsCancellationRequested;
        public readonly bool IsHurryUpRequested => HurryUpToken.IsCancellationRequested;
    }

    [Serializable]
    public class UnityEventString : UnityEvent<string> { }
}
