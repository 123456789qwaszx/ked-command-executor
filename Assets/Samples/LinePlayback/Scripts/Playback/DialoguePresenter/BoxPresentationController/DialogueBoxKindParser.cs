using System;

// 태그 하나 -> 박스 종류.
//
// 종류 이름 그대로 쓴 표기는 Enum.TryParse가 받는다 —
// 그래서 **종류를 새로 늘려도 이 파서는 건드릴 필요가 없다.**
// switch에는 이름과 다른 별칭만 남긴다.
public static class DialogueBoxKindParser
{
    public static bool TryParse(string raw, out DialogueBoxKind kind)
    {
        kind = default;

        string token = DialogueBoxTagNormalizer.Normalize(raw);

        if (token.Length == 0)
            return false;

        // `box=surface` 처럼 명시적으로 적은 형태의 접두사를 벗긴다.
        if (token.StartsWith("box=", StringComparison.Ordinal))
            token = token.Substring(4);

        switch (token)
        {
            case "surfacebox":
                kind = DialogueBoxKind.Surface;
                return true;

            case "text":
                kind = DialogueBoxKind.OnlyText;
                return true;
        }

        // Enum.TryParse는 "0" 같은 숫자 문자열도 받는다 —
        // 저작 태그가 우연히 숫자면 0번 종류로 해석되므로 먼저 막는다.
        if (char.IsDigit(token[0]))
            return false;

        return Enum.TryParse(token, true, out kind);
    }
}