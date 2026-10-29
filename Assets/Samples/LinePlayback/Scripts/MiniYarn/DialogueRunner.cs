using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace MiniYarn
{
    // Yarn DialogueRunner의 최소 흉내. 대본 파일 대신 코드에 적은 내용(DemoScript)을 차례로 건넨다.
    //
    //   명령   CommandHandler(YarnCommandBridge)에 넘기고 바로 다음으로 간다 — 브리지는 쌓기만 한다.
    //   대사   줄마다 취소 소스(다음 줄)와 거기 묶인 서두르기 소스를 만들고, 프레젠터의 RunLineAsync가 끝날 때까지 기다린다.
    //   정지   대화를 취소하고, 프레젠터의 OnDialogueCompleteAsync(대기 풀기 · 줄이 빠져나오길 기다림)를 거쳐 돌아온다.
    //
    // 시크로 지나가는 줄은 프레젠터가 동기로 끝내므로, 원본 Yarn처럼 한 호출 안에서 줄줄이 지나간다.
    public sealed class DialogueRunner : MonoBehaviour
    {
        // 명령 한 줄("place_left s1 bust 0fr") 또는 대사 한 줄.
        public readonly struct Content
        {
            public readonly string Command;
            public readonly LocalizedLine Line;

            private Content(string command, LocalizedLine line)
            {
                Command = command;
                Line = line;
            }

            public static Content Cmd(string command) => new(command, null);

            public static Content Say(string lineId, string speaker, string text, params string[] tags)
                => new(null, new LocalizedLine(lineId, speaker, text, tags));
        }

        public UnityEventString onNodeStart = new();

        private IReadOnlyList<Content> _contents = Array.Empty<Content>();
        private Action<string, string[]> _commandHandler;
        private DialoguePresenterBase _presenter;

        private CancellationTokenSource _dialogueCts;
        private CancellationTokenSource _lineCts;
        private CancellationTokenSource _hurryCts;
        private YarnTaskCompletionSource _completion;

        public bool IsDialogueRunning { get; private set; }

        public YarnTask DialogueTask => _completion != null ? _completion.Task : YarnTask.CompletedTask;

        public void SetContents(IReadOnlyList<Content> contents) => _contents = contents;
        public void SetCommandHandler(Action<string, string[]> handler) => _commandHandler = handler;
        public void SetPresenter(DialoguePresenterBase presenter) => _presenter = presenter;

        public async YarnTask StartDialogue(string nodeName)
        {
            if (IsDialogueRunning)
            {
                Debug.LogWarning($"[DialogueRunner] Already running - '{nodeName}' ignored.", this);
                return;
            }

            _dialogueCts = new CancellationTokenSource();
            _completion = new YarnTaskCompletionSource();
            IsDialogueRunning = true;

            await _presenter.OnDialogueStartedAsync();

            RunAsync(nodeName, _dialogueCts.Token, _completion).Forget();
        }

        public async YarnTask Stop()
        {
            if (!IsDialogueRunning)
                return;

            // 지금 줄의 다음 줄 · 서두르기 토큰도 함께 취소된다(묶여 있다).
            _dialogueCts.Cancel();

            await _presenter.OnDialogueCompleteAsync();
            await DialogueTask;
        }

        public void RequestNextLine() => _lineCts?.Cancel();

        public void RequestHurryUpLine() => _hurryCts?.Cancel();

        private async YarnTask RunAsync(string nodeName, CancellationToken token, YarnTaskCompletionSource completion)
        {
            onNodeStart?.Invoke(nodeName);

            for (int i = 0; i < _contents.Count && !token.IsCancellationRequested; i++)
            {
                Content content = _contents[i];

                if (content.Command != null)
                    DispatchCommand(content.Command);
                else
                    await RunLineAsync(content.Line, token);
            }

            // 끝까지 갔다. 정지였다면 Stop이 이미 프레젠터를 닫았다.
            if (!token.IsCancellationRequested)
                await _presenter.OnDialogueCompleteAsync();

            IsDialogueRunning = false;
            completion.TrySetResult();
        }

        private void DispatchCommand(string commandText)
        {
            string[] parts = commandText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return;

            var args = new string[parts.Length - 1];
            Array.Copy(parts, 1, args, 0, args.Length);

            try
            {
                _commandHandler?.Invoke(parts[0], args);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private async YarnTask RunLineAsync(LocalizedLine line, CancellationToken dialogueToken)
        {
            _lineCts = CancellationTokenSource.CreateLinkedTokenSource(dialogueToken);
            _hurryCts = CancellationTokenSource.CreateLinkedTokenSource(_lineCts.Token);

            var token = new LineCancellationToken
            {
                NextContentToken = _lineCts.Token,
                HurryUpToken = _hurryCts.Token,
            };

            try
            {
                await _presenter.RunLineAsync(line, token);
            }
            catch (Exception e)
            {
                Debug.LogException(e, _presenter);
            }
            finally
            {
                _hurryCts.Dispose();
                _lineCts.Dispose();
                _hurryCts = null;
                _lineCts = null;
            }
        }

        private void OnDestroy()
        {
            _dialogueCts?.Cancel();
        }
    }
}
