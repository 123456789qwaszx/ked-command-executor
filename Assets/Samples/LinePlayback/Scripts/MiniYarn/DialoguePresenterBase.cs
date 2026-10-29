using UnityEngine;

namespace MiniYarn
{
    // Yarn Spinner의 DialoguePresenterBase 흉내(선택지 메서드는 뺐다).
    // DialogueRunner는 줄을 모든 프레젠터에 건네고, 전부 끝낼 때까지 기다린 뒤 다음 내용으로 간다.
    public abstract class DialoguePresenterBase : MonoBehaviour
    {
        public abstract YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token);

        public abstract YarnTask OnDialogueStartedAsync();

        public abstract YarnTask OnDialogueCompleteAsync();

        public virtual void OnNodeEnter(string nodeName) { }
        public virtual void OnNodeExit(string nodeName) { }
    }
}
