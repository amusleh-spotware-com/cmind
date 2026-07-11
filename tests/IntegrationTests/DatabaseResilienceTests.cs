using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public sealed class DatabaseResilienceTests
{
    [Fact]
    public void UseAppNpgsql_configures_a_retrying_execution_strategy()
    {
        var builder = new DbContextOptionsBuilder<DataContext>();
        builder.UseAppNpgsql("Host=localhost;Database=resilience-test;Username=x;Password=y");

        using var context = new DataContext(builder.Options);

        // A retrying strategy re-runs a transaction after a transient disconnect / failover; the default
        // NonRetryingExecutionStrategy would surface the error to the caller instead.
        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
    }
}
