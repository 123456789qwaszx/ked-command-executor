using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Every command is guaranteed one execution entry (first MoveNext);
// ticket.MarkCommandEntered() records the entry right after.
public sealed class SequencePlayer
{
    private readonly MonoBehaviour _host;

    public SequencePlayer(MonoBehaviour host)
    {
        _host = host;
    }

    public IEnumerator PlayCommands(
        IReadOnlyList<ISequenceCommand> commands, CommandRunScope scope, Func<bool> isValid)
    {
        int total = commands.Count;

        for (int i = 0; i < total; i++)
        {
            ISequenceCommand command = commands[i];
            IEnumerator routine = command.Execute(scope);

            bool hasMore = routine.MoveNext();
            object firstYield = hasMore 
                ? routine.Current 
                : null;
            
            if (!hasMore)
                continue;

            if (command.WaitForCompletion && !scope.ShouldCompressCommandExecution)
            {
                yield return RunAfterFirstYield(routine, firstYield, scope, isValid);
            }
            else
            {
                IEnumerator wrappedRoutine = RunAfterFirstYield(routine, firstYield, scope, isValid);
                BindBackgroundLifetime(command, scope, wrappedRoutine);
                _host.StartCoroutine(wrappedRoutine);
            }
        }
    }

    private static IEnumerator RunAfterFirstYield(
        IEnumerator routine, 
        object firstYield,
        CommandRunScope scope,
        Func<bool> isValid)
    {
        if (isValid() && !scope.Token.IsCancellationRequested)
            yield return firstYield;
        
        while (isValid() && !scope.Token.IsCancellationRequested)
        {
            bool movedNext;

            try
            {
                movedNext = routine.MoveNext();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                yield break;
            }

            if (!movedNext)
                yield break;

            yield return routine.Current;
        }
    }
    
    // Binds a background routine to its cleanup lifetime.
    // Run is matched first, so a command that opts into IRunScopedCommand overrides the step-scoped default.
    private void BindBackgroundLifetime(ISequenceCommand command, CommandRunScope scope, IEnumerator routine)
    {
        switch (command)
        {
            case IRunScopedCommand runScoped:
                runScoped.RegisterRunLifetime(scope, _host, routine);
                break;

            case IStepScopedCommand stepScoped:
                stepScoped.RegisterStepLifetime(scope, _host, routine);
                break;

            default:
                Debug.LogWarning(
                    $"[SequencePlayer] Background command '{command.GetType().Name}' implements no " +
                    $"lifetime scope. Derive from CommandBase, or implement IStepScopedCommand / " +
                    $"IRunScopedCommand. Binding to StepLifetime as a fallback.");

                scope.TrackStep(
                    cancel: () => _host.StopCoroutine(routine),
                    finish: () => _host.StopCoroutine(routine));
                break;
        }
    }
}