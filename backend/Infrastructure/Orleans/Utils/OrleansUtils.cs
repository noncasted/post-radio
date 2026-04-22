using Common.Extensions;
using Infrastructure.State;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public interface IOrleans
{
    IClusterClient Client { get; }
    ITransactions Transactions { get; }
    IStateStorage StateStorage { get; }
    IStateSerializer Serializer { get; }
    ILogger Logger { get; }
}

public class OrleansUtils : IOrleans
{
    public OrleansUtils(
        IClusterClient client,
        IGrainFactory grains,
        ITransactions transactions,
        IStateSerializer serializer,
        ILogger<OrleansUtils> logger,
        IStateStorage stateStorage)
    {
        Grains = grains;
        Transactions = transactions;
        Logger = logger;
        StateStorage = stateStorage;
        Serializer = serializer;
        Client = client;
    }

    public IGrainFactory Grains { get; }
    public ITransactions Transactions { get; }

    public IClusterClient Client { get; }
    public IStateStorage StateStorage { get; }
    public IStateSerializer Serializer { get; }
    public ILogger Logger { get; }
}

public static class OrleansUtilsExtensions
{
    public static IHostApplicationBuilder AddOrleansUtils(this IHostApplicationBuilder builder)
    {
        builder.Add<StateFactory>()
               .As<IStateFactory>();

        builder.Add<StateAttributeMapper>()
               .As<IAttributeToFactoryMapper<StateAttribute>>();

        builder.Add<StateMigrations>()
               .As<IStateMigrations>();

        builder.Add<StateStorage>()
               .As<IStateStorage>();

        builder.Add<StateSerializer>()
               .As<IStateSerializer>();

        builder.Add<Transactions>()
               .As<ITransactions>();

        builder.Add<OrleansUtils>()
               .As<IOrleans>();

        return builder;
    }

    public static Task Iterate<T>(this IReadOnlyList<T> grains, Func<T, Task> action)
        where T : IGrainWithGuidKey
    {
        var tasks = grains.Select(action);
        return Task.WhenAll(tasks);
    }

    extension(IOrleans orleans)
    {
        public IGrainFactory Grains => orleans.Client;

        public T GetGrain<T>(string key) where T : IGrainWithStringKey
        {
            return orleans.Grains.GetGrain<T>(key);
        }

        public T GetGrain<T>(long key) where T : IGrainWithIntegerKey
        {
            return orleans.Grains.GetGrain<T>(key);
        }

        public T GetGrain<T>(Guid key) where T : IGrainWithGuidKey
        {
            return orleans.Grains.GetGrain<T>(key);
        }

        public T GetGrain<T>() where T : IGrainWithGuidKey
        {
            return orleans.Grains.GetGrain<T>(Guid.Empty);
        }

        public IReadOnlyList<T> GetGrains<T>(IReadOnlyList<Guid> ids)
            where T : IGrainWithGuidKey
        {
            var grains = new T[ids.Count];

            for (var i = 0; i < ids.Count; i++)
                grains[i] = orleans.Grains.GetGrain<T>(ids[i]);

            return grains;
        }

        public Task InTransaction(Func<Task> action)
        {
            return orleans.Transactions.Run(action);
        }

        public Task<T> InTransaction<T>(Func<Task<T>> action)
        {
            return orleans.Transactions.Run(action);
        }
    }
}