using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class MetricsSnapshotService : IHostedService, IDisposable
{
    public MetricsSnapshotService(ILogger<MetricsSnapshotService> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<MetricsSnapshotService> _logger;
    private readonly ConcurrentDictionary<string, MetricSnapshot> _snapshots = new();

    private MeterListener? _listener;
    private Timer? _timer;
    private string _serviceName = "unknown";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ??
                       Assembly.GetEntryAssembly()?.GetName().Name?.ToLowerInvariant() ?? "unknown";

        _listener = new MeterListener();
        _listener.InstrumentPublished = OnInstrumentPublished;
        _listener.SetMeasurementEventCallback<long>(OnMeasurement);
        _listener.SetMeasurementEventCallback<int>(OnMeasurement);
        _listener.SetMeasurementEventCallback<double>(OnMeasurement);
        _listener.Start();

        _timer = new Timer(WriteSnapshot, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _listener?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _listener?.Dispose();
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (instrument.Meter.Name == "Backend")
            listener.EnableMeasurementEvents(instrument);
    }

    private void OnMeasurement(
        Instrument instrument,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        Record(instrument, value);
    }

    private void OnMeasurement(
        Instrument instrument,
        int value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        Record(instrument, value);
    }

    private void OnMeasurement(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        Record(instrument, value);
    }

    private void Record(Instrument instrument, double value)
    {
        var snapshot = _snapshots.GetOrAdd(instrument.Name, _ => new MetricSnapshot(instrument));
        snapshot.Record(value);
    }

    private void WriteSnapshot(object? state)
    {
        try
        {
            var dir = TelemetryPaths.GetTelemetryDir("metrics");

            if (dir == null)
                return;

            var metrics = new Dictionary<string, object>();

            foreach (var (name, snapshot) in _snapshots)
            {
                metrics[name] = snapshot.ToDict();
            }

            var data = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["service"] = _serviceName,
                ["metrics"] = metrics
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(dir, $"metrics_{_serviceName}.json");
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[MetricsSnapshot] Failed to write snapshot");
        }
    }

    private class MetricSnapshot
    {
        private readonly bool _isHistogram;
        private long _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;
        private readonly Lock _lock = new();

        public MetricSnapshot(Instrument instrument)
        {
            _isHistogram = instrument is Histogram<double> or Histogram<int> or Histogram<long>;
        }

        public void Record(double value)
        {
            lock (_lock)
            {
                _count++;
                _sum += value;

                if (value < _min)
                    _min = value;

                if (value > _max)
                    _max = value;
            }
        }

        public Dictionary<string, object> ToDict()
        {
            lock (_lock)
            {
                if (_isHistogram)
                {
                    return new Dictionary<string, object>
                    {
                        ["count"] = _count,
                        ["sum"] = Math.Round(_sum, 2),
                        ["min"] = _count > 0 ? Math.Round(_min, 2) : 0,
                        ["max"] = _count > 0 ? Math.Round(_max, 2) : 0,
                        ["avg"] = _count > 0 ? Math.Round(_sum / _count, 2) : 0
                    };
                }

                return new Dictionary<string, object>
                {
                    ["value"] = _count > 0 && _sum == _count ? (long)_sum : Math.Round(_sum, 2)
                };
            }
        }
    }
}