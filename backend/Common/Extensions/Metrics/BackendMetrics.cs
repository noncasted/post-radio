using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Common.Extensions;

public struct MetricWatch : IDisposable
{
    private readonly Histogram<double> _histogram;
    private readonly long _start;

    private MetricWatch(Histogram<double> histogram)
    {
        _histogram = histogram;
        _start = Stopwatch.GetTimestamp();
    }

    public static MetricWatch Start(Histogram<double> histogram) => new(histogram);

    public void Dispose()
    {
        _histogram.Record(Stopwatch.GetElapsedTime(_start).TotalMilliseconds);
    }
}

public static class BackendMetrics
{
    private static readonly Meter Meter = new("Backend");

    // --- Transactions ---
    public static readonly Counter<long> TransactionTotal = Meter.CreateCounter<long>("backend.transactions.total",
        description: "Total transactions processed");

    public static readonly Counter<long> TransactionSuccess = Meter.CreateCounter<long>("backend.transactions.success",
        description: "Successful transactions");

    public static readonly Counter<long> TransactionFailure = Meter.CreateCounter<long>("backend.transactions.failure",
        description: "Failed transactions");

    public static readonly Counter<long> TransactionRollback =
        Meter.CreateCounter<long>("backend.transactions.rollback", description: "Transaction rollbacks");

    public static readonly Counter<long> TransactionRollbackFailure = Meter.CreateCounter<long>(
        "backend.transactions.rollback_failure", description: "Failed transaction rollbacks");

    public static readonly Histogram<double> TransactionDuration =
        Meter.CreateHistogram<double>("backend.transactions.duration", "ms", "Transaction duration");

    public static readonly Histogram<int> TransactionParticipantCount = Meter.CreateHistogram<int>(
        "backend.transactions.participant_count", description: "Participants per transaction");

    // --- State ---
    public static readonly Counter<long> StateReadTotal = Meter.CreateCounter<long>("backend.state.read.total",
        description: "State read operations");

    public static readonly Counter<long> StateWriteTotal = Meter.CreateCounter<long>("backend.state.write.total",
        description: "State write operations");

    public static readonly Counter<long> StateDeleteTotal = Meter.CreateCounter<long>("backend.state.delete.total",
        description: "State delete operations");

    public static readonly Histogram<double> StateReadDuration =
        Meter.CreateHistogram<double>("backend.state.read.duration", "ms", "State read duration");

    public static readonly Histogram<double> StateWriteDuration =
        Meter.CreateHistogram<double>("backend.state.write.duration", "ms", "State write duration");

    public static readonly Histogram<double> StateDeleteDuration =
        Meter.CreateHistogram<double>("backend.state.delete.duration", "ms", "State delete duration");

    public static readonly Histogram<int> StateWriteBatchSize =
        Meter.CreateHistogram<int>("backend.state.write.batch_size", description: "Records per write batch");

    // --- Side Effects ---
    public static readonly Counter<long> SideEffectProcessed =
        Meter.CreateCounter<long>("backend.side_effects.processed", description: "Side effects processed");

    public static readonly Counter<long> SideEffectRetry = Meter.CreateCounter<long>("backend.side_effects.retry",
        description: "Side effects retried");

    public static readonly Counter<long> SideEffectFailed = Meter.CreateCounter<long>("backend.side_effects.failed",
        description: "Side effects failed");

    public static readonly Counter<long> SideEffectDeadLetter = Meter.CreateCounter<long>(
        "backend.side_effects.dead_letter",
        description: "Side effects moved to dead letter queue");

    public static readonly Histogram<double> SideEffectDuration =
        Meter.CreateHistogram<double>("backend.side_effects.duration", "ms", "Side effect execution duration");

    public static readonly UpDownCounter<int> SideEffectInProgress = Meter.CreateUpDownCounter<int>(
        "backend.side_effects.in_progress", description: "Side effects currently executing");

    public static readonly Histogram<int> SideEffectQueueDepth =
        Meter.CreateHistogram<int>("backend.side_effects.queue_depth", description: "Side effects found per scan");

    // --- Task Balancer ---
    public static readonly Counter<long> TaskExecuted = Meter.CreateCounter<long>("backend.tasks.executed",
        description: "Tasks executed");

    public static readonly Counter<long> TaskSuccess = Meter.CreateCounter<long>("backend.tasks.success",
        description: "Tasks succeeded");

    public static readonly Counter<long> TaskFailure = Meter.CreateCounter<long>("backend.tasks.failure",
        description: "Tasks failed");

    public static readonly Histogram<double> TaskDuration = Meter.CreateHistogram<double>("backend.tasks.duration",
        "ms", "Task execution duration");

    public static readonly Histogram<int> TaskQueueDepth = Meter.CreateHistogram<int>("backend.tasks.queue_depth",
        description: "Tasks in scheduler queue");

    // --- Messaging: Durable Queue ---
    public static readonly Counter<long> DurableQueuePushed = Meter.CreateCounter<long>("backend.durable_queue.pushed",
        description: "Messages pushed to durable queue");

    public static readonly Histogram<int> DurableQueueObserverCount =
        Meter.CreateHistogram<int>("backend.durable_queue.observer_count", description: "Observers at push time");

    public static readonly Counter<long> DurableQueueDeliveryFailure =
        Meter.CreateCounter<long>("backend.durable_queue.delivery_failure", description: "Failed deliveries");

    public static readonly Counter<long> DurableQueueNoSubscribers = Meter.CreateCounter<long>(
        "backend.durable_queue.no_subscribers", description: "Push with no active subscribers");

    // --- Messaging: Runtime Channel ---
    public static readonly Counter<long> ChannelPublished = Meter.CreateCounter<long>("backend.channel.published",
        description: "Messages published to channel");

    public static readonly Histogram<int> ChannelObserverCount =
        Meter.CreateHistogram<int>("backend.channel.observer_count", description: "Observers at publish time");

    public static readonly Counter<long> ChannelDeliveryFailure =
        Meter.CreateCounter<long>("backend.channel.delivery_failure", description: "Failed deliveries");

    public static readonly Counter<long> ChannelDeliveryTimeout =
        Meter.CreateCounter<long>("backend.channel.delivery_timeout", description: "Observer delivery timeouts");

    public static readonly Counter<long> ChannelCatchUpExecuted =
        Meter.CreateCounter<long>("backend.channel.catchup_executed", description: "Catch-up operations executed");

    public static readonly Histogram<int> ChannelCatchUpMessages = Meter.CreateHistogram<int>(
        "backend.channel.catchup_messages", description: "Messages delivered per catch-up");

    public static readonly Counter<long> ChannelGapDetected = Meter.CreateCounter<long>("backend.channel.gap_detected",
        description: "Gaps detected during catch-up");

    // --- Messaging: Runtime Pipe ---
    public static readonly Counter<long> PipeRequestSent = Meter.CreateCounter<long>("backend.pipe.sent",
        description: "Pipe requests sent");

    public static readonly Counter<long> PipeTimeout = Meter.CreateCounter<long>("backend.pipe.timeout",
        description: "Pipe request timeouts");

    public static readonly Counter<long> PipeRetry = Meter.CreateCounter<long>("backend.pipe.retry",
        description: "Pipe send retries");

    public static readonly Histogram<double> PipeDuration = Meter.CreateHistogram<double>("backend.pipe.duration", "ms",
        "Pipe request-response duration");
}