using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimePipe retry with exponential backoff.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimePipeRetryTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Send_HandlerFailsOnceThenSucceeds_ReturnsResponse()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();
        var callCount = 0;

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId, req => {
            var attempt = Interlocked.Increment(ref callCount);

            if (attempt == 1)
                throw new Exception("transient failure");

            return Task.FromResult(new TestResponse { Answer = $"ok-{attempt}" });
        });

        var response = await messaging.SendPipe<TestResponse>(pipeId,
            new TestRequest { Question = "retry-me" });

        response.Answer.Should().StartWith("ok");
        callCount.Should().BeGreaterThan(1);
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_NoHandler_AllRetriesExhausted_Throws()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        var act = () => messaging.SendPipe<TestResponse>(pipeId,
            new TestRequest { Question = "no-handler" });

        await act.Should().ThrowAsync<Exception>();
    }
}