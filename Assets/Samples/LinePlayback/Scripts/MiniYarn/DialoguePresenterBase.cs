using UnityEngine;

namespace MiniYarn
{
    // Yarn Spinner의 DialoguePresenterBase 흉내 — 줄 · 대화 시작 · 대화 끝만.
    // DialogueRunner는 줄을 프레젠터에 건네고, 끝낼 때까지 기다린 뒤 다음 내용으로 간다.
    public abstract class DialoguePresenterBase : MonoBehaviour
    {
        public abstract YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token);

        public abstract YarnTask OnDialogueStartedAsync();

        public abstract YarnTask OnDialogueCompleteAsync();
    }
}
