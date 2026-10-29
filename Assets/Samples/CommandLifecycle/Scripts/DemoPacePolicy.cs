// 실행기가 재생 쪽에 묻는 두 가지(ICommandPacePolicy)의 데모 구현.
// 재생 저장소에서는 자동·빨리 넘기기·시크 같은 깃발을 이 둘로 접어서 넘긴다. 여기서는 버튼이 직접 바꾼다.
// CommandRunScope는 값을 복사해 두지 않고 매번 묻는다 — 바꾼 값은 다음에 진입하는 커맨드부터 반영된다.
public sealed class DemoPacePolicy : ICommandPacePolicy
{
    public bool CompressExecution { get; set; }
    public float TimeScale { get; set; } = 1f;
}
