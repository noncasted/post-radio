using Infrastructure.Orleans;

namespace Infrastructure.StorableActions;

public interface IClusterStateStorage<T> : IGrainWithStringKey
{
    Task Set(T value);
    ValueTask<T> Get();
}

public static class ClusterStateStorageExtensions
{
    public static Task SetClusterState<T>(this IOrleans orleans, T value)
    {
        return orleans.Grains.SetClusterState(value);
    }

    public static Task SetClusterState<T>(this IGrainFactory grains, T value)
    {
        var grain = grains.GetClusterStateGrain<T>();
        return grain.Set(value);
    }

    public static ValueTask<T> GetClusterState<T>(this IOrleans orleans)
    {
        return orleans.Grains.GetClusterState<T>();
    }

    extension(IGrainFactory grains)
    {
        public ValueTask<T> GetClusterState<T>()
        {
            var grain = grains.GetClusterStateGrain<T>();
            return grain.Get();
        }

        private IClusterStateStorage<T> GetClusterStateGrain<T>()
        {
            var type = typeof(T);
            var grainId = type.FullName!;
            return grains.GetGrain<IClusterStateStorage<T>>(grainId);
        }
    }
}