using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;

namespace Infrastructure;

[GenerateSerializer]
public class TransactionHandlerResult
{
    [Id(0)]
    public required IReadOnlyList<IStateValue> States { get; init; }
}

public interface IGrainTransactionHandler : IGrainExtension
{
    [AlwaysInterleave]
    Task<Guid> Join(Guid transactionId);

    [AlwaysInterleave]
    Task<TransactionHandlerResult> CollectResult(Guid transactionId);

    [AlwaysInterleave]
    Task OnSuccess(Guid transactionId);

    [AlwaysInterleave]
    Task OnFailure(Guid transactionId);
}

// GrainTransactionHandler is attached to every grain as an IGrainExtension.
// It serializes access between concurrent transactions on the same grain.
//
// Invariant: _lock is held for the entire duration of an active transaction.
//   - Acquired in Join() when a new transaction registers this grain
//   - Released in OnSuccess() / OnFailure() when the transaction ends
//
// All methods are [AlwaysInterleave], meaning Orleans can call them concurrently
// even if the grain itself is busy — hence the semaphore is required.
//
// Transaction lifecycle on this grain:
//   Transactions.Process() calls grain methods
//     → TransactionAttribute intercepts each outgoing call
//       → Join() is called on the target grain's handler (registers participant)
//       → grain method executes, calls State.Write() → RecordStateChanged()
//   After all grain calls finish:
//     → CollectResult() — snapshots current in-memory state for DB write
//     → [DB write happens atomically in a single Postgres transaction]
//     → OnSuccess() — confirms commit, releases lock
//   On any exception:
//     → OnFailure() — rolls back in-memory state, releases lock
public class GrainTransactionHandler : IGrainTransactionHandler
{
    public GrainTransactionHandler(
        ITransactionConfig transactionConfig,
        ILogger<GrainTransactionHandler> logger)
    {
        _transactionConfig = transactionConfig;
        _logger = logger;
    }

    // Mutex held for the entire duration of an active transaction.
    // Initial count 1 = grain is free (unlocked).
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Stable identity returned to Transactions.Process() to track this grain as a participant.
    private readonly Guid _participantId = Guid.NewGuid();

    private readonly ITransactionConfig _transactionConfig;
    private readonly ILogger<GrainTransactionHandler> _logger;

    // Id of the transaction currently holding this grain.
    // Guid.Empty means the grain is free.
    private Guid _currentTransactionId;

    // Last activity time of the current transaction.
    // Refreshed on every Join() call (including idempotent re-joins).
    // Used to detect stuck transactions eligible for takeover.
    private DateTime _currentTransactionTime;

    private readonly HashSet<IGrainStateTransactionParticipant> _states = new();

