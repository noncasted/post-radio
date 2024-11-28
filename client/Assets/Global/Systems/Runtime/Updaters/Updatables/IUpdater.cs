using Internal;

namespace Global.Systems
{
    public interface IUpdater
    {
        IViewableProperty<float> Speed { get; }

        void SetSpeed(float speed);
        
        void Pause();
        void Continue();
        
        void Add(IReadOnlyLifetime l, IUpdatable u);
        void Add(IReadOnlyLifetime l, IPreUpdatable updatable);
        void Add(IReadOnlyLifetime l, IPreFixedUpdatable u);
        void Add(IReadOnlyLifetime l, IFixedUpdatable u);
        void Add(IReadOnlyLifetime l, IPostFixedUpdatable updatable);
        void Add(IReadOnlyLifetime l, IGizmosUpdatable updatable);
    }
}