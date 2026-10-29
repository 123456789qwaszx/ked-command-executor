using System.Threading.Tasks;
using MiniYarn;

// 원본과 같다(Yarn.Unity → MiniYarn).

public interface IEpisodeNodeRunner
{
    bool IsRunning { get; }

    Task StopAsync();

    Task RunNodeAsync(string nodeName);
}

public sealed class YarnEpisodeNodeRunner : IEpisodeNodeRunner
{
    private readonly DialogueRunner _dialogueRunner;

    public YarnEpisodeNodeRunner(DialogueRunner dialogueRunner)
    {
        _dialogueRunner = dialogueRunner;
    }

    public bool IsRunning => _dialogueRunner.IsDialogueRunning;

    public async Task StopAsync()
    {
        await _dialogueRunner.Stop();
    }

    public async Task RunNodeAsync(string nodeName)
    {
        await _dialogueRunner.StartDialogue(nodeName);
        await _dialogueRunner.DialogueTask;

        // DialogueTask 완료 콜백의 현재 호출 스택에서 빠져나오기 위한 양보.
        // 제거 시 Stop/Start 재진입으로 데드락이 발생할 수 있음.
        await YarnTask.Yield();
    }
}