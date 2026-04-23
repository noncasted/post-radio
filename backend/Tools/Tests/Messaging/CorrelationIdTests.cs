using FluentAssertions;
using Infrastructure;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests ICorrelatedSideEffect and CorrelationId on DurableQueueSideEffect.
/// Unit tests — no cluster needed.
/// </summary>
public class CorrelationIdTests
{
    [Fact]
    public void DurableQueueSideEffect_ImplementsICorrelatedSideEffect()
    {
        var sideEffect = new DurableQueueSideEffect
        {
            QueueName = "test",
            Message = "payload",
            CorrelationId = Guid.NewGuid()
        };

        sideEffect.Should().BeAssignableTo<ICorrelatedSideEffect>();
    }

    [Fact]
    public void DurableQueueSideEffect_CorrelationId_IsPreserved()
    {
        var id = Guid.NewGuid();

        var sideEffect = new DurableQueueSideEffect
        {
            QueueName = "test",
            Message = "payload",
            CorrelationId = id
        };

        sideEffect.CorrelationId.Should().Be(id);
    }

    [Fact]
    public void DurableQueueSideEffect_DefaultCorrelationId_IsEmpty()
    {
        var sideEffect = new DurableQueueSideEffect
        {
            QueueName = "test",
            Message = "payload"
        };

        sideEffect.CorrelationId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ICorrelatedSideEffect_CastFromISideEffect_Works()
    {
        ISideEffect sideEffect = new DurableQueueSideEffect
        {
            QueueName = "test",
            Message = "payload",
            CorrelationId = Guid.NewGuid()
        };

        var correlated = sideEffect as ICorrelatedSideEffect;
        correlated.Should().NotBeNull();
        correlated!.CorrelationId.Should().NotBe(Guid.Empty);
    }
}