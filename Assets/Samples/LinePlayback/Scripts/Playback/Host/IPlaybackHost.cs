using System.Threading.Tasks;

/// <summary>
/// 재생을 끝까지 쥐는 쪽. 한 실행에 하나만 있다.
///
/// 입력·화면은 "처음부터 다시 돌려 달라 / 끝내 달라"를 이 계약에만 말한다.
/// 실제로 무엇을 다시 도는지(노드 하나인지, 챕터의 장면인지)는 모른다.
///
/// 롤백은 두 몫으로 나뉜다.
/// 1. 표적 정하기 — VNFeatureController가 롤백 포인트를 고르고 시크를 건다.
/// 2. 처음부터 다시 — 이 계약의 구현이 무대를 비우고 재생 단위를 다시 시작한다.
///    시크가 표적 라인까지 데려간다.
///
/// 구현:
/// - NodePlaybackHost — 연출 단독. 재생 단위는 Yarn 노드 하나.
/// - ProgressionLauncher — 진행·저장. 재생 단위는 챕터의 장면.
/// </summary>
public interface IPlaybackHost
{
    bool IsRunning { get; }

    /// <summary>
    /// 지금 재생을 멈추고 같은 단위를 처음부터 다시 돈다.
    /// 실행 중이 아니면 아무 일도 하지 않는다.
    /// </summary>
    Task RequestReplayAsync();

    /// <summary>
    /// 재생을 끝낸다. 실행 중이 아니면 아무 일도 하지 않는다.
    /// </summary>
    Task ExitAsync();
}
