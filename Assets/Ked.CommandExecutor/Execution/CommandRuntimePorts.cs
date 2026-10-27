using System;
using System.Collections;

/// <summary>
/// Latches string-based signals and allows one-shot consumption.
/// </summary>
public interface ISignalLatch
{
    void Latch(string key); // Mark a signal as latched (idempotent).
    bool IsLatched(string key); // Returns true if the signal has been latched (non-consuming).
    bool Consume(string key);// Returns true only once per key; consumes the latched state.
    void Clear();
}

/// <summary>
/// One-shot "advance" input for the command system (click / key / tap).
/// Reading consumes the current press so it won't be processed twice.
/// </summary>
public interface IInputSource
{
    bool ConsumeAdvancePressed();
}

/// <summary>
/// Time source for gates/commands that use unscaled delta time.
/// </summary>
public interface ITimeSource
{
    float UnscaledDeltaTime { get; }
}

/// <summary>
/// Signal bus for commands (e.g., signal gates, RaiseSignal).
/// </summary>
public interface ISignalBus
{
    event Action<string> OnSignal;
    void Raise(string key);
}

/// <summary>
/// Applies a UI theme/locale patch to whatever the host considers the current screen.
/// The command system knows neither what the current screen is, nor how patching works —
/// the host resolves both.
/// </summary>
public interface IUIThemePatchPort
{
    IEnumerator PatchCurrentScreen(string themeId, string localeId);
}

/// <summary>
/// 실행기가 재생 정책에서 받는 둘 — 실행을 압축할지(트윈을 즉시 확정), 시간 배율.
/// VN의 자동·속도·빨리 넘기기·시크 깃발은 여기로 오지 않는다: 재생 쪽이 그 둘로 접어서 준다.
/// 커맨드가 실제로 묻는 것이 이 둘뿐이라, 실행기는 VN을 모른 채 돈다.
/// </summary>
public interface ICommandPacePolicy
{
    bool CompressExecution { get; }

    /// <summary>1이 보통. 1보다 크면 연출 길이를 그만큼 줄인다(ScalePresentationDuration).</summary>
    float TimeScale { get; }
}
