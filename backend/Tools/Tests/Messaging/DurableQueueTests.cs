using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests DurableQueue: push via side effects, listen with observer, multi-consumer delivery.
/// DurableQueue uses side effects for delivery, so requires SideEffectTestFixture.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class DurableQueueTests(SideEffectTestFixture fixture) : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    [Fact]
    public async Task PushDirect_SingleMessage_DeliveredToListener()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(),
            queueId,
            msg => received.TrySetResult(msg));

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "hello", Sequence = 1 });
        await DrainSideEffectsAsync();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("hello");
        result.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task PushDirect_MultipleMessages_AllDelivered()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(),
            queueId,
            msg => {
                lock (received)
                {
                    received.Add(msg);

                    if (received.Count >= 5)
                        allReceived.TrySetResult();
                }
            });

        for (var i = 0; i < 5; i++)
            await messaging.PushDirectQueue(queueId, new TestMessage { Text = $"msg-{i}", Sequence = i });

        await DrainSideEffectsAsync();
        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        received.Should().HaveCount(5);
        received.Select(m => m.Sequence).Should().BeEquivalentTo([0, 1, 2, 3, 4]);
    }

    [Fact]
    public async Task PushDirect_MultipleListeners_AllReceive()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received1 = new TaskCompletionSource<TestMessage>();
        var received2 = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => received1.TrySetResult(msg));
        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => received2.TrySetResult(msg));

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "broadcast", Sequence = 42 });
        await DrainSideEffectsAsync();

        var r1 = await received1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var r2 = await received2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        r1.Text.Should().Be("broadcast");
        r2.Text.Should().Be("broadcast");
    }

    [Fact]
    public async Task PushDirect_ListenerTerminated_NoDelivery()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var lifetime = new Lifetime();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(lifetime, queueId, msg => {
            lock (received)
                received.Add(msg);
        });

        // Terminate the listener
        lifetime.Terminate();

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "ghost" });
        await DrainSideEffectsAsync();

        // Small delay to ensure no late delivery
        await Task.Delay(100);
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task PushTransactional_WithinTransaction_DeliveredAfterCommit()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => received.TrySetResult(msg));

        await RunTransaction(() => {
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-msg", Sequence = 99 });
            return Task.CompletedTask;
        });

        await DrainSideEffectsAsync();

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("tx-msg");
        result.Sequence.Should().Be(99);
    }

    [Fact]
    public async Task PushTransactional_TransactionRollback_NotDelivered()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => {
            lock (received)
                received.Add(msg);
        });

        var transactions = GetSiloService<ITransactions>();

        var result = await transactions.Run(() => {
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "should-not-arrive" });
            throw new Exception("Intentional rollback");
            return Task.CompletedTask;
        });

        result.IsSuccess.Should().BeFalse();

        // Drain — should find nothing because transaction rolled back
        var drain = await Pipeline!.DrainUntilQuietAsync();
        drain.TotalTasks.Should().Be(0);

        await Task.Delay(200);
        received.Should().BeEmpty();
    }

    [Fact]
    public async Task PushTransactional_MultipleMessagesInTransaction_AllDelivered()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var received = new List<TestMessage>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => {
            lock (received)
            {
                received.Add(msg);

                if (received.Count >= 3)
                    allReceived.TrySetResult();
            }
        });

        await RunTransaction(() => {
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-0", Sequence = 0 });
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-1", Sequence = 1 });
            messaging.PushTransactionalQueue(queueId, new TestMessage { Text = "tx-2", Sequence = 2 });
            return Task.CompletedTask;
        });

        await DrainSideEffectsAsync();
        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        received.Should().HaveCount(3);
        received.Select(m => m.Sequence).Should().BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public async Task PushDirect_NoListeners_DoesNotBufferMessages()
    {
        var queueId = new TestQueueId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        // Push to a queue nobody is listening to
        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "old" });
        await DrainSideEffectsAsync();

        // Subscribe after the push — should NOT receive the old message
        var received = new List<TestMessage>();

        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => {
            lock (received)
                received.Add(msg);
        });

        await Task.Delay(200);
        received.Should().BeEmpty("messages pushed with no listeners should not be buffered");

        // Verify queue is still functional — new message should arrive
        var done = new TaskCompletionSource<TestMessage>();
        await messaging.ListenDurableQueue<TestMessage>(new Lifetime(), queueId, msg => done.TrySetResult(msg));

        await messaging.PushDirectQueue(queueId, new TestMessage { Text = "new", Sequence = 1 });
        await DrainSideEffectsAsync();

        var result = await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("new");
    }
}