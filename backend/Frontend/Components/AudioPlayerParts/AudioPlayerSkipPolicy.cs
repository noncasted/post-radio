namespace Frontend.Components;

internal static class AudioPlayerSkipPolicy
{
    public const string ProgressTimeoutReason = "progress-timeout";
    public const string MissingStreamReason = "missing-stream";
    public const string StreamUrlFailedReason = "stream-url-failed";

    public static bool IsUiSuppressed(string reason)
    {
        return reason switch
        {
            "playlist-changed" => true,
            "ended" => true,
            "skip-requested" => true,
            MissingStreamReason => true,
            _ => false
        };
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
            "no-audio-element" => ("Не найден audio-элемент", "error"),
            MissingStreamReason => ("Трек недоступен, пропускаем", "info"),
            StreamUrlFailedReason => ("Не удалось получить URL трека", "warning"),
            ProgressTimeoutReason => ("Watchdog: нет прогресса 30 сек", "warning"),
            "buffering-timeout" => ("Watchdog: буферизация > 90 сек", "warning"),
            "startup-timeout" => ("Watchdog: трек не стартовал за 20 сек", "warning"),
            _ => ($"Переход: {reason}", "info")
        };
    }
}
