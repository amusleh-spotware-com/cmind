using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Compliance;

public class LegalDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Draft_rejects_invalid_version()
    {
        var act = () => LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 0, "body");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.LegalDocumentVersionInvalid);
    }

    [Fact]
    public void Draft_rejects_empty_body()
    {
        var act = () => LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 1, "  ");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.LegalDocumentBodyRequired);
    }

    [Fact]
    public void Publish_marks_published_with_timestamp()
    {
        var doc = LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 1, "risk warning");
        doc.Publish(Now);
        doc.Published.Should().BeTrue();
        doc.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Publishing_twice_throws()
    {
        var doc = LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 1, "risk warning");
        doc.Publish(Now);
        var act = () => doc.Publish(Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.LegalDocumentAlreadyPublished);
    }

    [Fact]
    public void Editing_a_published_document_throws()
    {
        var doc = LegalDocument.Draft(LegalDocumentType.RiskDisclosure, 1, "risk warning");
        doc.Publish(Now);
        var act = () => doc.UpdateBody("changed");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.LegalDocumentAlreadyPublished);
    }

    [Fact]
    public void Consent_accept_captures_version_time_and_ip()
    {
        var consent = ConsentRecord.Accept(UserId.New(), LegalDocumentType.RiskDisclosure, 2, Now, "1.2.3.4");
        consent.Version.Should().Be(2);
        consent.AcceptedAt.Should().Be(Now);
        consent.Ip.Should().Be("1.2.3.4");
    }
}
