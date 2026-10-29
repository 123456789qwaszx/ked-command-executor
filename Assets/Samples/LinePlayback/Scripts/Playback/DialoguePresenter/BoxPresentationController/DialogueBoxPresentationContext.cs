using System;
using MiniYarn;

// 원본과 같다.
public sealed class DialogueBoxPresentationContext
{
    public LinePresentationRun Run { get; }
    public bool UseImmediateTransition { get; }

    public string Text { get; }
    public string CharacterName { get; }
    public bool HasCharacterName { get; }
    public string[] Metadata { get; }

    public DialogueBoxPresentationContext(
        LocalizedLine line,
        LinePresentationRun run,
        bool useImmediateTransition)
    {
        Run = run;
        UseImmediateTransition = useImmediateTransition;

        Text = line.TextWithoutCharacterName.Text ?? string.Empty;
        CharacterName = line.CharacterName ?? string.Empty;
        HasCharacterName = !string.IsNullOrWhiteSpace(CharacterName);
        Metadata = line.Metadata ?? Array.Empty<string>();
    }
}
