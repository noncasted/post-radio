using Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Serialization;
using Orleans.Transactions;

namespace Infrastructure.Orleans;

public interface IOrleans
{
    IClusterClient Client { get; }
    IGrainFactory Grains { get; }
    ITransactions Transactions { get; }
    IDbSource DbSource { get; }
    OrleansJsonSerializer Serializer { get; }
    ILogger Logger { get; }
}

public class OrleansUtils : IOrleans
{
    public OrleansUtils(
        IClusterClient client,
        IGrainFactory grains,
        ITransactions transactions,
        IDbSource dbSource,
        OrleansJsonSerializer serializer,
        ILogger<OrleansUtils> logger)
    {
        Grains = grains;
        Transactions = transactions;
        Logger = logger;
        Serializer = serializer;
        DbSource = dbSource;
        Client = client;
    }

    public IClusterClient Client { get; }
    public IGrainFactory Grains { get; }
    public ITransactions Transactions { get; }
    public IDbSource DbSource { get; }
    public OrleansJsonSerializer Serializer { get; }
    public ILogger Logger { get; }
}

public static class OrleansUtilsExtensions
{
    public static IHostApplicationBuilder AddOrleansUtils(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.Add<ITransactionAgent, TransactionResolver>()
            .As<ITransactionResolver>();

        services.Add<ITransactionRunner, TransactionRunner>();
        services.Add<ITransactions, Transactions>();
        services.Add<IOrleans, OrleansUtils>();

        return builder;
    }

    public static T GetGrain<T>(this IOrleans orleans, string key) where T : IGrainWithStringKey
    {
        return orleans.Grains.GetGrain<T>(key);
    }

    public static T GetGrain<T>(this IOrleans orleans, long key) where T : IGrainWithIntegerKey
    {
        return orleans.Grains.GetGrain<T>(key);
    }
    
    public static T GetGrain<T>(this IOrleans orleans, Guid key) where T : IGrainWithGuidKey
    {
        return orleans.Grains.GetGrain<T>(key);
    }

    public static T GetGrain<T>(this IOrleans orleans) where T : IGrainWithGuidKey
    {
        return orleans.Grains.GetGrain<T>(Guid.Empty);
    }

    public static IReadOnlyList<T> GetGrains<T>(this IOrleans orleans, IReadOnlyList<Guid> ids)
        where T : IGrainWithGuidKey
    {
        var grains = new T[ids.Count];

        for (var i = 0; i < ids.Count; i++)
            grains[i] = orleans.Grains.GetGrain<T>(ids[i]);

        return grains;
    }

    public static Task Iterate<T>(this IReadOnlyList<T> grains, Func<T, Task> action)
        where T : IGrainWithGuidKey
    {
        var tasks = grains.Select(action);
        return Task.WhenAll(tasks);
    }

    public static Task InTransaction(this IOrleans orleans, Func<Task> action)
    {
        return orleans.Transactions.Client.RunTransaction(TransactionOption.CreateOrJoin, action);
    }

    public static TransactionRunBuilder Transaction(this IOrleans orleans, Func<Task> action)
    {
        return orleans.Transactions.Runner.Create(action);
    }
}