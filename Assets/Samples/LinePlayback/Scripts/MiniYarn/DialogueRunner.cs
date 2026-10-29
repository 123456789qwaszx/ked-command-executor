using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace MiniYarn
{
    // Yarn Spinner의 DialogueRunner를 이 샘플에 필요한 만큼 옮긴 것.
    //
    // 남긴 것은 원본과 같은 흐름이다:
    //   - VM이 명령을 주면 디스패처로 부르고, 동기로 끝났으면 SignalContentComplete, 아니면 기다린 뒤 Continue.
    //   - VM이 줄을 주면 줄마다 취소 소스(다음 줄)와 그에 묶인 서두르기 소스를 만들고, 모든 프레젠터의
    //     RunLineAsync를 기다린 뒤 Continue. 전부 동기로 끝났으면 SignalContentComplete.
    //   - Stop은 현재 줄과 대화를 취소하고 VM을 멈춘 뒤, 프레젠터들의 OnDialogueCompleteAsync가 끝날 때까지 기다린다.
    //
    // 뺀 것: YarnProject · 변수 저장소 · 로컬라이제이션(줄 공급자는 동기) · 선택지 · 세일리언시.
    public sealed class DialogueRunner : MonoBehaviour
    {
        private Dialogue dialogue;

        public Dialogue Dialogue
        {
            get
            {
                if (dialogue == null)
                {
                    dialogue = new Dialogue
                    {
                        LineHandler = OnLineReceived,
                        CommandHandler = OnCommandReceived,
                        NodeStartHandler = OnNodeStarted,
                        NodeCompleteHandler = OnNodeCompleted,
                        DialogueCompleteHandler = OnDialogueCompleted,
                    };

                    if (program != null)
                        dialogue.SetProgram(program);
                }

                return dialogue;
            }
        }

        private YarnProgram program;

        [SerializeField] private List<DialoguePresenterBase> dialoguePresenters = new();

        public bool verboseLogging;

        public bool IsDialogueRunning => Dialogue.IsActive;

        public UnityEventString onNodeStart = new();
        public UnityEventString onNodeComplete = new();
        public UnityEvent onDialogueStart = new();
        public UnityEvent onDialogueComplete = new();

        public IEnumerable<DialoguePresenterBase> DialoguePresenters
        {
            get => dialoguePresenters;
            set => dialoguePresenters = new List<DialoguePresenterBase>(value);
        }

        private CancellationTokenSource dialogueCancellationSource;
        private CancellationTokenSource currentLineCancellationSource;
        private CancellationTokenSource currentLineHurryUpSource;

        private YarnTaskCompletionSource dialogueCompletionSource;
        private YarnTaskCompletionSource dialogueCancellationCompletion;

        private readonly Dictionary<string, Delegate> commandHandlers = new(StringComparer.Ordinal);

        public void SetProgram(YarnProgram yarnProgram)
        {
            if (IsDialogueRunning)
                throw new InvalidOperationException("Can't set program, because dialogue is currently running.");

            program = yarnProgram;
            Dialogue.SetProgram(yarnProgram);
        }

        // ───────────────────────────── 명령 등록 ─────────────────────────────

        public void AddCommandHandler(string commandName, Action handler) => AddCommandHandler(commandName, (Delegate)handler);
        public void AddCommandHandler(string commandName, Func<YarnTask> handler) => AddCommandHandler(commandName, (Delegate)handler);
        public void AddCommandHandler<T1>(string commandName, Action<T1> handler) => AddCommandHandler(commandName, (Delegate)handler);
        public void AddCommandHandler<T1, T2>(string commandName, Action<T1, T2> handler) => AddCommandHandler(commandName, (Delegate)handler);
        public void AddCommandHandler<T1, T2, T3>(string commandName, Action<T1, T2, T3> handler) => AddCommandHandler(commandName, (Delegate)handler);
        public void AddCommandHandler<T1, T2, T3, T4>(string commandName, Action<T1, T2, T3, T4> handler) => AddCommandHandler(commandName, (Delegate)handler);
        public void AddCommandHandler<T1, T2, T3, T4, T5>(string commandName, Action<T1, T2, T3, T4, T5> handler) => AddCommandHandler(commandName, (Delegate)handler);

        public void AddCommandHandler(string commandName, Delegate handler)
        {
            if (commandHandlers.ContainsKey(commandName))
            {
                Debug.LogError($"Cannot add a command handler for {commandName}: one already exists");
                return;
            }

            commandHandlers.Add(commandName, handler);
        }

        // ───────────────────────────── 수명 ─────────────────────────────

        public async YarnTask Stop()
        {
            dialogueCancellationCompletion = new YarnTaskCompletionSource();
            CancelDialogue();
            await dialogueCancellationCompletion.Task;
            dialogueCancellationCompletion = null;
        }

        private void OnDestroy()
        {
            CancelDialogue();
        }

        public YarnTask DialogueTask => dialogueCompletionSource == null
            ? YarnTask.CompletedTask
            : dialogueCompletionSource.Task;

        private void CancelDialogue()
        {
            if (dialogueCancellationSource == null || Dialogue.IsActive == false)
                return;

            currentLineCancellationSource?.Cancel();
            dialogueCancellationSource?.Cancel();

            // VM을 멈춘다. OnDialogueCompleted가 불린다.
            Dialogue.Stop();
        }

        private void OnDialogueCompleted()
        {
            OnDialogueCompleteAsync().Forget();
        }

        private async YarnTask OnDialogueCompleteAsync()
        {
            currentLineCancellationSource?.Dispose();
            currentLineCancellationSource = null;
            currentLineHurryUpSource?.Dispose();
            currentLineHurryUpSource = null;

            var pendingTasks = new List<YarnTask>();

            foreach (DialoguePresenterBase view in dialoguePresenters)
            {
                if (view == null)
                    continue;

                async YarnTask RunCompletion()
                {
                    try
                    {
                        await view.OnDialogueCompleteAsync();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, view);
                    }
                }

                pendingTasks.Add(RunCompletion());
            }

            await YarnTask.WhenAll(pendingTasks);

            dialogueCompletionSource?.TrySetResult();
            onDialogueComplete?.Invoke();

            dialogueCancellationSource?.Dispose();
            dialogueCancellationSource = null;
            dialogueCompletionSource = null;

            dialogueCancellationCompletion?.TrySetResult();
        }

        private void OnNodeCompleted(string completedNodeName)
        {
            onNodeComplete?.Invoke(completedNodeName);

            foreach (DialoguePresenterBase presenter in dialoguePresenters)
            {
                if (presenter != null && presenter.enabled)
                    presenter.OnNodeExit(completedNodeName);
            }
        }

        private void OnNodeStarted(string startedNodeName)
        {
            onNodeStart?.Invoke(startedNodeName);

            foreach (DialoguePresenterBase presenter in dialoguePresenters)
            {
                if (presenter != null && presenter.enabled)
                    presenter.OnNodeEnter(startedNodeName);
            }
        }

        // ───────────────────────────── 명령 ─────────────────────────────

        private void OnCommandReceived(Command command)
        {
            OnCommandReceivedAsync(command).Forget();
        }

        private async YarnTask OnCommandReceivedAsync(Command command)
        {
            string[] parts = SplitCommandText(command.Text);
            string commandName = parts.Length > 0 ? parts[0] : string.Empty;

            if (!commandHandlers.TryGetValue(commandName, out Delegate handler))
            {
                Debug.LogError($"No Command \"{commandName}\" was found. Did you remember to use AddCommandHandler()?");
                return;
            }

            if (!TryDispatch(handler, parts, out YarnTask task, out string error))
            {
                Debug.LogError($"Can't call command <<{command.Text}>>: {error}");
                return;
            }

            if (task.IsCompletedSuccessfully())
            {
                // 동기로 끝났다.
                Dialogue.SignalContentComplete();
            }
            else
            {
                // 아직 돌고 있다(box_hide 같은 즉시 명령의 페이드). 끝날 때까지 기다린다.
                await task;
            }

            if (dialogueCancellationSource?.IsCancellationRequested ?? false)
                return;

            Dialogue.Continue();
        }

        // 인자는 매개변수 자리에 맞춰 넣는다. 모자라면 기본값(없으면 null), 넘치면 오류.
        private static bool TryDispatch(Delegate handler, string[] parts, out YarnTask task, out string error)
        {
            task = YarnTask.CompletedTask;
            error = null;

            ParameterInfo[] parameters = handler.Method.GetParameters();
            int argCount = parts.Length - 1;

            if (argCount > parameters.Length)
            {
                error = $"expected at most {parameters.Length} parameter(s), got {argCount}";
                return false;
            }

            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < argCount)
                {
                    if (!TryConvert(parts[i + 1], parameters[i].ParameterType, out args[i]))
                    {
                        error = $"can't convert '{parts[i + 1]}' to {parameters[i].ParameterType.Name}";
                        return false;
                    }
                }
                else
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                }
            }

            try
            {
                object result = handler.DynamicInvoke(args);

                if (result is YarnTask yarnTask)
                    task = yarnTask;
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e);
            }

            return true;
        }

        private static bool TryConvert(string token, Type type, out object value)
        {
            value = null;

            if (type == typeof(string))
            {
                value = token;
                return true;
            }

            try
            {
                value = Convert.ChangeType(token, type, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string[] SplitCommandText(string text)
        {
            var parts = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;

            foreach (char c in text ?? string.Empty)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        parts.Add(sb.ToString());
                        sb.Clear();
                    }

                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
                parts.Add(sb.ToString());

            return parts.ToArray();
        }

        // ───────────────────────────── 줄 ─────────────────────────────

        private void OnLineReceived(Line line)
        {
            OnLineReceivedAsync(line).Forget();
        }

        private async YarnTask OnLineReceivedAsync(Line line)
        {
            // 원본은 줄 공급자를 비동기로 기다린다. 이 샘플의 공급자는 동기다.
            var localisedLine = new LocalizedLine(line);

            await RunLocalisedLine(localisedLine);

            if (dialogueCancellationSource?.IsCancellationRequested == false)
                Dialogue.Continue();
        }

        private async YarnTask RunLocalisedLine(LocalizedLine localisedLine)
        {
            currentLineCancellationSource?.Dispose();
            currentLineHurryUpSource?.Dispose();

            currentLineCancellationSource = dialogueCancellationSource != null
                ? CancellationTokenSource.CreateLinkedTokenSource(dialogueCancellationSource.Token)
                : new CancellationTokenSource();

            // 다음 줄 토큰에 묶인 서두르기 토큰.
            currentLineHurryUpSource = CancellationTokenSource.CreateLinkedTokenSource(currentLineCancellationSource.Token);

            var metaToken = new LineCancellationToken
            {
                NextContentToken = currentLineCancellationSource.Token,
                HurryUpToken = currentLineHurryUpSource.Token,
            };

            var pendingTasks = new List<YarnTask>();

            foreach (DialoguePresenterBase view in dialoguePresenters)
            {
                if (view == null || view.enabled == false)
                    continue;

                async YarnTask RunLineAndInvokeCompletion(DialoguePresenterBase presenter, LocalizedLine line, LineCancellationToken token)
                {
                    try
                    {
                        await presenter.RunLineAsync(line, token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning($"Dialogue presenter {presenter.name} threw an OperationCanceledException from RunLineAsync. Presenters should clean up and return instead.", presenter);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, presenter);
                    }
                }

                pendingTasks.Add(RunLineAndInvokeCompletion(view, localisedLine, metaToken));
            }

            YarnTask waitForAllLines = YarnTask.WhenAll(pendingTasks);

            if (waitForAllLines.IsCompletedSuccessfully())
            {
                // 모든 프레젠터가 동기로 끝냈다(시크 패스스루).
                Dialogue.SignalContentComplete();
            }
            else
            {
                await waitForAllLines;
            }

            currentLineCancellationSource?.Dispose();
            currentLineCancellationSource = null;

            currentLineHurryUpSource?.Dispose();
            currentLineHurryUpSource = null;
        }

        // ───────────────────────────── 시작 · 요청 ─────────────────────────────

        public async YarnTask StartDialogue(string nodeName)
        {
            if (program == null)
            {
                Debug.LogError("Can't start dialogue: no program has been set.", this);
                return;
            }

            dialogueCancellationSource?.Dispose();

            dialogueCompletionSource = new YarnTaskCompletionSource();
            dialogueCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            Dialogue.SetProgram(program);
            Dialogue.SetNode(nodeName);

            onDialogueStart?.Invoke();

            var tasks = new List<YarnTask>();

            foreach (DialoguePresenterBase view in dialoguePresenters)
            {
                if (view != null)
                    tasks.Add(view.OnDialogueStartedAsync());
            }

            await YarnTask.WhenAll(tasks);

            Dialogue.Continue();
        }

        public void RequestNextLine()
        {
            if (currentLineCancellationSource == null)
                return;

            currentLineCancellationSource.Cancel();
        }

        public void RequestHurryUpLine()
        {
            if (currentLineCancellationSource == null || currentLineHurryUpSource == null)
                return;

            currentLineHurryUpSource.Cancel();
        }
    }
}
