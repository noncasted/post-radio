using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimeChannel: pub/sub broadcast, multiple subscribers, listener isolation.
/// RuntimeChannel is in-memory (no side effects needed).
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Publish_SingleSubscriber_ReceivesMessage()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "broadcast", Sequence = 1 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("broadcast");
        result.Sequence.Should().Be(1);
    }

    [Fact]
    public async Task Publish_MultipleSubscribers_AllReceive()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received1 = new TaskCompletionSource<TestMessage>();
        var received2 = new TaskCompletionSource<TestMessage>();
        var received3 = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received1.TrySetResult(msg));
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received2.TrySetResult(msg));
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received3.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "all", Sequence = 7 });

        var r1 = await received1.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var r2 = await received2.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var r3 = await received3.Task.WaitAsync(TimeSpan.FromSeconds(5));

        r1.Text.Should().Be("all");
        r2.Text.Should().Be("all");
        r3.Text.Should().Be("all");
    }

    [Fact]
    public async Task Publish_MultipleMessages_AllDeliveredInOrder()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<int>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => {
            lock (received)
            {
                received.Add(msg.Sequence);

                if (received.Count >= 10)
                    allReceived.TrySetResult();
            }
        });

        for (var i = 0; i < 10; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Sequence = i });

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().HaveCount(10);
        received.Should().Equal(Enumerable.Range(0, 10).ToList());
    }

    [Fact]
    public async Task Publish_DifferentChannels_Isolated()
    {
        var channelA = new TestChannelId(Guid.NewGuid().ToString());
        var channelB = new TestChannelId(Guid.NewGuid().ToString());
        var receivedA = new List<string>();
        var receivedB = new List<string>();
        var doneA = new TaskCompletionSource();
        var doneB = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelA, msg => {
            lock (receivedA)
            {
                receivedA.Add(msg.Text);
                doneA.TrySetResult();
            }
        });

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelB, msg => {
            lock (receivedB)
            {
                receivedB.Add(msg.Text);
                doneB.TrySetResult();
            }
        });

        await messaging.PublishChannel(channelA, new TestMessage { Text = "for-A" });
        await messaging.PublishChannel(channelB, new TestMessage { Text = "for-B" });

        await doneA.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await doneB.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedA.Should().ContainSingle().Which.Should().Be("for-A");
        receivedB.Should().ContainSingle().Which.Should().Be("for-B");
    }

    [Fact]
    public async Task Publish_TerminatedListener_NoDelivery()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<string>();
        var lifetime = new Lifetime();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(lifetime, channelId, msg => {
            lock (received)
                received.Add(msg.Text);
        });

        // Terminate — unsubscribes
        lifetime.Terminate();

        await messaging.PublishChannel(channelId, new TestMessage { Text = "ghost" });
        await Task.Delay(100);

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task Publish_NoSubscribers_ChannelRemainsFunctional()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        // Publish to empty channel — should not throw
        await messaging.PublishChannel(channelId, new TestMessage { Text = "void" });

        // Channel should still work: subscribe and publish a new message
        var received = new TaskCompletionSource<TestMessage>();
        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "after-empty", Sequence = 42 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("after-empty");
        result.Sequence.Should().Be(42);
    }

    [Fact]
    public async Task Publish_SubscriberAddedAfterPublish_DoesNotReceiveOldMessage()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        // Publish before anyone is listening
        await messaging.PublishChannel(channelId, new TestMessage { Text = "old" });

        // Now subscribe
        var received = new List<string>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => {
            lock (received)
                received.Add(msg.Text);
        });

        // Wait a bit to ensure no late delivery of the old message
        await Task.Delay(200);
        received.Should().BeEmpty();

        // New message should arrive
        var done = new TaskCompletionSource();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => {
            if (msg.Text == "new")
                done.TrySetResult();
        });

        await messaging.PublishChannel(channelId, new TestMessage { Text = "new" });
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Publish_ConcurrentRapidFire_AllDelivered()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new List<int>();
        var allReceived = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();
        const int messageCount = 20;

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => {
            lock (received)
            {
                received.Add(msg.Sequence);

                if (received.Count >= messageCount)
                    allReceived.TrySetResult();
            }
        });

        // Fire all publishes concurrently
        var tasks = Enumerable.Range(0, messageCount)
                              .Select(i => messaging.PublishChannel(channelId, new TestMessage { Sequence = i }))
                              .ToList();
        await Task.WhenAll(tasks);

        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        received.Should().HaveCount(messageCount);
        received.Should().BeEquivalentTo(Enumerable.Range(0, messageCount));
    }

    [Fact]
    public async Task Publish_PartialTermination_RemainingListenersStillReceive()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var lifetime1 = new Lifetime();
        var received1 = new List<string>();
        var received2 = new List<string>();
        var done2 = new TaskCompletionSource();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(lifetime1, channelId, msg => {
            lock (received1)
                received1.Add(msg.Text);
        });

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => {
            lock (received2)
            {
                received2.Add(msg.Text);
                done2.TrySetResult();
            }
        });

        // Terminate first listener
        lifetime1.Terminate();

        await messaging.PublishChannel(channelId, new TestMessage { Text = "after-terminate" });
        await done2.Task.WaitAsync(TimeSpan.FromSeconds(5));

        received1.Should().BeEmpty();
        received2.Should().ContainSingle().Which.Should().Be("after-terminate");
    }
}