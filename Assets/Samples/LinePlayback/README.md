# Line Playback Demo

> `ked-presentation-runtime`의 대사 재생 계층을 **구조 그대로** 옮겨, Yarn 대본이 명령과 줄을 내놓을 때 실행기의 생명주기가
> 롤백 · 빨리 넘기기 · 스킵 · 종료 속에서도 맞는지 매 줄 검사하며 보여 주는 샘플.

## 핵심 — 명령은 그 자리에서 실행되지 않는다

Yarn은 대본을 위에서부터 걸으며 `<<명령>>`과 대사 줄을 차례로 내놓는다. 이 구조는 명령을 즉시 실행하지 않는다.

```
<<place_center s1 bust 72fr>>   ─┐ YarnCommandBridge → 무대 파서 → 스펙 → YarnPlaybackDriver.Enqueue (쌓기만)
<<face s1 smile>>               ─┘ 러너에게는 즉시 끝난 명령 — 다음 내용으로 간다
하린: ...                        ── CustomLinePresenter.RunLineAsync → VNLinePresentationFlow.RunAsync
                                     LineReceived → 기록(백로그 · 롤백 좌표)
                                     → _playbackDriver.PlayCollected()   ← 여기서 쌓인 명령 전부가 이 줄의 스텝 하나로 재생
                                     → 시크 판정 → 대사창 → 타자기 → 넘김 대기
```

그래서 **줄 하나 = 스텝 하나**다. 다음 줄의 `PlayCollected`가 `CommandExecutor.PlaySpecs`를 부르는 순간, 앞 줄의 배경 명령은
`CleanupStep(Finish)`로 정리되고 새 줄의 명령이 진입한다. 롤백은 무대를 되돌리지 않는다 — 루트부터 다시 달리며, 표적 전 줄은
보이지 않고(`SeekPassThrough`) 그 명령들은 압축으로 최종 상태에 선다.

## 돌리기

`Scenes/LinePlaybackDemo.unity`를 열고 Play. 재생할 내용은 대본 파일이 아니라 코드(`Scripts/DemoScript.cs`)에 있다 —
한 묶음이 **명령 넷 + 대사 한 줄**이다.

```csharp
Cmd("place_center s1 bust 72fr"),
Cmd("place_right s2 bust 0fr"),
Cmd("fade_in s2 14fr"),
Cmd("face s2 smile"),
Say("l02", "시구레", "하린이 3초 동안 걸어가요. ..."),
```

| 키 / 버튼 | 원본에서 맡는 곳 | 하는 일 |
|---|---|---|
| `Space` Advance | `DialogueAdvanceDispatcher` · `AdvanceGate` | 다 안 보였으면 서두르기, 다 보였으면 다음 줄. 줄 앞머리 대기 명령이 도는 동안은 거절(`cps_node_busy`) |
| `Ctrl`(누르는 동안) Rapid skip | `RapidSkipController` | 빨리 넘기기 — **압축**(`PresentationSessionContext`). 관문의 "바쁨" 검사를 건너뛴다 |
| `S` Speed | `VNFeatureController` | 속도 모드 — 시간 배율 |
| `A` Auto | `AutoAdvanceScheduler` | 자동 진행 |
| `K` Skip | `EpisodeSkipController` | 노드 끝까지 — 압축이 **아니다**. 줄이 빨리 넘어가며 스텝 경계가 Finish로 끝낸다 |
| `R` Rollback | `VNFeatureController.RequestRollbackOneStep` → `NodePlaybackHost.RequestReplayAsync` | 한 줄 되감기 — 정지 → 루트부터 다시 → 시크 |
| BACKLOG jump | `VNFeatureController.RequestBacklogJump` | 백로그의 그 줄로 되감기 |
| `Esc` Exit | `NodePlaybackHost.ExitAsync` | 재생 종료(`Stop(CleanupPolicy.Cancel)`). 무대는 멈춘 자리에 남는다 |
| Restart | — | 처음부터. 감사 결과도 지운다 |

## 묶음마다 지나가는 상황

