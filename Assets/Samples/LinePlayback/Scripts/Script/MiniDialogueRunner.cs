using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Yarn LineCancellationToken 흉내 — 한 줄 동안의 두 요청(서두르기 · 다음 줄)과 정지.
public sealed class MiniLineToken
{
    public bool HurryUpRequested { get; private set; }
    public bool NextContentRequested { get; private set; }
    public bool IsCancelled { get; private set; }

    public void RequestHurryUp() => HurryUpRequested = true;

    // 다음 줄 요청은 서두르기를 겸한다(Yarn과 같다).
    public void RequestNextContent()
    {
        HurryUpRequested = true;
        NextContentRequested = true;
    }

    public void Cancel() => IsCancelled = true;
}

// Yarn DialoguePresenterBase.RunLineAsync 흉내. 줄 하나를 끝까지(다음 줄 요청까지) 맡는다.
public interface IMiniLinePresenter
{
    IEnumerator RunLine(MiniInstruction line, MiniLineToken token);
}

// Yarn DialogueRunner 흉내 — 노드 하나를 처음부터 끝까지 걷는다.
// 명령은 등록된 핸들러에 넘기고(동기), 대사는 프레젠터가 끝낼 때까지 기다린다.
// 러너는 실행기도 무대도 모른다. 정지는 세대(generation)를 올려 이전 걸음이 더 나아가지 못하게 한다.
public sealed class MiniDialogueRunner
{
    private readonly MonoBehaviour _host;
    private readonly IReadOnlyList<MiniInstruction> _script;
    private readonly IMiniLinePresenter _presenter;
    private readonly Dictionary<string, Action<string[]>> _handlers = new(StringComparer.Ordinal);

    private Coroutine _routine;
    private MiniLineToken _token;
    private int _generation;

    public bool IsRunning { get; private set; }

    // 노드 끝까지 갔다(정지로 끊긴 것은 아니다).
    public event Action NodeCompleted;

    public MiniDialogueRunner(MonoBehaviour host, IReadOnlyList<MiniInstruction> script, IMiniLinePresenter presenter)
    {
        _host = host;
        _script = script;
        _presenter = presenter;
    }

    public void AddCommandHandler(string name, Action<string[]> handler) => _handlers[name] = handler;

    public void StartNode()
    {
        Stop();

        IsRunning = true;
        _routine = _host.StartCoroutine(Run(++_generation));
    }

    public void Stop()
    {
        _generation++;

        _token?.Cancel();
        _token = null;

        if (_routine != null)
            _host.StopCoroutine(_routine);

        _routine = null;
        IsRunning = false;
    }

    public void RequestHurryUp() => _token?.RequestHurryUp();
    public void RequestNextLine() => _token?.RequestNextContent();

    private IEnumerator Run(int generation)
    {
        for (int i = 0; i < _script.Count; i++)
        {
            if (generation != _generation)
                yield break;

            MiniInstruction instruction = _script[i];

            if (instruction.Kind == MiniInstructionKind.Command)
            {
                Dispatch(instruction);
                continue;
            }

            var token = new MiniLineToken();
            _token = token;

            yield return _presenter.RunLine(instruction, token);

            if (generation != _generation || token.IsCancelled)
                yield break;

            _token = null;
        }

        _routine = null;
        IsRunning = false;
        NodeCompleted?.Invoke();
    }

    private void Dispatch(MiniInstruction command)
    {
        if (!_handlers.TryGetValue(command.Name, out Action<string[]> handler))
        {
            Debug.LogWarning($"[MiniDialogueRunner] Unknown command '<<{command.Name}>>' (script line {command.SourceLine}).");
            return;
        }

        try
        {
            handler(command.Args);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}
