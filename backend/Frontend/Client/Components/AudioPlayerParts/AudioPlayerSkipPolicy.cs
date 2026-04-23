namespace Frontend.Client.Components;

internal static class AudioPlayerSkipPolicy
{
    public static bool IsUiSuppressed(string reason, DateTime componentStartUtc)
    {
        if (reason == "playlist-changed" || reason == "ended" || reason == "skip-requested")
            return true;

        if (reason == "empty-url" && AudioPlayerTiming.IsStartupWindow(componentStartUtc))
            return true;

        return false;
    }

    public static (string Title, string Severity) DescribeReason(string reason)
    {
        if (reason.StartsWith("media-error", StringComparison.Ordinal))
            return ("Ошибка декодирования аудио", "error");

        if (reason.StartsWith("play-failed", StringComparison.Ordinal))
            return ("Браузер не смог запустить воспроизведение", "error");

        return reason switch
        {
            "ended" => ("Трек доигран", "info"),
            "skip-requested" => ("Пропущено пользователем", "info"),
            "playlist-changed" => ("Смена плейлиста", "info"),
            "empty-url" => ("Нет URL для трека", "warning"),
            AudioPlayerTiming.ProgressTimeoutReason => ("Watchdog: нет прогресса 30 сек", "warning"),
            "buffering-timeout" => ("Watchdog: буферизация > 90 сек", "warning"),
            "startup-timeout" => ("Watchdog: трек не стартовал за 20 сек", "warning"),
            "loop-exception" => ("Исключение в плеере", "error"),
            _ => ($"Переход: {reason}", "info")
        };
    }
}