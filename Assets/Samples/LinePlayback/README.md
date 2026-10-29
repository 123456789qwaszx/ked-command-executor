# Line Playback Demo

> 대사 재생 계층(Yarn 흉내) 위에서 **롤백 · 빨리 감기 · 스킵 · 취소**가 섞여도 명령의 생명주기가 맞는지 — 그것을 매 줄 검사하며 보여 주는 샘플.

[Command Lifecycle Demo](../CommandLifecycle/README.md)가 실행기 하나의 동작(대기 · 병렬 · 스텝 정리 · 압축 · 취소)을 떼어 보여 준다면,
이 샘플은 그것들이 **실제 재생 흐름 안에서 서로 겹칠 때**를 다룬다. 구조는 `ked-presentation-runtime`의 재생 계층을 작게 옮겨 왔다.

캐릭터 연출은 일부러 단순하다(사각형 둘 — 위치 · 투명도 · 표정 · 대기 루프). 리그와 화면 연출은 `ked-stage-runtime`의 일이고,
여기서 보려는 것은 **그 상태에 누가 언제 썼는가, 정리된 뒤의 결과가 길과 상관없이 같은가**다.

## 돌리기

`Scenes/LinePlaybackDemo.unity`를 열고 Play. 노드가 처음부터 재생된다.

| 입력 | 동작 |
|---|---|
| `Space` / Advance | 다음 대사 — 다 안 보였으면 서두르기, 다 보였으면 다음 줄. 대기 명령이 도는 동안은 거절 |
| `Ctrl`(누르는 동안) / Fast-fwd | 빨리 감기 — **압축**. 진입하는 명령은 `OnSkip`으로 최종 상태 |
| Skip | 노드 끝까지 넘기기 — 압축이 **아니다**. 명령은 보통대로 진입하고 스텝 경계가 `Finish`로 끝낸다 |
| `R` / Rollback | 한 줄 되감기 — 세션을 닫고 루트부터 다시 달려 앞 줄까지 **시크**(압축 패스스루) |
| Cancel | 세션 정지(`Stop(CleanupPolicy.Cancel)`). 무대는 멈춘 자리에 남는다 |
| Restart | 처음부터. 감사 결과도 지운다 |
| Speed x4 | 시간 배율(`ICommandPacePolicy.TimeScale`) |

## 대본이 지나가는 상황

`Scripts/Script/DemoScript.cs` — 줄마다 생명주기 상황 하나.

| 줄 | 명령 | 상황 |
|---|---|---|
| l01 | show · show · `pause 1.2` | 줄 앞머리의 **대기 명령** — 메인 루틴이 바쁜 동안 넘기기 거절 |
| l02 | `move A center 3` | 긴 **배경 명령** — 끝나기 전에 넘기면 스텝 경계 `CleanupStep(Finish)`가 끝낸다 |
| l03 | face · `ambient B` | **런 수명** 명령 — 줄 경계를 넘어 살고, 런이 끝날 때 자기 정리 훅으로 거둔다 |
| l04 | `move A left 1.5 wait` | 기다리는 이동. 여기서 **롤백** → 루트부터 l03까지 압축 시크 |
| l05 | hide · face | **빨리 감기** — 압축 |
| l06 | show · move · `pause 1.5` · face | pause 도중 빨리 감기/스킵 → **다음 스텝이 앞 스텝보다 먼저 시작** (아래 "드러나는 틈") |
| l07 | face | 스킵 · 취소 |
| l08 | hide · move, 뒤에 `face` | 마지막 줄 뒤의 명령은 **다음 줄이 없어 버려진다** |

## 오른쪽 패널 — 실행기 안쪽

- **LINE** — 줄 좌표와 단계(`LineEntered → Typing → WaitingForAdvance → Completed`, 시크면 `SeekPassThrough`, 무너지면 `Stale`)
- **SCOPE** — 세션 번호, 스텝(=줄), 메인 루틴(`CommandIsPlaying`), 압축 · 배율
- **COMMANDS** — 이 스텝의 명령과 아직 살아 있는 명령. 수명(`Wait` · `Step` · `Run`)과 상태(`Running` · `Completed` · `Skipped` · `Finished` · `Cancelled` · `NotEntered`)
- **AUDIT** — 스텝 경계마다 판정한다(`MiniPlaybackDriver`)
  - `settled == model` — 앞 스텝을 `Finish`로 정리한 무대 사진이, 명령을 시간 없이 접은 순수 모델(`MiniStageModel`)과 같은가. 보통 재생 · 서두르기 · Finish · 압축 · 시크 어느 길로 왔든 같아야 한다
  - `replay == first` — 롤백이나 다시 시작으로 같은 줄을 다시 지났을 때, 처음 그 줄에서 본 무대와 같은가
  - `overlapping steps` — 앞 스텝의 메인 루틴이 대기 명령이나 미진입 명령을 붙든 채 다음 스텝이 시작됐다
  - `late writes` — 같은 세션이지만 이미 지나간 스텝의 명령이 무대에 썼다(런 수명 명령은 제외)
  - `stale writes` — 끝난 세션(이전 `CommandRunScope`)의 명령이 무대에 썼다. 롤백 · 취소 뒤에 살아남은 루틴

## 구조 — 무엇을 흉내 냈나

