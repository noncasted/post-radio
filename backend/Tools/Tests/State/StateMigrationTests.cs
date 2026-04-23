using FluentAssertions;
using Infrastructure.State;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests state version migration: write V0, read V1/V2, verify migration applies.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateMigrationTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task StateMigration_WriteV0ReadV1_MigratesCorrectly()
    {
        var key = Guid.NewGuid().ToString();
        const int writtenValue = 42;
        const string expectedLabel = "migrated-42";

        var v0Grain = GetGrain<IMigrationGrainV0>(key);
        await v0Grain.Write(writtenValue);

        var v1Grain = GetGrain<IMigrationGrainV1>(key);
        var (value, label) = await v1Grain.Read();

        value.Should().Be(writtenValue);
        label.Should().Be(expectedLabel);
    }

    [Fact]
    public async Task StateMigration_WriteV0ReadV2_MigratesSequentially()
    {
        var key = Guid.NewGuid().ToString();
        const int writtenValue = 7;

        var v0Grain = GetGrain<IMigrationGrainV0>(key);
        await v0Grain.Write(writtenValue);

        var v2Grain = GetGrain<IMigrationGrainV2>(key);
        var (value, label, doubledValue) = await v2Grain.Read();

        value.Should().Be(writtenValue);
        label.Should().Be("migrated-7");
        doubledValue.Should().Be(14);
    }

    [Fact]
    public async Task StateMigration_NoData_ReturnsDefaultState()
    {
        var key = Guid.NewGuid().ToString();

        var v1Grain = GetGrain<IMigrationGrainV1>(key);
        var (value, label) = await v1Grain.Read();

        value.Should().Be(0);
        label.Should().BeEmpty();
    }

    [Fact]
    public void StateMigration_GetLatestVersion_ReturnsHighestVersion()
    {
        var migrations = GetSiloService<IStateMigrations>();

        var latestV1 = migrations.GetLatestVersion<MigrationTestState_1>();
        var latestV2 = migrations.GetLatestVersion<MigrationTestState_2>();

        latestV1.Should().Be(1);
        latestV2.Should().Be(2);
    }

    [Fact]
    public void StateMigration_NoMigrations_ReturnsZero()
    {
        var migrations = GetSiloService<IStateMigrations>();

        var latest = migrations.GetLatestVersion<SimpleTestState>();

        latest.Should().Be(0);
    }
}