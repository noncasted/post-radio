using Internal;

namespace Global.Systems
{
    public class UpdaterProxy :
        IUpdater,
        IUpdatable,
        IPreUpdatable,
        IPreFixedUpdatable,
        IFixedUpdatable,
        IPostFixedUpdatable,
        IGizmosUpdatable,
        IScopeSetup
    {
        private readonly ModifiableList<IPreFixedUpdatable> _preFixed = new();
        private readonly ModifiableList<IFixedUpdatable> _fixed = new();
        private readonly ModifiableList<IPostFixedUpdatable> _postFixed = new();
        private readonly ModifiableList<IPreUpdatable> _preUpdate = new();
        private readonly ModifiableList<IUpdatable> _update = new();
        private readonly ModifiableList<IGizmosUpdatable> _gizmos = new();

        private readonly IUpdater _updater;
        private readonly ViewableProperty<float> _speed = new(1f);

        private float _savedSpeed = 1f;

        public IViewableProperty<float> Speed => _speed;

        public UpdaterProxy(IUpdater updater)
        {
            _updater = updater;
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, (IPreUpdatable)this);
            _updater.Add(lifetime, (IUpdatable)this);
            _updater.Add(lifetime, (IFixedUpdatable)this);
            _updater.Add(lifetime, (IPostFixedUpdatable)this);
            _updater.Add(lifetime, (IGizmosUpdatable)this);
            _updater.Add(lifetime, (IPreFixedUpdatable)this);
        }

        public void Add(IReadOnlyLifetime l, IPreUpdatable u) => Add(l, u, _preUpdate);
        public void Add(IReadOnlyLifetime l, IUpdatable u) => Add(l, u, _update);
        public void Add(IReadOnlyLifetime l, IFixedUpdatable u) => Add(l, u, _fixed);
        public void Add(IReadOnlyLifetime l, IPostFixedUpdatable u) => Add(l, u, _postFixed);
        public void Add(IReadOnlyLifetime l, IGizmosUpdatable u) => Add(l, u, _gizmos);
        public void Add(IReadOnlyLifetime l, IPreFixedUpdatable u) => Add(l, u, _preFixed);

        public void OnUpdate(float delta)
        {
            foreach (var updatable in _update)
                updatable.OnUpdate(delta * _updater.Speed.Value);
        }

        public void OnPreUpdate(float delta)
        {
            foreach (var updatable in _preUpdate)
                updatable.OnPreUpdate(delta * _updater.Speed.Value);
        }

        public void OnPreFixedUpdate(float delta)
        {
            foreach (var updatable in _preFixed)
                updatable.OnPreFixedUpdate(delta * _updater.Speed.Value);
        }

        public void OnFixedUpdate(float delta)
        {
            foreach (var updatable in _fixed)
                updatable.OnFixedUpdate(delta * _updater.Speed.Value);
        }

        public void OnPostFixedUpdate(float delta)
        {
            foreach (var updatable in _postFixed)
                updatable.OnPostFixedUpdate(delta * _updater.Speed.Value);
        }

        public void OnGizmosUpdate()
        {
            foreach (var updatable in _gizmos)
                updatable.OnGizmosUpdate();
        }

        private static void Add<T>(IReadOnlyLifetime l, T updatable, ModifiableList<T> list)
        {
            list.Add(updatable);
            l.Listen(() => list.Remove(updatable));
        }

        public void SetSpeed(float speed)
        {
            _speed.Set(speed);
        }

        public void Pause()
        {
            _savedSpeed = _speed.Value;
            SetSpeed(0f);
        }

        public void Continue()
        {
            SetSpeed(_savedSpeed);
        }
    }
}