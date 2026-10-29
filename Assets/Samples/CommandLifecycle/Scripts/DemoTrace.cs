using System.Collections.Generic;
using UnityEngine;

// 데모 화면에 그릴 장부. 실행기의 일부가 아니다 — 실행기는 무엇이 돌고 있는지 바깥에 알리지 않는다.
// 그래서 지나간 쪽이 각자 적는다:
//   팩토리 → Register(Pending) / 커맨드 → Enter · Complete · Skip · FinishByStep / 데모 → CancelUnfinished
public sealed class DemoTrace
{
    public enum CommandState { Pending, Running, Completed, Skipped, FinishedByStep, Cancelled, NotEntered }

    public sealed class Entry
    {
        private readonly DemoTrace _owner;

        public string Label { get; }
        public bool Wait { get; }
        public int Step { get; }
        public CommandState State { get; private set; } = CommandState.Pending;

        // 런 시작 기준 초. 진입 전이면 -1.
        public float StartTime { get; private set; } = -1f;
        public float EndTime { get; private set; } = -1f;

        // 진입 시점의 배율(ScalePresentationDuration)이 반영된 길이.
        public float PlannedDuration { get; private set; }

        public bool IsRunning => State == CommandState.Running;
        public bool IsOver => State != CommandState.Pending && State != CommandState.Running;

        internal Entry(DemoTrace owner, string label, bool wait, int step)
        {
            _owner = owner;
            Label = label;
            Wait = wait;
            Step = step;
        }

        public void Enter(float scaledDuration)
        {
            State = CommandState.Running;
            StartTime = _owner.Now;
            PlannedDuration = scaledDuration;
            _owner.Write($"{Label} entered — {(Wait ? "main routine waits" : "background, step lifetime")}");
        }

        public void Complete() => End(CommandState.Completed, "completed");

        public void Skip()
        {
            StartTime = _owner.Now;
            End(CommandState.Skipped, "compressed — OnSkip wrote the final state");
        }

        public void FinishByStep() => End(CommandState.FinishedByStep, "OnStepLifetimeFinished — jumped to the final state");

        internal void Interrupt()
        {
            if (State == CommandState.Running)
                End(CommandState.Cancelled, "cancelled — left where it stopped");
            else if (State == CommandState.Pending)
                End(CommandState.NotEntered, "never entered");
        }

        private void End(CommandState state, string message)
        {
            if (IsOver)
                return;

            State = state;
            EndTime = _owner.Now;
            _owner.Write($"{Label} {message}");
        }
    }

    private const int MaxLogLines = 6;

    private readonly List<Entry> _entries = new();
    private readonly List<float> _stepTimes = new();
    private readonly List<string> _log = new();

    private float _runStartTime;

    public IReadOnlyList<Entry> Entries => _entries;
    public IReadOnlyList<float> StepTimes => _stepTimes;
    public IReadOnlyList<string> Log => _log;

    public string RunName { get; private set; }
    public int CurrentStep => _stepTimes.Count;
    public bool WasCancelled { get; private set; }

    public float Now => Time.time - _runStartTime;

    public void BeginRun(string name)
    {
        Clear();
        RunName = name;
        Write($"session.Start() — run '{name}'");
    }

    public void BeginStep()
    {
        _stepTimes.Add(Now);

        Write(CurrentStep == 1
            ? "step 1 — PlaySpecs"
            : $"step {CurrentStep} — PlaySpecs → CleanupStep(Finish) on step {CurrentStep - 1}");
    }

    public Entry Register(string label, bool wait)
    {
        var entry = new Entry(this, label, wait, CurrentStep);
        _entries.Add(entry);
        return entry;
    }

    // Stop(Cancel)은 커맨드를 부르지 않는다. 그 순간 돌던 것과 아직 못 들어간 것을 데모가 대신 적는다.
    public void CancelUnfinished()
    {
        WasCancelled = true;
        Write("session.End() — CommandExecutor.Stop(CleanupPolicy.Cancel)");

        foreach (Entry entry in _entries)
            entry.Interrupt();
    }

    public int CountRunning(bool wait)
    {
        int count = 0;

        foreach (Entry entry in _entries)
        {
            if (entry.IsRunning && entry.Wait == wait)
                count++;
        }

        return count;
    }

    public void Clear()
    {
        _entries.Clear();
        _stepTimes.Clear();
        _log.Clear();
        _runStartTime = Time.time;
        RunName = null;
        WasCancelled = false;
    }

    public void Write(string message)
    {
        _log.Add($"{Now,5:0.00}s  {message}");

        if (_log.Count > MaxLogLines)
            _log.RemoveAt(0);
    }
}
