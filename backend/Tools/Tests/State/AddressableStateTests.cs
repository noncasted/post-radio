using Common.Reactive;
using FluentAssertions;
using Tests.Fixtures;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests ViewableProperty subscription behavior through AddressableState:
/// View vs Advise semantics, lifetime-based cleanup, subscriber notifications.
/// </summary>
public class AddressableStateTests
{
    public class TestConfig
    {
        public string Label { get; set; } = string.Empty;
        public int MaxRetries { get; set; }

        public override string ToString() => $"Label={Label}, MaxRetries={MaxRetries}";
    }

    [Fact]
    public async Task TestAddressableState_SetValue_NotifiesSubscribers()
    {
        var state = new TestAddressableState<TestConfig>();

        var lifetime = new Lifetime();
        var received = new List<TestConfig>();
        state.View(lifetime, value => received.Add(value));

        // View fires immediately with default
        received.Should().HaveCount(1);
        received[0].Label.Should().BeEmpty();

        // Set new value — subscriber receives update
        var newConfig = new TestConfig { Label = "notify", MaxRetries = 3 };
        await state.SetValue(newConfig);

        received.Should().HaveCount(2);
        received[1].Label.Should().Be("notify");
        received[1].MaxRetries.Should().Be(3);

        lifetime.Terminate();
    }

    [Fact]
    public async Task TestAddressableState_SubscriberTerminated_NoMoreNotifications()
    {
        var state = new TestAddressableState<TestConfig>();

        var lifetime = new Lifetime();
        var received = new List<TestConfig>();
        state.Advise(lifetime, (_, value) => received.Add(value));

        // Advise does NOT fire immediately (unlike View)
        received.Should().BeEmpty();

        await state.SetValue(new TestConfig { Label = "first" });
        received.Should().HaveCount(1);

        // Terminate lifetime — no more notifications
        lifetime.Terminate();

        await state.SetValue(new TestConfig { Label = "second" });
        received.Should().HaveCount(1); // Still 1, not 2
    }

    [Fact]
    public async Task TestAddressableState_ViewVsAdvise_ViewFiresImmediately()
    {
        var state = new TestAddressableState<TestConfig>(new TestConfig { Label = "initial" });

        var lifetime = new Lifetime();

        var viewResults = new List<string>();
        var adviseResults = new List<string>();

        state.View(lifetime, value => viewResults.Add(value.Label));
        state.Advise(lifetime, (_, value) => adviseResults.Add(value.Label));

        // View fired immediately, Advise did not
        viewResults.Should().Equal("initial");
        adviseResults.Should().BeEmpty();

        await state.SetValue(new TestConfig { Label = "updated" });

        viewResults.Should().Equal("initial", "updated");
        adviseResults.Should().Equal("updated");

        lifetime.Terminate();
    }
}