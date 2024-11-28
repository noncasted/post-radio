using Internal;
using UnityEngine;

namespace Global.Systems
{
    [DisallowMultipleComponent]
    public class Updater : MonoBehaviour, IUpdater
    {
        private readonly ModifiableList<UpdateTargetHandle<IPreFixedUpdatable>> _preFixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IFixedUpdatable>> _fixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPostFixedUpdatable>> _postFixed = new();
        private readonly ModifiableList<UpdateTargetHandle<IPreUpdatable>> _preUpdate = new();
        private readonly ModifiableList<UpdateTargetHandle<IUpdatable>> _update = new();
        private readonly ModifiableList<UpdateTargetHandle<IGizmosUpdatable>> _gizmos = new();

        private readonly ViewableProperty<float> _speed = new(1f);

        private float _setSpeed = 1f;

        public IViewableProperty<float> Speed => _speed;

        private void Update()
        {
            var delta = Time.unscaledDeltaTime * Speed.Value;

            foreach (var updatable in _preUpdate)
            {
                if (updatable.Lifetime.IsTerminated == false)
                    updatable.Target.OnPreUpdate(delta);
            }

            foreach (var updatable in _update)
            {
                if (updatable.Lifetime.IsTerminated == false)
                    updatable.Target.OnUpdate(delta);
            }

            foreach (var updatable in _gizmos)
            {
                if (updatable.Lifetime.IsTerminated == false)
                    updatable.Target.OnGizmosUpdate();
            }
        }

        private void FixedUpdate()
        {
            var delta = Time.fixedDeltaTime * Speed.Value;

            foreach (var updatable in _preFixed)
            {
                if (updatable.Lifetime.IsTerminated == false)
                    updatable.Target.OnPreFixedUpdate(delta);
            }

            foreach (var updatable in _fixed)
            {
                if (updatable.Lifetime.IsTerminated == false)
                    updatable.Target.OnFixedUpdate(delta);
            }

            foreach (var updatable in _postFixed)
            {
                if (updatable.Lifetime.IsTerminated == false)
                    updatable.Target.OnPostFixedUpdate(delta);
            }
        }

        public void Add(IReadOnlyLifetime l, IPreUpdatable u) => Add(l, u, _preUpdate);
        public void Add(IReadOnlyLifetime l, IUpdatable u) => Add(l, u, _update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => Add(l, u, _fixed);
        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable u) => Add(l, u, _postFixed);
        public void Add(IReadOnlyLifetime l, IGizmosUpdatable u) => Add(l, u, _gizmos);
        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => Add(l, u, _preFixed);

        public void SetSpeed(float speed)
        {
            if (speed < 0)
                return;

            _speed.Set(speed);
        }

        public void Pause()
        {
            _setSpeed = _speed.Value;

            SetSpeed(0f);
        }

        public void Continue()
        {
            SetSpeed(_setSpeed);
        }

        private static void Add<T>(IReadOnlyLifetime l, T updatable, ModifiableList<UpdateTargetHandle<T>> list)
        {
            var handle = new UpdateTargetHandle<T>(updatable, l);
            list.Add(handle);
            l.Listen(() => list.Remove(handle));
        }
    }

    public readonly struct UpdateTargetHandle<T>
    {
        public UpdateTargetHandle(T target, IReadOnlyLifetime lifetime)
        {
            Target = target;
            Lifetime = lifetime;
        }

        public T Target { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}