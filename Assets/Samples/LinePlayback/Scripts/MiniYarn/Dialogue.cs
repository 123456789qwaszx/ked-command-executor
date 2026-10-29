using System;
using UnityEngine;

namespace MiniYarn
{
    // Yarn.Dialogue(가상 머신) 흉내. 노드의 내용을 하나씩 핸들러에 건넨다.
    //
    // 실제 VM과 같은 약속을 지킨다:
    //   - 내용을 건넨 뒤에는 멈춘다. 다음 내용은 Continue()가 불려야 간다.
    //   - 핸들러 안에서 동기로 끝난 내용은 SignalContentComplete() / Continue()로 알리고, VM은 같은 호출 안에서 이어 간다.
    //     그래서 시크로 지나가는 줄 · 즉시 끝나는 명령은 한 프레임 안에 줄줄이 지나간다.
    //   - Stop()은 대화 완료 핸들러를 부른다.
    public sealed class Dialogue
    {
        private enum ExecutionState { Stopped, Ready, Running, DeliveringContent, WaitingForContinue }

        public Action<Line> LineHandler;
        public Action<Command> CommandHandler;
        public Action<string> NodeStartHandler;
        public Action<string> NodeCompleteHandler;
        public Action DialogueCompleteHandler;

        private YarnProgram _program;
        private YarnProgram.Node _node;
        private int _next;
        private ExecutionState _state = ExecutionState.Stopped;

        public bool IsActive => _state != ExecutionState.Stopped;

        public void SetProgram(YarnProgram program) => _program = program;

        public void SetNode(string nodeName)
        {
            if (_program == null || !_program.TryGetNode(nodeName, out _node))
                throw new InvalidOperationException($"[MiniYarn] No node named '{nodeName}'.");

            _next = 0;
            _state = ExecutionState.Ready;
        }

        public void Continue()
        {
            switch (_state)
            {
                case ExecutionState.Stopped:
                    return;

                // 핸들러 안에서 동기로 끝났다 — 바깥 루프가 이어 간다.
                case ExecutionState.DeliveringContent:
                    _state = ExecutionState.Running;
                    return;

                // 이미 루프 안이다.
                case ExecutionState.Running:
                    return;

                case ExecutionState.Ready:
                    _state = ExecutionState.Running;
                    NodeStartHandler?.Invoke(_node.Name);
                    break;

                case ExecutionState.WaitingForContinue:
                    _state = ExecutionState.Running;
                    break;
            }

            while (_state == ExecutionState.Running)
            {
                if (_next >= _node.Content.Count)
                {
                    CompleteNode();
                    return;
                }

                object content = _node.Content[_next++];
                _state = ExecutionState.DeliveringContent;

                try
                {
                    if (content is Line line)
                        LineHandler?.Invoke(line);
                    else if (content is Command command)
                        CommandHandler?.Invoke(command);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                if (_state == ExecutionState.DeliveringContent)
                    _state = ExecutionState.WaitingForContinue;
            }
        }

        public void SignalContentComplete()
        {
            if (_state == ExecutionState.DeliveringContent)
                _state = ExecutionState.Running;
        }

        public void Stop()
        {
            if (_state == ExecutionState.Stopped)
                return;

            _state = ExecutionState.Stopped;
            DialogueCompleteHandler?.Invoke();
        }

        private void CompleteNode()
        {
            string name = _node.Name;
            _state = ExecutionState.Stopped;

            NodeCompleteHandler?.Invoke(name);
            DialogueCompleteHandler?.Invoke();
        }
    }
}
