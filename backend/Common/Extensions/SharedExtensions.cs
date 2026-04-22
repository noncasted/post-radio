namespace Common.Extensions;

public static class SharedExtensions
{
    public static T ThrowIfNull<T>(this T? obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        return obj;
    }

    public static void AddSharedContexts()
    {
        /*
        var builder = new UnionBuilder<INetworkContext>();

        builder
            .Add<EmptyResponse>()
            .AddSharedBackend()
            .AddSharedGame()
            .AddSharedSession();

        var entityPayloads = new UnionBuilder<IEntityPayload>();

        entityPayloads
            .Add<MenuPlayerPayload>()
            .Add<CardCreatePayload>()
            .Add<PlayerCreatePayload>();

        builder.Build();
        entityPayloads.Build();
    */
    }
}