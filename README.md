# ked-command-executor

> 연출 커맨드 하나의 **수명**을 돌리는 실행기. 커맨드가 무엇을 하는지(무대)도, 누가 언제 시키는지(재생)도 모른다.

`ked-presentation-runtime`에서 떼어 낸 세 조각 중 하나다 — 실행기 · 무대([ked-stage-runtime](https://github.com/123456789qwaszx/ked-stage-runtime)) · 재생([ked-presentation-runtime](https://github.com/123456789qwaszx/ked-presentation-runtime)).
가른 근거는 재생 저장소의 `docs/PLAN_stage-seam.md`에 있다.

## 무엇이 있나

`Assets/Ked.CommandExecutor/` — 어셈블리 `Ked.CommandExecutor` (참조: `DOTween.Modules`)

| | 무엇 |
|---|---|
| `Base/` | `CommandBase`(스텝 수명 기본) · `ClaimTweenCommandBase`(ResolveTargets → ClaimTarget → 트윈 → Commit / Skip / 스텝 경계 가속) · `CanvasFadeCommandBase` · `CommandSpecBase` · `INodeCommandFactory` |
| `Execution/` | `CommandExecutor`(MonoBehaviour 호스트) · `SequencePlayer` · `ISequenceCommand` · `SkipPolicy` · 포트(`CommandRuntimePorts.cs`) |
| 루트 | `CommandRunScope`(스텝/런 수명 · 취소 · 압축 · 시간 배율) · `LifetimeScope` · `PresentationScopeSession` · `CompositeCommandFactory` · `SignalLatch` · Unity 포트 구현(`UnityInputSource` · `UnityTimeSource` · `UnitySignalBus`) |

## 무엇을 모르나

- **무대를 모른다.** 리그·배경·cast·shot 타입이 여기 없다. 무대가 필요한 커맨드는 생성자로 받는다 — 팩토리가 넘긴다.
- **재생을 모른다.** 자동·속도·빨리 넘기기·시크 같은 VN 깃발이 없다. 실행기가 재생에게 묻는 것은 `ICommandPacePolicy` 둘뿐이다 — 실행을 **압축**할지, **시간 배율**.
- **Yarn을 모른다.** 대본 문법은 무대의 `StageCommandParser`가 안다.

밖에 요구할 것이 생기면 `Execution/CommandRuntimePorts.cs`에 인터페이스를 두고, 구현은 바깥이 갖는다(`IInputSource` · `ITimeSource` · `ISignalBus` · `ISignalLatch` · `IUIThemePatchPort` · `ICommandPacePolicy`).

## 어떻게 쓰나

1. `CommandSpecBase`를 상속한 스펙과, `CommandBase`(또는 `ClaimTweenCommandBase`)를 상속한 커맨드를 만든다.
2. `INodeCommandFactory`로 스펙 → 커맨드를 잇고, `CompositeCommandFactory`로 묶어 `CommandExecutor.Initialize`에 준다.
3. `PresentationScopeSession.Start()`로 `CommandRunScope`를 열고, `CommandExecutor.PlaySpecs(specs, scope)`로 돌린다. 줄 경계에서 `scope.CleanupStep`, 세션 끝에 `Session.End()`.

## 샘플

[Line Playback Demo](Assets/Samples/LinePlayback/README.md) — `Assets/Samples/LinePlayback/Scenes/LinePlaybackDemo.unity`

`ked-presentation-runtime`의 재생 계층(`CustomLinePresenter` · `VNLinePresentationFlow` · `YarnPlaybackDriver` · `YarnCommandBridge` · 재생 제어 · 호스트)을
구조 그대로 옮겨, 최소한의 Yarn 흉내(`MiniYarn`) 위에서 돌린다. 명령은 쌓였다가 다음 줄에서 한 스텝으로 재생되고,
롤백 · 빨리 넘기기 · 스킵 · 종료 속에서 스텝 경계마다 무대를 순수 모델과 맞춰 본다.

실행기의 공개 API만 쓰고(어셈블리 `LinePlayback.Sample` → `Ked.CommandExecutor`), 복사 반입 대상이 아니다 — 반입은 `Assets/Ked.CommandExecutor/`만 가져간다.

## 다른 저장소로 가져가기

패키지가 아니다. **복사 반입**이다 — `Assets/Ked.CommandExecutor/`를 `.meta`까지 통째로 복사하고, 반입한 쪽의 `Documentation~/vendoring.md`에 원본 커밋을 적는다.
스크립트 GUID가 같아야 씬·프리팹의 `CommandExecutor` 참조가 이어진다. 여기가 원본이다. 사본에서 먼저 고쳤으면 여기에도 같은 손을 댄다.

## 의존

- Unity `6000.3.16f1` (`ProjectSettings/ProjectVersion.txt`)
- DOTween (`Assets/Plugins/Demigiant/`, `DOTween.Modules.asmdef` 포함 — DOTween을 업데이트하면 이 asmdef가 지워질 수 있다. DOTween 유틸리티 패널의 "Create ASMDEF"로 같은 이름으로 다시 만든다)
