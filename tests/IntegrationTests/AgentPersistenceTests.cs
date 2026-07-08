using Core;
using Core.Agent;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class AgentPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .Options);

    [Fact]
    public async Task Mandate_and_proposal_round_trip_with_string_enums_and_soft_delete()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"agent-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var cbot = CBot.Create(user.Id, $"bot-{Guid.NewGuid():N}", "algo"u8.ToArray());
        var mandate = AgentMandate.Create(user.Id, cbot.Id, $"mandate-{Guid.NewGuid():N}", "grow safely",
            new RiskPercent(1), new DrawdownPercent(20), new Symbol("EURUSD"), new Timeframe("h1"),
            DockerImageTag.Latest, AgentAutonomy.Auto, null);
        mandate.Enable();
        mandate.AddProposal("Backtest", "tighten stop", "{\"StopLoss\":20}", "Tighter SL");

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.CBots.Add(cbot);
            write.AgentMandates.Add(mandate);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var reloaded = await read.AgentMandates.FirstAsync(m => m.Id == mandate.Id);
            reloaded.Autonomy.Should().Be(AgentAutonomy.Auto);
            reloaded.Enabled.Should().BeTrue();

            var proposal = await read.AgentProposals.FirstAsync(p => p.MandateId == mandate.Id);
            proposal.Status.Should().Be(AgentProposalStatus.Pending);
            proposal.PayloadJson.Should().Contain("StopLoss");

            var storedAutonomy = await read.Database
                .SqlQuery<string>($"SELECT \"Autonomy\" AS \"Value\" FROM \"AgentMandates\" WHERE \"Id\" = {mandate.Id.Value}")
                .FirstAsync();
            storedAutonomy.Should().Be("Auto");
        }

        await using (var delete = CreateContext())
        {
            var toRemove = await delete.AgentMandates.FirstAsync(m => m.Id == mandate.Id);
            delete.AgentMandates.Remove(toRemove);
            await delete.SaveChangesAsync();
        }

        await using var afterDelete = CreateContext();
        (await afterDelete.AgentMandates.AnyAsync(m => m.Id == mandate.Id)).Should().BeFalse();
    }
}