| 이 샘플 | 원본(`ked-presentation-runtime`) | 하는 일 |
|---|---|---|
| `MiniScript` · `MiniDialogueRunner` | Yarn 컴파일 결과 · `DialogueRunner` | 명령은 핸들러에, 대사는 프레젠터에. 정지는 세대로 끊는다 |
| `MiniCommandBridge` | `YarnCommandBridge` | 명령 토큰 → 스펙. 실행하지 않고 쌓는다 |
| `MiniPlaybackDriver` | `YarnPlaybackDriver` | 줄 진입 때 쌓인 스펙을 한 스텝으로 `PlaySpecs`. 경계에서 감사를 부른다 |
| `MiniLinePresenter` | `CustomLinePresenter` · `VNLinePresentationFlow` | 줄 트랜잭션 — 기록 → 스텝 재생 → 시크 판정 → 타자기 → 넘김 대기 |
| `MiniLineState` · `MiniRollbackHistory` | `VNLinePresentationState` · `VNSeekState` · `RollbackHistory` | 다 보였나, 시크 표적, 롤백 좌표(줄 ID + 등장 순번) |
| `MiniPlaybackControls` | `AdvanceGate` · `DialogueAdvanceDispatcher` · RapidSkip/EpisodeSkip | 넘기기 관문. 빨리 감기 · 스킵은 바쁨 검사를 건너뛴다(원본과 같다) |
| `MiniPacePolicy` | `PresentationSessionContext` | 빨리 감기 · 시크 → 압축, 속도 모드 → 시간 배율 |
| `LinePlaybackDemo` | `NodePlaybackHost` · `PlaybackUnit` · `ScenePresentation` | 런 수명 — 열기 · 정지(정지 순서도 원본과 같다) · 루트부터 다시 |
| `MiniStage` · `MiniStageModel` · `MiniLifecycleAudit` | 무대 상태 · Core(Reduce) · `StageEquivalenceHarness` | 쓰기 출처 검사, 순수 모델과의 등가성 판정 |

어셈블리 `LinePlayback.Sample`은 `Ked.CommandExecutor`만 참조한다. Yarn · DOTween · TextMeshPro를 쓰지 않는다.

## 커맨드가 지키는 것

`Commands/MiniCommandBase.cs` — 세 문(`ExecuteInner` · `OnSkip` · `OnStepLifetimeFinished`)이 같은 `Commit`에 닿는다. 그래서 감사가 길과 상관없이 같은 답을 기대할 수 있다.

- 루프는 매 프레임 `scope.IsCancelled`와 자기 상태를 본다 — `StopCoroutine`이 중첩된 본문까지 멈추는지에 기대지 않는다.
- `OnStepLifetimeFinished`는 자연 완료 뒤에도 다음 경계에서 한 번 더 불린다 — 끝난 커맨드는 거른다.
- 런 수명 커맨드(`MiniAmbientCommand`)는 `RegisterRunLifetime`을 덮어 정리 때 자기 흔적(깃발 · 출렁임)을 거둔다. 기본 구현은 루틴만 멈춘다.
- 지속되는 상태를 만드는 런 수명 커맨드는 `SkipPolicy.ExecuteEvenIfSkipping` — 압축 시크 중에 건너뛰면 롤백 재현이 깨진다.

## 드러나는 틈 — l06

빨리 감기와 스킵은 원본의 `AdvanceGate`처럼 "메인 루틴이 바쁨" 검사를 건너뛴다. l06의 `pause 1.5`가 도는 중에 빨리 감거나 스킵하면:

1. 다음 줄이 들어오며 `PlaySpecs`가 새 스텝을 연다. **앞 메인 루틴은 멈추지 않는다** — `PlaySpecs`는 스텝 수명(배경 루틴)만 정리하고, `_runId`도 올리지 않는다.
2. 감사: `overlapping steps` +1. l06의 `face A surprised`는 아직 진입하지 못해 `settled == model`이 **FAIL**.
3. pause가 끝나면 앞 메인 루틴이 이어서 `face A surprised`를 쓴다 → `late writes` +1, 뒤 줄의 표정을 덮어써 그 줄도 **FAIL**.
4. 앞 `RunNode`의 `finally`는 `runId`가 같아 `scope.Token`을 `None`으로, `CommandIsPlaying`을 `false`로 되돌린다 — 새 스텝이 아직 돌고 있어도.

실행기 쪽에서 닫아야 할 틈이다(새 스텝을 열기 전에 앞 메인 루틴을 끝내는 규칙 — 남은 명령을 압축 완료할지, 진행 중인 대기 명령을 어떻게 마무리할지). 이 샘플은 그것을 고치지 않고 재현과 판정만 한다.

## 알아 둘 것

- 화면은 IMGUI다. 기본 폰트에서 깨지지 않도록 게임 화면 글자는 영어 · ASCII로만 쓴다.
- 입력은 `UnityEngine.Input`을 쓴다(프로젝트 설정 Active Input Handling = Both).
- 시크 패스스루 줄은 한 프레임 안에 여러 개가 지나갈 수 있다. 명령은 `SequencePlayer`가 동기로 진입시키므로(압축이면 `OnSkip`이 그 자리에서 끝난다) 순서가 섞이지 않는다 — 섞이면 감사가 잡는다.
