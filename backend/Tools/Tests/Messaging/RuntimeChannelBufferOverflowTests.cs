using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimeChannel circular buffer overflow and gap detection edge cases.
/// Covers scenarios where published messages exceed CatchUpBufferSize.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimeChannelBufferOverflowTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task CatchUp_BufferOverflow_DetectsGap()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 100;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // CatchUp from sequence 0 — far behind oldest available
        var result = await channel.CatchUp(0);

        result.GapDetected.Should().BeTrue();
        result.CurrentSequence.Should().Be(totalMessages);
        result.Messages.Should().HaveCount(bufferSize);
    }

    [Fact]
    public async Task CatchUp_AfterOverflow_GapDetectedForStaleSequence()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 50;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // lastSeenSequence=5 is far behind oldest in buffer
        var result = await channel.CatchUp(5);

        result.GapDetected.Should().BeTrue();
        result.Messages.Should().NotBeEmpty();
        result.Messages.Count.Should().BeLessThanOrEqualTo(bufferSize);
    }

    [Fact]
    public async Task CatchUp_AfterOverflow_MessagesHaveCorrectSequentialSequences()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 77;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        var result = await channel.CatchUp(0);

        result.GapDetected.Should().BeTrue();
        result.Messages.Should().HaveCount(bufferSize);

        // Sequences must be strictly sequential with no corruption from modulo wraparound
        var expectedStart = totalMessages - bufferSize + 1;

        for (var i = 0; i < result.Messages.Count; i++)
            result.Messages[i].Sequence.Should().Be(expectedStart + i);
    }

    [Fact]
    public async Task CatchUp_AtExactBoundary_NoGapDetected()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 10;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // oldestInBuffer = totalMessages - bufferSize + 1
        // lastSeenSequence = oldestInBuffer - 1 is the exact boundary (no gap)
        var oldestInBuffer = totalMessages - bufferSize + 1;
        var lastSeenAtBoundary = oldestInBuffer - 1;

        var result = await channel.CatchUp(lastSeenAtBoundary);

        result.GapDetected.Should().BeFalse();
        result.Messages.Should().HaveCount(bufferSize);
        result.Messages[0].Sequence.Should().Be(oldestInBuffer);
    }

    [Fact]
    public async Task CatchUp_OneBeforeBoundary_GapDetected()
    {
        var channelId = Guid.NewGuid().ToString();
        var channel = GetGrain<IRuntimeChannel>(channelId);
        var bufferSize = GetSiloService<IRuntimeChannelConfig>().Value.CatchUpBufferSize;
        var totalMessages = bufferSize + 10;

        for (var i = 1; i <= totalMessages; i++)
            await channel.Publish(new TestMessage { Text = $"msg-{i}", Sequence = i });

        // lastSeenSequence = oldestInBuffer - 2 means one message before boundary is missing
        var oldestInBuffer = totalMessages - bufferSize + 1;
        var lastSeenOneBeforeBoundary = oldestInBuffer - 2;

        var result = await channel.CatchUp(lastSeenOneBeforeBoundary);

        result.GapDetected.Should().BeTrue();
        result.CurrentSequence.Should().Be(totalMessages);
    }
}