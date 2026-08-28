using Cluster.Deploy;
using Cluster.Discovery;
using FluentAssertions;
using Meta.Online;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Tests.Meta.Online;

public class OnlineTrackerTests
{
    [Fact]
    public void CountsOneListenerPerClientId()
    {
        var tracker = CreateTracker();

        tracker.Touch("device-a");
        tracker.Touch("device-a");
        tracker.Touch("device-b");

        tracker.GetSnapshot().Count.Should().Be(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IgnoresBeatsWithoutClientId(string? clientId)
    {
        var tracker = CreateTracker();

        tracker.Touch(clientId);

        tracker.GetSnapshot().Count.Should().Be(0);
    }

    [Fact]
    public void IgnoresOversizedClientId()
    {
        var tracker = CreateTracker();

        tracker.Touch(new string('a', 129));

        tracker.GetSnapshot().Count.Should().Be(0);
    }

    [Fact]
    public void DoesNotExposeRawClientId()
    {
        var tracker = CreateTracker();

        tracker.Touch("device-a");

        tracker.GetSnapshot().Listeners.Single().ClientId.Should().NotBe("device-a");
    }

    [Fact]
    public void RestoresHourlyHistory()
    {
        var tracker = CreateTracker();
        var hour = ToHourBucket(DateTime.UtcNow.AddHours(-2));

        tracker.Restore([new OnlineHistoryBucket { BucketStartUtc = hour, PeakCount = 7 }]);

        tracker.GetSnapshot().Hourly.Single(bucket => bucket.BucketStartUtc == hour).PeakCount.Should().Be(7);
    }

    [Fact]
    public void DropsRestoredBucketsOutsideTheChartWindow()
    {
        var tracker = CreateTracker();
        var hour = ToHourBucket(DateTime.UtcNow.AddHours(-30));

        tracker.Restore([new OnlineHistoryBucket { BucketStartUtc = hour, PeakCount = 7 }]);

        tracker.GetSnapshot().Hourly.Should().NotContain(bucket => bucket.BucketStartUtc == hour);
    }

    [Fact]
    public void KeepsTheHigherPeakWhenRestoreOverlapsLiveSamples()
    {
        var tracker = CreateTracker();

        tracker.Touch("device-a");
        tracker.GetSnapshot();

        var hour = ToHourBucket(DateTime.UtcNow);
        tracker.Restore([new OnlineHistoryBucket { BucketStartUtc = hour, PeakCount = 42 }]);

        tracker.GetSnapshot().Hourly.Single(bucket => bucket.BucketStartUtc == hour).PeakCount.Should().Be(42);
    }

    private static OnlineTracker CreateTracker()
    {
        return new OnlineTracker(
            Substitute.For<ILiveState<OnlineLiveData>>(),
            Substitute.For<IServiceEnvironment>(),
            NullLogger<OnlineTracker>.Instance);
    }

    private static DateTime ToHourBucket(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
    }
}