| 줄 | 명령 넷 | 상황 |
|---|---|---|
| l01 | `bgm` · `place_left` · `fade_in` · `24fr` | 줄 앞머리 명령 넷이 한 스텝으로. 대기 명령(`24fr`)이 끝날 때까지 넘기기 거절 |
| l02 | `place_center 72fr` · `place_right` · `fade_in` · `face` | 3초짜리 **배경** 명령 — 다 가기 전에 넘기면 다음 줄의 스텝 경계가 Finish로 끝낸다. BGM은 **런 수명**이라 계속 흐른다 |
| l03 | `face` · `place_left 36fr wait` · `fade_in` · `place_center` | **기다리는** 명령. 여기서 `R` → 루트부터 l02까지 압축 시크 |
| l04 | `fade_out` · `face` · `pause 3` · `face` | 글자가 다 나온 뒤 pause가 끝나기 전에 `Ctrl`/`K` → 아래 "드러나는 틈" |
| l05 | `face` · `face` · `place_right` · `12fr` | 감사 결과 확인 |
| l06 | `fade_out` · `fade_out` · `stop_bgm` · `24fr` | `#boxhide #auto` — 대사창 없이 퇴장 연출, 다 보이면 입력 없이 넘어가 노드가 끝난다 |

## 오른쪽 패널

- **LINE** — `VNLinePresentationFlow.CurrentPhase`를 정상 경로(`Received → Entered → Resolved → VisualRun → Surface → TypeReady → Typed → Displayed → Waiting → Completed`) 위에 칠한다. 시크 줄은 `SeekPassThrough`, 롤백 · 종료로 무너진 줄은 `Stale`
- **SCOPE** — 세션 번호, 스텝(=줄), 메인 루틴(`CommandIsPlaying`), 압축 · 배율 · 시크
- **COMMANDS** — 이 스텝의 명령과 아직 살아 있는 명령. 수명(`Wait` · `Step` · `Run`)과 상태
- **AUDIT** — 스텝 경계마다(`YarnPlaybackDriver.PlayCollected` → `LifecycleProbe`)
  - `settled == model` — 앞 스텝을 Finish로 정리한 무대 == 명령을 시간 없이 접은 순수 모델(`MiniStageModel`, 원본의 Core · 등가성 하네스 자리)
  - `replay == first` — 롤백 · 다시 시작으로 같은 줄을 다시 지났을 때 처음 본 무대와 같은가
  - `overlap` · `late write` · `stale write` — 겹친 스텝, 지나간 스텝의 쓰기, 끝난 세션의 쓰기
- **BACKLOG** — 백로그와 백점프

## 무엇을 옮겼나

`Scripts/Playback/` 아래는 원본 폴더 구조와 이름을 그대로 쓴다. 각 파일 머리에 원본과 **다른 곳**만 적어 두었다.

| 폴더 | 옮긴 것 | 원본과 다른 곳 |
|---|---|---|
| `DialoguePresenter/VNLinePresentationFlow/` | `CustomLinePresenter` · `VNLinePresentationFlow` · `VNLinePresentationPhase` · `VNLinePresentationContext` · `LinePresentationRun` · `VNLinePresentationState` · `VNSeekState` · `VNYarnLineBoundary` · `VNLineAdvanceTag` | `TMP_Text` → `DialogueTextView`, `EllipsisBreathTypewriter` → `DemoTypewriter`(같은 약속: 서두르기 = 이 줄 끝까지 가속, hard cancel = 즉시 멈춤) |
| `DialoguePresenter/BoxPresentationController/` | `DialogueBoxPresentationController` · 상태 · 태그 파서 | 화자 정책 · 레이아웃 프리셋 · box_hide/show 없음. 뷰는 IMGUI |
| `YarnBridge/` | `YarnPlaybackDriver` · `YarnCommandBridge` | 브리지는 명령별 등록 표 없이 `Forward` 하나로 받는다. 드라이버에 `[샘플]` 관측점 두 줄 |
| `FeatureController/` | `VNFeatureController` · `AdvanceGate` · `DialogueAdvanceDispatcher` · `RapidSkipController` · `EpisodeSkipController` · `AutoAdvanceScheduler` · `LineHurrySpeedController` · `RollbackHistory` · `BacklogRecorder` · `VNAdvanceInputPoller` · 설정 | 선택 기록 없음. `DOTween.unscaledTimeScale` → `PresentationClock` |
| `Host/` | `NodePlaybackHost` · `PlaybackUnit` · `ScenePresentation` · `YarnEpisodeNodeRunner` · `PresentationSessionContext` | 이어하기 · 저장/UI용 현재 줄 공급자 · 진행 호스트용 표면 · 화면 전환 · 샷 응답 없음. `ScenePresentation`에 `[샘플]` 관측점 |

