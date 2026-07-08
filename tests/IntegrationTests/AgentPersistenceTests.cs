using Core;
using Core.Agent;
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

        var user = new OwnerUser
        {
            Email = $"agent-{Guid.NewGuid():N}@test.local",
            NormalizedEmail = $"AGENT-{Guid.NewGuid():N}@TEST.LOCAL",
            PasswordHash = "x",
            SecurityStamp = Guid.NewGuid().ToByteArray()
        };
        var cbot = new CBot { UserId = user.Id, User = user, Name = $"bot-{Guid.NewGuid():N}", EncryptedAlgo = "algo"u8.ToArray() };
        var mandate = new AgentMandate
        {
            UserId = user.Id, CBotId = cbot.Id, Name = $"mandate-{Guid.NewGuid():N}",
            Objective = "grow safely", Autonomy = AgentAutonomy.Auto, Enabled = true
        };

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.CBots.Add(cbot);
            write.AgentMandates.Add(mandate);
            write.AgentProposals.Add(new AgentProposal
            {
                MandateId = mandate.Id, UserId = user.Id, Reasoning = "tighten stop",
                PayloadJson = "{\"StopLoss\":20}", ProposedName = "Tighter SL",
                Status = AgentProposalStatus.Pending
            });
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
