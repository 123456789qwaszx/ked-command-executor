using System.Text;

// 라인 메타데이터 태그의 표기 흔들림을 흡수한다.
//
// 저작에서 `#box:surface` `#box=surface` `#surface_box` `#SurfaceBox`가 전부 나오는데,
// 이걸 파서마다 case로 나열하면 종류 하나에 표기 조합만큼 줄이 늘어난다.
// 여기서 한 번 접어 두면 파서는 정규형 하나만 알면 된다.
public static class DialogueBoxTagNormalizer
{
    /// <summary>소문자화 + 구분자 제거(`_` `-` 공백) + `:`를 `=`로 통일.</summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        StringBuilder sb = new(raw.Length);

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            // '#'은 Yarn이 이미 떼고 주지만, 저작이 그대로 넘겨도 걸리지 않도록 같이 버린다.
            if (c == '_' || c == '-' || c == ' ' || c == '#')
                continue;

            sb.Append(c == ':' ? '=' : char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
