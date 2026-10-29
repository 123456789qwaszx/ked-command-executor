// 원본과 같다.
public sealed class DialogueBoxCurrentState
{
    public IPresentationDialogueBoxView Box { get; private set; }
    public DialogueBoxKind? BoxKind { get; private set; }
    public bool IsVisible { get; private set; }

    public void Commit(
        DialogueBoxKind boxKind,
        IPresentationDialogueBoxView box,
        DialogueBoxTransitionKind transitionKind)
    {
        BoxKind = boxKind;
        Box = box;
        IsVisible = transitionKind != DialogueBoxTransitionKind.Hide;
    }

    public void MarkHidden()
    {
        IsVisible = false;
    }

    public bool TryMarkVisible()
    {
        if (Box == null || BoxKind.HasValue == false)
            return false;

        IsVisible = true;
        return true;
    }

    public void Reset()
    {
        Box = null;
        BoxKind = null;
        IsVisible = false;
    }
}