    // Called by TransactionAttribute every time a [Transaction] method on this grain is invoked.
    // Returns _participantId so Transactions.Process() can track this grain.
    public async Task<Guid> Join(Guid transactionId)
    {
        // Same transaction calling again (e.g. multiple [Transaction] methods in one Process).
        // Refresh timestamp so the stuck-transaction check stays accurate.
        if (_currentTransactionId == transactionId)
        {
            _currentTransactionTime = DateTime.UtcNow;
            return _participantId;
        }

        // Grain is free — acquire the lock and register the new transaction.
        if (_currentTransactionId == Guid.Empty)
        {
            await _lock.WaitAsync();
            _currentTransactionId = transactionId;
            _currentTransactionTime = DateTime.UtcNow;
            return _participantId;
        }

        // Another transaction is active. Wait for it to finish.
        // The previous transaction releases the lock in OnSuccess/OnFailure.
        var options = _transactionConfig.Value ?? new TransactionOptions();
        var isAcquired = await _lock.WaitAsync(TimeSpan.FromSeconds(options.LockWaitSeconds));

        if (isAcquired == false)
        {
            // Timed out. If the stuck transaction is still within its grace period,
            // we cannot take over — it may still be running normally but slowly.
            if (_currentTransactionTime.AddSeconds(options.StuckGraceSeconds) >= DateTime.UtcNow)
            {
                _logger.LogWarning(
                    "[Transaction] [Join] Timeout waiting for lock. TransactionId={TransactionId} BlockedBy={BlockedBy}",
                    transactionId, _currentTransactionId);

                throw new Exception(
                    $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{_currentTransactionId}'.");
            }

            // The transaction has been inactive for >30s — treat it as stuck.
            // Force-release its semaphore and acquire for ourselves.
            // The stuck transaction's eventual OnSuccess/OnFailure will fail the ID check
            // and exit early without touching the semaphore or state.
            var stuckAge = (DateTime.UtcNow - _currentTransactionTime).TotalSeconds;

            _logger.LogWarning(
                "[Transaction] [Takeover] Forcing takeover of stuck transaction. StuckTransactionId={StuckId} AgeSeconds={AgeSeconds} IncomingTransactionId={TransactionId}",
                _currentTransactionId, stuckAge, transactionId);

            if (_lock.CurrentCount == 0)
                _lock.Release();

            await _lock.WaitAsync();

            // Roll back any in-memory state left by the stuck transaction.
            foreach (var state in _states)
                state.OnTransactionFailure();

            _states.Clear();
            _currentTransactionId = transactionId;
            _currentTransactionTime = DateTime.UtcNow;
            return _participantId;
        }

        // Acquired the lock normally — the previous transaction completed while we waited.
        // Sanity check: _currentTransactionId should be Guid.Empty at this point
        // (OnSuccess/OnFailure always clear it before releasing the lock).
        if (_currentTransactionId != Guid.Empty && _currentTransactionId != transactionId)
        {
            _lock.Release();

            throw new Exception(
                $"Handler failed to join transaction id '{transactionId}'. Current transaction in progress '{_currentTransactionId}'.");
        }

        _currentTransactionId = transactionId;
        _currentTransactionTime = DateTime.UtcNow;
        return _participantId;
    }

    public void RecordStateChanged(IGrainStateTransactionParticipant state)
    {
        _states.Add(state);
    }

    // Called by Transactions.Process() after all grain methods have executed.
    // Returns the current in-memory snapshots of all modified states.
    // These are then written atomically to Postgres in a single DB transaction.
    public Task<TransactionHandlerResult> CollectResult(Guid transactionId)
    {
        if (_currentTransactionId != transactionId)
        {
            _logger.LogError(
                "[Transaction] [CollectResult] Transaction ID mismatch. Expected={TransactionId} Current={CurrentId}",
                transactionId, _currentTransactionId);

            throw new Exception(
                $"Handler failed to complete transaction id '{transactionId}'. Current transaction in progress '{_currentTransactionId}'.");
        }

        var states = new List<IStateValue>();

        foreach (var state in _states)
            states.Add(state.GetState());

        return Task.FromResult(new TransactionHandlerResult
        {
            States = states,
        });
    }

    // Called by Transactions.Process() after the DB write succeeds.
    // Confirms the commit to each state object and releases the lock.
    public Task OnSuccess(Guid transactionId)
    {
        // ID mismatch means this grain was taken over by another transaction.
        // That transaction now owns the lock — do not release it here.
        if (_currentTransactionId != transactionId)
        {
            _logger.LogWarning(
                "[Transaction] [OnSuccess] Skipped — grain was taken over. ExpectedId={TransactionId} CurrentId={CurrentId}",
                transactionId, _currentTransactionId);

            return Task.CompletedTask;
        }

        foreach (var state in _states)
            state.OnTransactionSuccess();

        _states.Clear();
        _currentTransactionId = Guid.Empty;

        if (_lock.CurrentCount == 0)
            _lock.Release();

        return Task.CompletedTask;
    }

    // Called by Transactions.Process() on any exception (before or after DB write).
    // Rolls back in-memory state and releases the lock.
    public Task OnFailure(Guid transactionId)
    {
        // ID mismatch means this grain was taken over — nothing to roll back here.
        if (_currentTransactionId != transactionId)
            return Task.CompletedTask;

        foreach (var state in _states)
            state.OnTransactionFailure();

        _states.Clear();
        _currentTransactionId = Guid.Empty;

        if (_lock.CurrentCount == 0)
            _lock.Release();

        return Task.CompletedTask;
    }
}