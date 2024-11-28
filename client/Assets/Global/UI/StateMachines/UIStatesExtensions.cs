using System;
using Cysharp.Threading.Tasks;

namespace Global.UI
{
    public static class UIStatesExtensions
    {
        public static IUIStateHandle EnterChild(this IUIStateMachine stateMachine, IUIState parent, IUIState state)
        {
            return stateMachine.CreateChild(parent, state).TryEnter();
        }

        public static UniTask ProcessChild(
            this IUIStateMachine stateMachine,
            IUIState parent,
            IUIState state)
        {
            return stateMachine.CreateChild(parent, state).TryProcess();
        }

        public static async UniTask ProcessChild(
            this IUIStateMachine stateMachine,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask> action)
        {
            var handle = stateMachine.CreateChild(parent, state);
            await action.Invoke(handle);
            handle.Exit();
        }

        public static UniTask ProcessStack(
            this IUIStateMachine stateMachine,
            IUIState parent,
            IUIState state)
        {
            return stateMachine.CreateStackChild(parent, state).TryProcess();
        }

        public static async UniTask ProcessStack(
            this IUIStateMachine stateMachine,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask> action)
        {
            var handle = stateMachine.CreateStackChild(parent, state);
            await action.Invoke(handle);
            handle.Exit();
        }

        public static UniTask Process(
            this IUIStateMachine stateMachine,
            IUIState parent,
            IUIState state)
        {
            var handle = stateMachine.CreateChild(parent, state);
            return handle.TryProcess();
        }

        public static async UniTask TryProcess(this IUIStateHandle handle)
        {
            switch (handle.State)
            {
                case IUIStateEnterHandler handler:
                    handler.OnEntered(handle);
                    await handle.Completion.Task;
                    handle.Exit();
                    break;
                case IUIStateAsyncEnterHandler handler:
                    await handler.OnEntered(handle);
                    handle.Exit();
                    break;
            }
        }

        public static IUIStateHandle TryEnter(this IUIStateHandle handle)
        {
            switch (handle.State)
            {
                case IUIStateEnterHandler handler:
                    handler.OnEntered(handle);
                    break;
                case IUIStateAsyncEnterHandler handler:
                    handler.OnEntered(handle);
                    break;
            }

            return handle;
        }
    }
}