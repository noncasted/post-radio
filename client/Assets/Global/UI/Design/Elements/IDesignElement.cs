using Internal;

namespace Global.UI
{
    public interface IDesignElement
    {
        IViewableProperty<DesignElementState> State { get; }
        IReadOnlyLifetime Lifetime { get; }

        void SetState(DesignElementState state);
    }
}