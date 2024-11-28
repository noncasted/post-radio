using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.Systems
{
    public static class UpdatableActionExtensions
    {
        public static UniTask RunFixedAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Action<float> callback,
            Func<bool> predicate)
        {
            var action = new FixedUpdatableActions(lifetime, updater, callback, predicate);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Action<float> callback,
            Func<bool> predicate)
        {
            var action = new UpdatableAction(lifetime, updater, callback, predicate);
            return action.Process();
        }

        public static UniTask RunUpdateAction(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            Action<float> callback)
        {
            var action = new UpdatableAction(lifetime, updater, callback, () => true);
            return action.Process();
        }
    }
}