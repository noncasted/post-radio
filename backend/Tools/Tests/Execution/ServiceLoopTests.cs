using Common.Reactive;
using FluentAssertions;
using Infrastructure;
using Xunit;

namespace Tests.Execution;

public class ServiceLoopTests
{
    [Fact]
    public async Task OnOrleansStarted_CallsAllParticipants()
    {
        var p1 = new FakeOrleansStarted();
        var p2 = new FakeOrleansStarted();

        var loop = new ServiceLoop(new IOrleansStarted[] { p1, p2 },
            Array.Empty<ILocalSetupCompleted>(),
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = new Lifetime();
        await loop.OnOrleansStarted(lifetime);

        p1.WasCalled.Should().BeTrue();
        p2.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnLocalSetupCompleted_CallsAllParticipants()
    {
        var p1 = new FakeLocalSetup();
        var p2 = new FakeLocalSetup();

        var loop = new ServiceLoop(Array.Empty<IOrleansStarted>(),
            new ILocalSetupCompleted[] { p1, p2 },
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = new Lifetime();
        await loop.OnLocalSetupCompleted(lifetime);

        p1.WasCalled.Should().BeTrue();
        p2.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnCoordinatorSetupCompleted_CallsAllParticipants()
    {
        var p1 = new FakeCoordinatorSetup();
        var p2 = new FakeCoordinatorSetup();

        var loop = new ServiceLoop(Array.Empty<IOrleansStarted>(),
            Array.Empty<ILocalSetupCompleted>(),
            new ICoordinatorSetupCompleted[] { p1, p2 },
            Array.Empty<IServiceStarted>());

        var lifetime = new Lifetime();
        await loop.OnCoordinatorSetupCompleted(lifetime);

        p1.WasCalled.Should().BeTrue();
        p2.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnOrleansStarted_PassesLifetimeToParticipants()
    {
        var participant = new FakeOrleansStarted();

        var loop = new ServiceLoop(new IOrleansStarted[] { participant },
            Array.Empty<ILocalSetupCompleted>(),
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = new Lifetime();
        await loop.OnOrleansStarted(lifetime);

        participant.ReceivedLifetime.Should().BeSameAs(lifetime);
    }

    [Fact]
    public async Task HandlerFailure_DoesNotBlockOtherParticipants()
    {
        // ServiceLoop uses Task.WhenAll — when a handler returns a faulted Task,
        // other handlers still execute
        var failing = new FailingOrleansStarted();
        var successful = new FakeOrleansStarted();

        var loop = new ServiceLoop(new IOrleansStarted[] { failing, successful },
            Array.Empty<ILocalSetupCompleted>(),
            Array.Empty<ICoordinatorSetupCompleted>(),
            Array.Empty<IServiceStarted>());

        var lifetime = new Lifetime();
        var act = () => loop.OnOrleansStarted(lifetime);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // The successful handler still ran (Task.WhenAll runs all in parallel)
        successful.WasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AllStages_IndependentOfEachOther()
    {
        var orleans = new FakeOrleansStarted();
        var local = new FakeLocalSetup();
        var coordinator = new FakeCoordinatorSetup();

        var loop = new ServiceLoop(new IOrleansStarted[] { orleans },
            new ILocalSetupCompleted[] { local },
            new ICoordinatorSetupCompleted[] { coordinator },
            Array.Empty<IServiceStarted>());

        var lifetime = new Lifetime();

        // Only call OnOrleansStarted
        await loop.OnOrleansStarted(lifetime);

        orleans.WasCalled.Should().BeTrue();
        local.WasCalled.Should().BeFalse();
        coordinator.WasCalled.Should().BeFalse();
    }
}

internal class FakeOrleansStarted : IOrleansStarted
{
    public bool WasCalled { get; private set; }
    public IReadOnlyLifetime? ReceivedLifetime { get; private set; }

    public Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        WasCalled = true;
        ReceivedLifetime = lifetime;
        return Task.CompletedTask;
    }
}

internal class FakeLocalSetup : ILocalSetupCompleted
{
    public bool WasCalled { get; private set; }

    public Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}

internal class FakeCoordinatorSetup : ICoordinatorSetupCompleted
{
    public bool WasCalled { get; private set; }

    public Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}

internal class FailingOrleansStarted : IOrleansStarted
{
    public async Task OnOrleansStarted(IReadOnlyLifetime lifetime)
    {
        await Task.Yield();
        throw new InvalidOperationException("Handler failed");
    }
}