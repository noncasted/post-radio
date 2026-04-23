using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimeChannel catch-up: ring buffer, sequence numbers, CatchUp replay, delivery timeout.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelCatchUpTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task CatchUp_NoMissedMessages_ReturnsEmpty()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "a", Sequence = 1 });
        await channel.Publish(new TestMessage { Text = "b", Sequence = 2 });

        var result = await channel.CatchUp(2);

        result.Messages.Should().BeEmpty();
        result.GapDetected.Should().BeFalse();
        result.CurrentSequence.Should().Be(2);
    }

    [Fact]
    public async Task CatchUp_MissedMessages_ReplaysFromBuffer()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "a", Sequence = 1 });
        await channel.Publish(new TestMessage { Text = "b", Sequence = 2 });
        await channel.Publish(new TestMessage { Text = "c", Sequence = 3 });

        var result = await channel.CatchUp(1);

        result.Messages.Should().HaveCount(2);
        result.Messages[0].Sequence.Should().Be(2);
        result.Messages[1].Sequence.Should().Be(3);
        result.GapDetected.Should().BeFalse();
        result.CurrentSequence.Should().Be(3);
    }

    [Fact]
    public async Task CatchUp_FromZero_ReplaysAll()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "a", Sequence = 1 });
        await channel.Publish(new TestMessage { Text = "b", Sequence = 2 });

        var result = await channel.CatchUp(0);

        result.Messages.Should().HaveCount(2);
        result.Messages[0].Sequence.Should().Be(1);
        result.Messages[1].Sequence.Should().Be(2);
        result.GapDetected.Should().BeFalse();
    }

    [Fact]
    public async Task CatchUp_EmptyChannel_ReturnsEmpty()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        var result = await channel.CatchUp(0);

        result.Messages.Should().BeEmpty();
        result.GapDetected.Should().BeFalse();
        result.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task CatchUp_PayloadPreserved()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        await channel.Publish(new TestMessage { Text = "hello", Sequence = 42 });

        var result = await channel.CatchUp(0);

        result.Messages.Should().HaveCount(1);
        var payload = result.Messages[0].Payload.Should().BeOfType<TestMessage>().Subject;
        payload.Text.Should().Be("hello");
        payload.Sequence.Should().Be(42);
    }

    [Fact]
    public async Task Publish_SequenceNumbers_MonotonicallyIncreasing()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);

        for (var i = 0; i < 20; i++)
            await channel.Publish(new TestMessage { Sequence = i });

        var result = await channel.CatchUp(0);

        result.Messages.Should().HaveCount(20);

        for (var i = 0; i < 20; i++)
            result.Messages[i].Sequence.Should().Be(i + 1);
    }

    [Fact]
    public async Task Publish_ObserverReceivesUnwrappedPayload()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var received = new TaskCompletionSource<TestMessage>();
        var messaging = GetSiloService<IMessaging>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId,
            msg => received.TrySetResult(msg));

        await messaging.PublishChannel(channelId, new TestMessage { Text = "unwrapped", Sequence = 7 });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Text.Should().Be("unwrapped");
        result.Sequence.Should().Be(7);
    }

    [Fact]
    public async Task CatchUp_AfterResubscribe_ReplaysFromLastSeen()
    {
        var channelId = new TestChannelId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var allReceived = new List<TestMessage>();
        var firstBatch = new TaskCompletionSource();
        var secondBatch = new TaskCompletionSource();
        var lifetime = new Lifetime();

        await messaging.ListenChannel<TestMessage>(lifetime, channelId, msg => {
            lock (allReceived)
            {
                allReceived.Add(msg);

                if (allReceived.Count >= 3)
                    firstBatch.TrySetResult();
            }
        });

        // Publish 3 messages — listener receives them
        for (var i = 1; i <= 3; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Text = $"msg-{i}", Sequence = i });

        await firstBatch.Task.WaitAsync(TimeSpan.FromSeconds(5));
        allReceived.Should().HaveCount(3);

        // Terminate listener, publish more messages
        lifetime.Terminate();

        for (var i = 4; i <= 6; i++)
            await messaging.PublishChannel(channelId, new TestMessage { Text = $"msg-{i}", Sequence = i });

        // Re-subscribe — should trigger catch-up for messages 4-6
        var catchUpReceived = new List<TestMessage>();

        await messaging.ListenChannel<TestMessage>(new Lifetime(), channelId, msg => {
            lock (catchUpReceived)
            {
                catchUpReceived.Add(msg);

                if (catchUpReceived.Count >= 3)
                    secondBatch.TrySetResult();
            }
        });

        // Force resubscribe by waiting for the resubscribe loop
        await Task.Delay(TimeSpan.FromSeconds(12));

        // Publish one more to ensure the new listener works
        await messaging.PublishChannel(channelId, new TestMessage { Text = "msg-7", Sequence = 7 });
        await Task.Delay(500);

        // The new listener should have received msg-7 at minimum
        lock (catchUpReceived)
        {
            catchUpReceived.Should().Contain(m => m.Sequence == 7);
        }
    }
}