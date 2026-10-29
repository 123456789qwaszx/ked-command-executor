using System.Collections.Generic;
using UnityEngine;

// 화면에 그릴 장부. 실행기는 무엇이 돌고 있는지 밖에 알리지 않는다 — 지나간 쪽이 각자 적는다:
//   팩토리 → Register(Pending) / 커맨드 → Enter · Complete · Skip · Finish / 호스트 → CancelUnfinished
// 수명(Wait · Step · Run)은 팩토리가 커맨드를 만들 때 정해 적는다.
public sealed class MiniTrace
{
    public enum Lifetime { Wait, Step, Run }

    public enum CommandState { Pending, Running, Completed, Skipped, Finished, Cancelled, NotEntered }

    public sealed class Entry
    {
        private readonly MiniTrace _owner;

        public string Label { get; }
        public Lifetime Lifetime { get; }
        public int Step { get; }
        public string StepLabel { get; }
        public CommandState State { get; private set; } = CommandState.Pending;
        public float StartTime { get; private set; } = -1f;
        public float PlannedDuration { get; private set; }

        // 무대가 이 커맨드의 위반을 한 번만 세도록 표시한다(MiniStage.Write).
        public bool FlaggedStale { get; set; }
        public bool FlaggedLate { get; set; }

        public bool IsRunning => State == CommandState.Running;
        public bool IsOver => State != CommandState.Pending && State != CommandState.Running;

        internal Entry(MiniTrace owner, string label, Lifetime lifetime, int step, string stepLabel)
        {
            _owner = owner;
            Label = label;
            Lifetime = lifetime;
            Step = step;
            StepLabel = stepLabel;
        }

        public float Progress =>
            !IsRunning ? 1f :
            PlannedDuration <= 0f ? 0f :
            Mathf.Clamp01((Time.time - StartTime) / PlannedDuration);

        public void Enter(float scaledDuration)
        {
            State = CommandState.Running;
            StartTime = Time.time;
            PlannedDuration = scaledDuration;
        }

        public void Complete() => End(CommandState.Completed, null);
        public void Skip() => End(CommandState.Skipped, null);

        // 스텝 경계(CleanupStep(Finish))나 런 경계에서 최종 상태로 보내졌다.
        public void Finish(string why) => End(CommandState.Finished, why);

        // 런 정리(Cancel)가 커맨드 자신의 정리 훅을 불렀다 — 런 수명 커맨드만 해당.
        public void Cancel(string why) => End(CommandState.Cancelled, why);

        internal void Interrupt()
        {
            if (State == CommandState.Running)
                End(CommandState.Cancelled, null);
            else if (State == CommandState.Pending)
                End(CommandState.NotEntered, null);
        }

        private void End(CommandState state, string why)
        {
            if (IsOver)
                return;

            State = state;

            if (why != null)
                _owner.Write($"[{StepLabel}] {Label}: {why}");
        }
    }

    private const int MaxLogLines = 9;

    private readonly List<Entry> _entries = new();
    private readonly List<string> _log = new();
    private readonly float _origin = Time.time;

    public IReadOnlyList<Entry> Entries => _entries;
    public IReadOnlyList<string> Log => _log;

    public int CurrentStep { get; private set; }
    public string CurrentStepLabel { get; private set; } = "-";

    public void BeginRun(int session, string reason)
    {
        _entries.Clear();
        CurrentStep = 0;
        CurrentStepLabel = "-";

        Write($"session #{session} start ({reason})");
    }

    public void BeginStep(string label, int specCount)
    {
        CurrentStep++;
        CurrentStepLabel = label;

        Write(specCount == 0
            ? $"[{label}] enter line - no commands"
            : $"[{label}] enter line - PlaySpecs({specCount})");
    }

    public Entry Register(string label, Lifetime lifetime)
    {
        var entry = new Entry(this, label, lifetime, CurrentStep, CurrentStepLabel);
        _entries.Add(entry);
        return entry;
    }

    // Stop(Cancel)은 대부분의 커맨드에 아무것도 알리지 않는다. 그 순간의 상태를 호스트가 대신 적는다.
    public void CancelUnfinished(string reason)
    {
        int running = 0, pending = 0;

        foreach (Entry entry in _entries)
        {
            if (entry.IsRunning) running++;
            else if (entry.State == CommandState.Pending) pending++;

            entry.Interrupt();
        }

        Write($"session end ({reason}) - Stop(Cancel): {running} running cut, {pending} never entered");
    }

    // 스텝 경계에서 메인 루틴이 아직 붙들고 있는 일이 있나 — 진행 중인 대기 명령이나, 아직 진입하지 못한 명령.
    // 배경 루틴은 여기 들지 않는다: 경계의 CleanupStep(Finish)가 끝내 준다. 런 수명은 원래 경계를 넘는다.
    public bool HasForegroundWorkInFlight(out string what)
    {
        foreach (Entry entry in _entries)
        {
            if (entry.Lifetime == Lifetime.Run)
                continue;

            if (entry.State == CommandState.Pending ||
                (entry.State == CommandState.Running && entry.Lifetime == Lifetime.Wait))
            {
                what = $"[{entry.StepLabel}] {entry.Label} ({entry.State})";
                return true;
            }
        }

        what = null;
        return false;
    }

    public void Write(string message)
    {
        _log.Add($"{Time.time - _origin,6:0.00}  {message}");

        if (_log.Count > MaxLogLines)
            _log.RemoveAt(0);
    }
}
