using Infrastructure.Orleans;

namespace Interfaces;

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
    
    public static ValueTask<T> GetClusterState<T>(this IGrainFactory grains)
    {
        var grain = grains.GetClusterStateGrain<T>();
        return grain.Get();
    }
    
    private static IClusterStateStorage<T> GetClusterStateGrain<T>(this IGrainFactory grains)
    {
        var type = typeof(T);
        var grainId = type.FullName!;
        return grains.GetGrain<IClusterStateStorage<T>>(grainId);
    }
}