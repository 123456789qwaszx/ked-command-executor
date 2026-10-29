public static class DialogueBoxTagResolver
{
    private delegate bool TryParseTag<T>(string token, out T value);

    public static bool TryResolveBoxKind(string[] metadata, out DialogueBoxKind kind)
        => TryResolveFirst(metadata, DialogueBoxKindParser.TryParse, out kind);

    public static bool TryResolveTransitionKind(string[] metadata, out DialogueBoxTransitionKind transition)
        => TryResolveFirst(metadata, DialogueBoxTransitionParser.TryParse, out transition);

    private static bool TryResolveFirst<T>(
        string[] metadata,
        TryParseTag<T> tryParse,
        out T value)
    {
        value = default;

        if (metadata == null)
            return false;

        for (int i = 0; i < metadata.Length; i++)
        {
            if (tryParse(metadata[i], out value))
                return true;
        }

        return false;
    }
}