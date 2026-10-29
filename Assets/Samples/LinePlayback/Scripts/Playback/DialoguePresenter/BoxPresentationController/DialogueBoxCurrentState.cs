// 원본과 같다. 바뀐 곳: box_hide/show가 쓰던 MarkHidden · TryMarkVisible 없음.
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

    public void Reset()
    {
        Box = null;
        BoxKind = null;
        IsVisible = false;
    }
}
