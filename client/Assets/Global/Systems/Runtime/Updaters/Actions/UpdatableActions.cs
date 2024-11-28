using System;
using Internal;

namespace Global.Systems
{
    public class FixedUpdatableActions : UpdatableActionBase, IFixedUpdatable
    {
        public FixedUpdatableActions(
            IReadOnlyLifetime lifetime,
            IUpdater updater,
            Action<float> callback,
            Func<bool> predicate) : base(lifetime, updater, callback, predicate)
        {
        }

        protected override void ListenUpdate(IReadOnlyLifetime lifetime, IUpdater updater)
        {
            updater.Add(lifetime, this);
        }

        public void OnFixedUpdate(float delta)
        {
            PassDelta(delta);
        }
    }

    public class UpdatableAction : UpdatableActionBase, IUpdatable
    {
        public UpdatableAction(
            IReadOnlyLifetime lifetime,
            IUpdater updater,
            Action<float> callback,
            Func<bool> predicate) : base(lifetime, updater, callback, predicate)
        {
        }

        protected override void ListenUpdate(IReadOnlyLifetime lifetime, IUpdater updater)
        {
            updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            PassDelta(delta);
        }
    }
}