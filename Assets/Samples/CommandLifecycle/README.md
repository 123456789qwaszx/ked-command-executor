# Command Lifecycle Demo

> 실행기는 커맨드가 **무엇을 하는지** 몰라도 **순서 · 대기 · 수명 · 취소 · 압축**을 관리한다 — 그것을 눈으로 보는 샘플.

캐릭터도 대사창도 없다. 사각형 두 개(A, B)와, 그 옆에 실행기 안쪽 상태(지금 도는 커맨드, 스텝, 타임라인, 이벤트)를 함께 그린다.

## 돌리기

`Scenes/CommandLifecycleDemo.unity`를 열고 Play. 시작하자마자 Sequential이 한 번 돈다.
씬에는 카메라와 `CommandExecutor` + `DemoController`가 붙은 오브젝트 하나뿐이고, 도형과 UI(IMGUI)는 코드가 만든다.

## 시나리오

| 버튼 | 무엇을 하나 | 보여 주는 것 |
|---|---|---|
| Sequential | Move(A, wait) → Fade(B, wait) | `WaitForCompletion` — SequencePlayer가 A를 끝까지 기다린 뒤 B에 진입 |
| Parallel | Move(A, bg) · Fade(B, bg) | 비대기 커맨드 — 진입만 시키고 넘어간다. 배경 루틴은 **Step** 수명에 묶인다 |
| Step Cleanup | 스텝 1: 5초 Move(A, bg) → 1초 뒤 스텝 2 | `PlaySpecs`가 먼저 `CleanupStep(Finish)` → `OnStepLifetimeFinished`가 A를 목표로 보낸다 |
| Compress | `ICommandPacePolicy.CompressExecution` 토글 | 다음에 진입하는 커맨드부터 `SkipPolicy.CompleteImmediately` → `OnSkip` — 기다림 없이 최종 상태 |
| Cancel | `session.End()` → `Stop(CleanupPolicy.Cancel)` | **완료가 아니라 중단.** 훅이 불리지 않고, 못 들어간 커맨드는 끝내 돌지 않는다 |
| Speed 1x/2x/4x | `ICommandPacePolicy.TimeScale` | 진입 시점에 `ScalePresentationDuration`으로 한 번 읽힌다 |

Sequential 도중에 Cancel을 누른 결과와, Compress를 켜고 Sequential을 돌린 결과를 나란히 보면 둘의 차이가 드러난다 — 압축은 최종 상태로 **완료**하고, 취소는 그 자리에서 **멈춘다**.

## 구조

```
DemoController ── 스펙(DemoMoveSpec · DemoFadeSpec)을 만든다. 커맨드는 모른다.
      │
      ▼  executor.PlaySpecs(specs, session.CurrentScope)
CommandExecutor ── CompositeCommandFactory ── DemoCommandFactory ── 스펙 → 커맨드 (+ 대상 도형, wait)
      │
      ▼
SequencePlayer ── 진입(첫 MoveNext) → wait면 기다리고, 아니면 배경 루틴으로 돌려 수명에 묶는다
      │
      ▼
CommandRunScope ── StepLifetime · RunLifetime · IsCancelled · 압축 · 배율  ←  DemoPacePolicy
```

| 파일 | 역할 |
|---|---|
| `Scripts/DemoController.cs` | 실행기를 쓰는 바깥(재생) 자리. 초기화 · 세션 · 시나리오 · 화면 |
| `Scripts/DemoPacePolicy.cs` | `ICommandPacePolicy` — 버튼이 바꾸는 압축 · 배율 |
| `Scripts/DemoCommandFactory.cs` | `INodeCommandFactory` — 대상 도형을 찾아 생성자로 넘기고, `CommandSpecBase.wait`를 `WaitForCompletion`으로 옮긴다 |
| `Scripts/Commands/DemoMoveCommand.cs` · `DemoFadeCommand.cs` | `CommandBase`를 바로 상속. `ExecuteInner` · `OnSkip` · `OnStepLifetimeFinished` 세 문이 같은 최종 상태로 모인다 |
| `Scripts/DemoStage.cs` | 이 샘플의 "무대" — 도형을 만들고 이름으로 찾아 준다. 실행기는 모른다 |
| `Scripts/DemoTrace.cs` | 화면용 장부. 실행기는 무엇이 도는지 밖에 알리지 않으므로 팩토리·커맨드·데모가 각자 적는다 |

어셈블리 `CommandLifecycle.Sample`은 `Ked.CommandExecutor`만 참조한다 — 실행기의 공개 API만으로 돈다는 뜻이다. DOTween도 쓰지 않는다.

## 샘플이 일부러 드러내는 것

- **`wait`는 자동으로 이어지지 않는다.** `CommandSpecBase.wait`를 커맨드의 `WaitForCompletion`으로 옮기는 것은 팩토리 몫이다.
- **`CommandBase`는 스텝 경계에서 아무것도 마무리하지 않는다.** 루틴을 멈출 뿐이다. 최종 상태로 보내려면 `OnStepLifetimeFinished`를 구현해야 하고, 그 훅은 자연 완료 뒤에도 다음 경계에서 한 번 더 불린다(그래서 두 데모 커맨드는 `_running`으로 거른다).
- **Cancel은 커맨드에 알리지 않는다.** 무엇을 남길지(멈춘 자리 / 되돌리기)는 커맨드가 정한다. 이 샘플은 멈춘 자리에 둔다.
- **취소된 `CommandRunScope`는 다시 쓰지 않는다.** `IsCancelled`가 남으므로 시나리오마다 `session.End()` → `session.Start()`로 새 스코프를 연다.

## 알려진 경계 (샘플이 피해 가는 곳)

- **실행 중 `PlaySpecs` 재호출.** `PlaySpecs`는 배경 루틴만 스텝 수명으로 정리하고, 앞 메인 루틴(대기 중인 커맨드)은 멈추지 않는다. Step Cleanup 시나리오는 `scope.CommandIsPlaying`이 내려간 뒤에 다음 스텝으로 넘어간다. 겹침 자체는 별도 테스트로 다룰 일이다.
- **중첩 코루틴 정지.** 커맨드 본문은 `yield return inner`로 중첩되어 돈다. `StopCoroutine`이 중첩까지 멈춘다는 데 기대지 않도록 데모 커맨드는 매 프레임 `scope.IsCancelled`와 자기 `_running`을 직접 본다.
- **트윈 취소.** `ClaimTweenCommandBase`의 기본 Cancel 경로는 코루틴만 멈추고 트윈을 죽이지 않는다. 이 샘플은 트윈을 쓰지 않으므로 이 문제를 비켜 간다 — 트윈 기반 샘플을 붙일 때 확인할 곳이다.
