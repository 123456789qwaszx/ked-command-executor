using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MiniYarn
{
    // Yarn Spinner의 YarnTask 흉내 — System.Threading.Tasks.Task를 감싼 awaitable.
    //
    // 원본 코드(CustomLinePresenter · VNLinePresentationFlow …)를 고치지 않고 옮겨 오려면 `async YarnTask`가
    // 그대로 컴파일돼야 한다. 그래서 메서드 빌더를 붙였다 — 안쪽은 AsyncTaskMethodBuilder 그대로다.
    // await의 이어짐은 Unity 메인 스레드(UnitySynchronizationContext)로 돌아온다.
    [AsyncMethodBuilder(typeof(YarnTaskMethodBuilder))]
    public readonly struct YarnTask
    {
        private readonly Task _task;

        internal YarnTask(Task task)
        {
            _task = task;
        }

        internal Task AsTask() => _task ?? Task.CompletedTask;

        public TaskAwaiter GetAwaiter() => AsTask().GetAwaiter();

        public static YarnTask CompletedTask => default;

        // 한 프레임 양보한다.
        public static YarnTask Yield() => new(NextFrame());

        public static YarnTask WhenAll(IEnumerable<YarnTask> tasks)
        {
            var list = new List<Task>();

            foreach (YarnTask task in tasks)
                list.Add(task.AsTask());

            return new YarnTask(Task.WhenAll(list));
        }

        // 토큰이 취소되면 OperationCanceledException으로 끝난다(SuppressCancellationThrow와 짝을 이룬다).
        public static YarnTask WaitUntilCanceled(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new YarnTask(Task.FromCanceled(token));

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => tcs.TrySetCanceled(token));
            return new YarnTask(tcs.Task);
        }

        private static async Task NextFrame() => await Awaitable.NextFrameAsync();
    }

    public static class YarnTaskExtensions
    {
        public static async YarnTask SuppressCancellationThrow(this YarnTask task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public static bool IsCompletedSuccessfully(this YarnTask task)
            => task.AsTask().Status == TaskStatus.RanToCompletion;

        // 기다리지 않고 흘려보낸다. 예외는 삼키지 않고 로그로 남긴다.
        public static async void Forget(this YarnTask task)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    public sealed class YarnTaskCompletionSource
    {
        private readonly TaskCompletionSource<bool> _source =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public YarnTask Task => new(_source.Task);

        public bool TrySetResult() => _source.TrySetResult(true);
    }

    public struct YarnTaskMethodBuilder
    {
        private AsyncTaskMethodBuilder _builder;

        public static YarnTaskMethodBuilder Create() => new() { _builder = AsyncTaskMethodBuilder.Create() };

        public YarnTask Task => new(_builder.Task);

        public void Start<TStateMachine>(ref TStateMachine stateMachine)
            where TStateMachine : IAsyncStateMachine
            => _builder.Start(ref stateMachine);

        public void SetStateMachine(IAsyncStateMachine stateMachine) => _builder.SetStateMachine(stateMachine);

        public void SetResult() => _builder.SetResult();

        public void SetException(Exception exception) => _builder.SetException(exception);

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
            => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
    }
}
