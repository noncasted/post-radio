using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public sealed class FileLoggerProvider : ILoggerProvider, IDisposable
{
    public FileLoggerProvider(string serviceName)
    {
        _serviceName = serviceName;

        var dir = TelemetryPaths.GetTelemetryDir("logs");

        if (dir != null)
        {
            var filePath = Path.Combine(dir, $"{serviceName}.log");
            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(stream) { AutoFlush = true };
        }
    }

    private readonly string _serviceName;
    private readonly StreamWriter? _writer;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    public void Dispose()
    {
        _writer?.Flush();
        _writer?.Dispose();
    }

    internal void WriteEntry(string categoryName, LogLevel logLevel, string message, Exception? exception)
    {
        if (_writer == null)
            return;

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        var level = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };

        var line = $"[{timestamp}] [{level}] [{categoryName}] {message}";

        try
        {
            lock (_writer)
            {
                _writer.WriteLine(line);

                if (exception != null)
                    _writer.WriteLine(exception.ToString());
            }
        }
        catch
        {
            // Logging must not crash the app
        }
    }
}

internal sealed class FileLogger : ILogger
{
    public FileLogger(string categoryName, FileLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    private readonly string _categoryName;
    private readonly FileLoggerProvider _provider;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        _provider.WriteEntry(_categoryName, logLevel, message, exception);
    }
}