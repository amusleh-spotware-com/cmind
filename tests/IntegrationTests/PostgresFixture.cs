using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => Container.StartAsync();
    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
