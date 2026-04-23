using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Tests.Fixtures;
using Xunit;

namespace Tests.Messaging;

/// <summary>
/// Tests RuntimePipe: request-response pattern, handler binding, error propagation.
/// RuntimePipe is in-memory (no side effects needed).
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class RuntimePipeTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Send_WithHandler_ReturnsResponse()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId,
            req => Task.FromResult(new TestResponse { Answer = $"reply-to-{req.Question}" }));

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "hello" });

        response.Answer.Should().Be("reply-to-hello");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_MultipleRequests_EachGetsCorrectResponse()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId,
            req => Task.FromResult(new TestResponse { Answer = req.Question.ToUpper() }));

        var r1 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "one" });
        var r2 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "two" });
        var r3 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "three" });

        r1.Answer.Should().Be("ONE");
        r2.Answer.Should().Be("TWO");
        r3.Answer.Should().Be("THREE");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_NoHandler_ThrowsException()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();

        var act = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "nobody-home" });

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Send_HandlerThrows_PropagatesException()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId,
            _ => throw new InvalidOperationException("handler-error"));

        var act = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "boom" });

        await act.Should().ThrowAsync<Exception>().WithMessage("*handler-error*");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_AsyncHandler_AwaitsCorrectly()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId,
            async req => {
                await Task.Delay(50);
                return new TestResponse { Answer = $"delayed-{req.Question}" };
            });

        var response = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "wait" });

        response.Answer.Should().Be("delayed-wait");
        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_HandlerTerminated_ThrowsException()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId,
            req => Task.FromResult(new TestResponse { Answer = "ok" }));

        // Terminate the handler
        lifetime.Terminate();

        // Allow resubscribe loop to notice the termination
        await Task.Delay(200);

        var act = () => messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "gone" });
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Send_ReplaceHandler_NewHandlerResponds()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime1 = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime1, pipeId,
            req => Task.FromResult(new TestResponse { Answer = "v1" }));

        var r1 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "x" });
        r1.Answer.Should().Be("v1");

        // Terminate old handler and bind new one
        lifetime1.Terminate();
        var lifetime2 = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime2, pipeId,
            req => Task.FromResult(new TestResponse { Answer = "v2" }));

        var r2 = await messaging.SendPipe<TestResponse>(pipeId, new TestRequest { Question = "x" });
        r2.Answer.Should().Be("v2");

        lifetime2.Terminate();
    }

    [Fact]
    public async Task Send_ConcurrentRequests_AllGetCorrectResponses()
    {
        var pipeId = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetime = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetime, pipeId,
            async req => {
                await Task.Delay(10); // small delay to allow interleaving
                return new TestResponse { Answer = $"reply-{req.Question}" };
            });

        var tasks = Enumerable.Range(0, 5)
                              .Select(i => messaging.SendPipe<TestResponse>(pipeId,
                                  new TestRequest { Question = $"q{i}" }))
                              .ToList();

        var responses = await Task.WhenAll(tasks);

        for (var i = 0; i < 5; i++)
            responses[i].Answer.Should().Be($"reply-q{i}");

        lifetime.Terminate();
    }

    [Fact]
    public async Task Send_DifferentPipes_Isolated()
    {
        var pipeA = new TestPipeId(Guid.NewGuid().ToString());
        var pipeB = new TestPipeId(Guid.NewGuid().ToString());
        var messaging = GetSiloService<IMessaging>();
        var lifetimeA = new Lifetime();
        var lifetimeB = new Lifetime();

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetimeA, pipeA,
            req => Task.FromResult(new TestResponse { Answer = "from-A" }));

        await messaging.AddPipeRequestHandler<TestRequest, TestResponse>(lifetimeB, pipeB,
            req => Task.FromResult(new TestResponse { Answer = "from-B" }));

        var rA = await messaging.SendPipe<TestResponse>(pipeA, new TestRequest { Question = "x" });
        var rB = await messaging.SendPipe<TestResponse>(pipeB, new TestRequest { Question = "x" });

        rA.Answer.Should().Be("from-A");
        rB.Answer.Should().Be("from-B");

        lifetimeA.Terminate();
        lifetimeB.Terminate();
    }
}