그 아래를 받치는 것:

- `Scripts/MiniYarn/` — **Yarn Spinner의 최소 흉내.**
  - `DialogueRunner` — `DemoScript`의 내용을 차례로 건넨다. 명령은 브리지에 넘기고 바로 다음으로, 대사는 줄마다 다음 줄 · 서두르기 토큰을 만들어 프레젠터가 끝낼 때까지 기다린다. `Stop`은 대화를 취소하고 프레젠터의 `OnDialogueCompleteAsync`를 거쳐 돌아온다. 원본이 쓰는 표면(`StartDialogue` · `Stop` · `DialogueTask` · `RequestNextLine` · `RequestHurryUpLine` · `onNodeStart`)만 있다.
  - `LineCancellationToken`(서두르기 토큰이 다음 줄 토큰에 묶인다) · `LocalizedLine` · `DialoguePresenterBase`
  - `YarnTask` — Task 위의 awaitable. 옮겨 온 코드의 `async YarnTask`가 그대로 컴파일되도록 메서드 빌더를 붙였다.
- `Scripts/Stage/` — 무대 흉내(`MiniStage` — 슬롯 셋 + BGM, 모든 쓰기의 출처를 검사), 무대 파서(`MiniStageCommandParser` — `StageCommandParser` 자리, `14fr` · `2s` 시간 토큰), `PresentationClock`.
- `Scripts/Commands/` — 커맨드. `MiniCommandBase`의 세 문(`ExecuteInner` · `OnSkip` · `OnStepLifetimeFinished`)이 같은 최종 상태에 닿는다. BGM(`MiniBgmCommand`)은 런 수명 · `ExecuteEvenIfSkipping`이고, 런이 끝날 때 자기 흔적을 거둔다.
- `Scripts/Diagnostics/` — 장부 · 감사 · `LifecycleProbe`.

어셈블리 `LinePlayback.Sample`은 `Ked.CommandExecutor`만 참조한다. Yarn Spinner · DOTween · TextMeshPro를 쓰지 않는다.

## 드러나는 틈 — l04

원본의 `AdvanceGate`처럼 빨리 넘기기와 스킵은 "메인 루틴이 바쁨" 검사를 건너뛴다. l04의 `pause 3`이 도는 중에 다음 줄로 가면:

1. 다음 줄의 `PlayCollected` → `PlaySpecs`가 새 스텝을 연다. **앞 메인 루틴은 멈추지 않는다** — `PlaySpecs`는 스텝 수명(배경 루틴)만 정리하고 `_runId`도 올리지 않는다.
2. 감사: `overlap` +1. l04의 `face s3 surprised`는 아직 진입하지 못해 `settled == model`이 **FAIL**.
3. pause가 끝나면(빨리 넘기기 중이면 서두르기 배율로 더 빨리) 앞 메인 루틴이 이어서 `face s3 surprised`를 쓴다 → `late write` +1, 뒤 줄의 표정을 덮어써 그 줄도 **FAIL**.
4. 앞 `RunNode`의 `finally`는 `runId`가 같아 `scope.Token`을 `None`으로, `CommandIsPlaying`을 `false`로 되돌린다 — 새 스텝이 아직 돌고 있어도.

실행기 쪽에서 닫아야 할 틈이다. 이 샘플은 고치지 않고 재현과 판정만 한다.

## 알아 둘 것

- 화면은 IMGUI다. 대사는 기본 글꼴(OS 글꼴 대체)로 그린다. 한글이 네모로 보이면 `LinePlaybackDemo`의 **Dialogue Font**에 한글 글꼴을 넣는다.
- 입력은 `UnityEngine.Input`을 쓴다(원본 `VNAdvanceInputBindings`와 같다. 프로젝트 설정 Active Input Handling = Both).
- `YarnTask`의 이어짐은 Unity 메인 스레드(동기화 컨텍스트)로 돌아온다. 원본 코드의 "스택 탈출용 양보"(`CustomLinePresenter.OnDialogueCompleteAsync`, `YarnEpisodeNodeRunner`)는 그대로 두었다.
- 선택지 · 변수 · jump는 다루지 않는다 — 이 샘플은 명령과 줄의 생명주기만 본다.
