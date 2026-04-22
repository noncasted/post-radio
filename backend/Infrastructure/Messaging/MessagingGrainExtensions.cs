namespace Infrastructure;

public static class MessagingGrainExtensions
{
    public static TimeSpan? GetKeepAliveDelay<T>(
        IEnumerable<T> observers,
        Func<T, DateTime> getUpdateDate,
        int keepAliveMinutes)
    {
        if (!observers.Any())
            return null;

        var latestUpdate = observers.Max(getUpdateDate);
        var timeSinceLastUpdate = DateTime.UtcNow - latestUpdate;
        var keepAlive = TimeSpan.FromMinutes(keepAliveMinutes);

        if (timeSinceLastUpdate < keepAlive)
            return keepAlive - timeSinceLastUpdate;

        return null;
    }
}