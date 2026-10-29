using System;

// 태그 하나 -> 박스 전환 종류.
//
// 박스 종류와 달리 **`box` 접두사를 반드시 요구한다.**
// 전환 이름(hide/cut/keep)이 흔한 단어라, 다른 용도로 붙인 `#hide` 하나가
// 대사창을 감추는 사고를 막기 위한 것이다.
//
// 접두사를 벗긴 뒤는 종류 파서와 같다 — 이름 그대로면 Enum.TryParse가 받으므로
// 전환을 늘려도 이 파서는 그대로다.
public static class DialogueBoxTransitionParser
{
    private const string LongPrefix = "boxtransition=";
    private const string ShortPrefix = "box";

    public static bool TryParse(string raw, out DialogueBoxTransitionKind transition)
    {
        transition = default;

        string token = DialogueBoxTagNormalizer.Normalize(raw);

        if (!TryStripBoxPrefix(token, out string name))
            return false;

        // FadeOutIn의 저작 표기가 `fade`다 — 열거형 이름과 달라 여기서 받는다.
        if (name == "fade")
        {
            transition = DialogueBoxTransitionKind.FadeOutIn;
            return true;
        }

        // Enum.TryParse는 "0" 같은 숫자 문자열도 받는다 — 종류 파서와 같은 이유로 막는다.
        if (char.IsDigit(name[0]))
            return false;

        return Enum.TryParse(name, true, out transition);
    }

    private static bool TryStripBoxPrefix(string token, out string name)
    {
        name = null;

        if (token.StartsWith(LongPrefix, StringComparison.Ordinal))
        {
            name = token.Substring(LongPrefix.Length);
            return name.Length > 0;
        }

        if (token.StartsWith(ShortPrefix, StringComparison.Ordinal))
        {
            name = token.Substring(ShortPrefix.Length);

            // `box=fade` 처럼 명시 표기로 적은 경우.
            if (name.StartsWith("=", StringComparison.Ordinal))
                name = name.Substring(1);

            return name.Length > 0;
        }

        return false;
    }
}