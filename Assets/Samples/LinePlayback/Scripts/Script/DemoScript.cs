// 이 샘플이 재생하는 대본 하나. 줄마다 한 가지 생명주기 상황을 지나가도록 짰다.
// 게임 화면 텍스트는 기본 폰트로 깨지지 않도록 영어로 둔다. 설명은 주석과 README에 있다.
//
//   l01  줄 앞머리 pause(대기 명령) — 끝날 때까지 넘기기를 거절한다(메인 루틴이 바쁘다).
//   l02  3초짜리 배경 이동 — 끝나기 전에 넘기면 스텝 경계가 Finish로 끝낸다.
//   l03  런 수명 명령(ambient) — 줄 경계를 넘어 살고, 런이 끝날 때 정리된다.
//   l04  기다리는 이동(wait) — 여기서 롤백하면 루트부터 다시 달려 l03까지 시크한다.
//   l05  빨리 감기 — 압축된 명령은 OnSkip으로 최종 상태에 바로 선다.
//   l06  pause 뒤에 명령이 하나 더 있다 — pause 도중에 빨리 감거나 스킵하면 이 스텝이 끝나기 전에
//        다음 스텝이 시작된다. 감사(audit)가 겹침과 늦은 쓰기를 잡는다.
//   l07  스킵 · 취소.
//   l08  마지막 줄. 그 뒤의 명령은 다음 줄이 없어 실행되지 않고 버려진다.
public static class DemoScript
{
    public const string Source = @"
// l01
<<show A left 0.6>>
<<show B right 0.6>>
<<pause 1.2>>
A: This line opened with a 1.2s pause. Advance is refused until it ends - try Space right away. #line:l01

// l02
<<move A center 3>>
B: A is crossing for 3 seconds in the background. Advance now: the step boundary finishes it. #line:l02

// l03
<<face A smile>>
<<ambient B>>
A: B started a run-scoped idle loop. It lives across lines until the run ends. #line:l03

// l04
<<move A left 1.5 wait>>
B: That move was waited on. Press R: the run restarts from the root and seeks back to l03. #line:l04

// l05
<<hide A 0.8>>
<<face B serious>>
B: Hold Ctrl to fast-forward. Compressed commands land on their final state at once. #line:l05

// l06
<<show A center 0.6>>
<<move B left 2>>
<<pause 1.5>>
<<face A surprised>>
A: Within 1.5s, hold Ctrl or press Skip. The next step starts before this one is done - read the audit. #line:l06

// l07
<<face A smile>>
A: Skip runs to the end of the node; Cancel stops the session wherever it is. #line:l07

// l08
<<hide A 0.6>>
<<move B center 1>>
B: End of the node. #line:l08

// 줄 없이 끝나는 명령 - 다음 줄이 없어 재생되지 않고 버려진다.
<<face B smile>>
";
}
