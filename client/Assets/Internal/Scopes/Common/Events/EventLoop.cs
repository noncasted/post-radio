using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer.Internal;

namespace Internal
{
    public class EventLoop : IEventLoop
    {
        public EventLoop(
            ContainerLocal<IReadOnlyList<IScopeBaseSetup>> baseSetup,
            ContainerLocal<IReadOnlyList<IScopeBaseSetupAsync>> baseSetupAsync,
            ContainerLocal<IReadOnlyList<IScopeSetup>> setup,
            ContainerLocal<IReadOnlyList<IScopeSetupAsync>> setupAsync,
            ContainerLocal<IReadOnlyList<IScopeSetupCompletion>> setupCompletion,
            ContainerLocal<IReadOnlyList<IScopeSetupCompletionAsync>> setupCompletionAsync,
            ContainerLocal<IReadOnlyList<IScopeLoaded>> loaded,
            ContainerLocal<IReadOnlyList<IScopeLoadedAsync>> loadedAsync,
            ContainerLocal<IReadOnlyList<IScopeDispose>> dispose,
            ContainerLocal<IReadOnlyList<IScopeDisposeAsync>> disposeAsync
        )
        {
            _baseSetup = baseSetup.Value;
            _baseSetupAsync = baseSetupAsync.Value;
            _setup = setup.Value;
            _setupAsync = setupAsync.Value;
            _setupCompletion = setupCompletion.Value;
            _setupCompletionAsync = setupCompletionAsync.Value;
            _loaded = loaded.Value;
            _loadedAsync = loadedAsync.Value;
            _dispose = dispose.Value;
            _disposeAsync = disposeAsync.Value;
        }

        private readonly IReadOnlyList<IScopeBaseSetup> _baseSetup;
        private readonly IReadOnlyList<IScopeBaseSetupAsync> _baseSetupAsync;
        private readonly IReadOnlyList<IScopeSetup> _setup;
        private readonly IReadOnlyList<IScopeSetupAsync> _setupAsync;
        private readonly IReadOnlyList<IScopeSetupCompletion> _setupCompletion;
        private readonly IReadOnlyList<IScopeSetupCompletionAsync> _setupCompletionAsync;

        private readonly IReadOnlyList<IScopeLoaded> _loaded;
        private readonly IReadOnlyList<IScopeLoadedAsync> _loadedAsync;

        private readonly IReadOnlyList<IScopeDispose> _dispose;
        private readonly IReadOnlyList<IScopeDisposeAsync> _disposeAsync;

        public async UniTask RunConstruct(IReadOnlyLifetime lifetime)
        {
            Invoke(_baseSetup, l => { l.OnBaseSetup(lifetime); });
            await InvokeAsync(_baseSetupAsync, l => { return l.OnBaseSetupAsync(lifetime); });

            Invoke(_setup, l => { l.OnSetup(lifetime); });
            await InvokeAsync(_setupAsync, l => { return l.OnSetupAsync(lifetime); });

            Invoke(_setupCompletion, l => { l.OnSetupCompletion(lifetime); });
            await InvokeAsync(_setupCompletionAsync, l => { return l.OnSetupCompletionAsync(lifetime); });
        }

        public async UniTask RunLoaded(IReadOnlyLifetime lifetime)
        {
            Invoke(_loaded, l => { l.OnLoaded(lifetime); });
            await InvokeAsync(_loadedAsync, l => { return l.OnLoadedAsync(lifetime); });
        }

        public async UniTask RunDispose()
        {
            Invoke(_dispose, l => { l.OnDispose(); });
            await InvokeAsync(_disposeAsync, l => { return l.OnDisposeAsync(); });
        }

        private void Invoke<T>(IReadOnlyList<T> listeners, Action<T> invoker)
        {
            foreach (var listener in listeners)
                invoker.Invoke(listener);
        }

        private UniTask InvokeAsync<T>(IReadOnlyList<T> listeners, Func<T, UniTask> invoker)
        {
            var count = listeners.Count;
            var tasks = new UniTask[count];

            for (var i = 0; i < count; i++)
                tasks[i] = invoker.Invoke(listeners[i]);

            return UniTask.WhenAll(tasks);
        }
    }
}