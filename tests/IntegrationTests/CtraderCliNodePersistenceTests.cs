using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class CtraderCliNodePersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task CtraderCliNode_BaseUrl_and_ApiSecret_survive_a_round_trip()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var node = new ActiveRunNode
        {
            Name = $"remote-{Guid.NewGuid():N}",
            BaseUrl = "http://10.20.30.40:8080",
            EncryptedApiSecret = "secret-bytes"u8.ToArray(),
            DataDirPath = "/var/app/remote"
        };

        await using (var writeContext = CreateContext())
        {
            writeContext.Nodes.Add(node);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var reloaded = await readContext.Nodes.OfType<ActiveRunNode>()
            .FirstAsync(n => n.Id == node.Id);

        reloaded.BaseUrl.Should().Be("http://10.20.30.40:8080");
        reloaded.EncryptedApiSecret.Should().Equal("secret-bytes"u8.ToArray());
    }
}
