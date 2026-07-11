using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class CompliancePersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 07, 11, 12, 0, 0, TimeSpan.Zero);

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Active_document_is_highest_published_version_and_consent_is_queryable()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"legal-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());

        var v1 = LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 1, "v1");
        v1.Publish(Now);
        var v2 = LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 2, "v2");
        v2.Publish(Now);
        var draft = LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 3, "v3-unpublished");

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.LegalDocuments.AddRange(v1, v2, draft);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var repo = new LegalDocumentRepository(read);
            var active = await repo.GetActiveAsync(LegalDocumentType.RiskDisclosure, CancellationToken.None);
            active!.Version.Should().Be(2, "the highest *published* version wins over the unpublished draft");
        }

        await using (var consentCtx = CreateContext())
        {
            var repo = new ConsentRepository(consentCtx);
            await repo.AddAsync(ConsentRecord.Accept(user.Id, LegalDocumentType.RiskDisclosure, 2, Now, "127.0.0.1"),
                CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        await using (var verify = CreateContext())
        {
            var repo = new ConsentRepository(verify);
            (await repo.HasConsentAsync(user.Id, LegalDocumentType.RiskDisclosure, 2, CancellationToken.None))
                .Should().BeTrue();
            (await repo.HasConsentAsync(user.Id, LegalDocumentType.RiskDisclosure, 3, CancellationToken.None))
                .Should().BeFalse();
        }
    }
}
