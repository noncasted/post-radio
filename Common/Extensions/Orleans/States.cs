using Microsoft.Extensions.Hosting;
using Orleans.Transactions.Abstractions;

namespace Common;

public static class States
{
    public const string Song = "Song";
    public const string Playlist = "Playlist";

    public const string Config = "Config";
    
    public const string Messaging_Queue = "Messaging_Queue";
    public const string ClusterState = "ClusterState";

    public static readonly IReadOnlyList<string> StateTables =
    [
        Song,
        Playlist,
        Config, 
        Messaging_Queue,
        ClusterState
    ];

    public class SongAttribute() : PersistentStateAttribute(Song, Song);
    public class PlaylistAttribute() : PersistentStateAttribute(Playlist, Playlist);

    public class ConfigStorageAttribute() : PersistentStateAttribute(Config, Config);
    
    public class MessageQueueAttribute() : PersistentStateAttribute(Messaging_Queue, Messaging_Queue);
    public class ClusterStateAttribute() : PersistentStateAttribute(ClusterState, ClusterState);
}

public static class StateAttributesExtensions
{
    public static IHostApplicationBuilder AddStateAttributes(this IHostApplicationBuilder builder)
    {
        AddPersistentAttribute<States.SongAttribute>();
        AddPersistentAttribute<States.PlaylistAttribute>();
        AddPersistentAttribute<States.MessageQueueAttribute>();
        AddPersistentAttribute<States.ConfigStorageAttribute>();
        AddPersistentAttribute<States.ClusterStateAttribute>();

        return builder;

        void AddTransactionalAttribute<TAttribute>()
            where TAttribute : TransactionalStateAttribute, new()
        {
            builder.Services.Add<IAttributeToFactoryMapper<TAttribute>,
                GenericTransactionalStateAttributeMapper<TAttribute>>();
        }

        void AddPersistentAttribute<TAttribute>()
            where TAttribute : PersistentStateAttribute
        {
            builder.Services.Add<IAttributeToFactoryMapper<TAttribute>,
                GenericPersistentStateAttributeMapper<TAttribute>>();
        }
    }
}