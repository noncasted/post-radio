namespace Internal
{
    public interface IGameObjectLifetime
    {
        IReadOnlyLifetime GetValidLifetime();
    }
}