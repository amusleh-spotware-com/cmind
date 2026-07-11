using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core;

public enum LegalDocumentType
{
    TermsOfService,
    RiskDisclosure,
    PrivacyPolicy
}

/// <summary>
/// A versioned legal document (terms, CFD risk disclosure, privacy policy). A new version is drafted, then
/// published; published versions are immutable so the exact text a user consented to is always recoverable
/// (MiFID/ESMA record-keeping). The active document for a type is its highest published version.
/// </summary>
public sealed class LegalDocument : AuditedEntity<LegalDocumentId>
{
    public LegalDocumentType Type { get; private set; }
    public int Version { get; private set; }
    public string Body { get; private set; } = default!;
    public bool Published { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    private LegalDocument()
    {
    }

    public static LegalDocument Draft(LegalDocumentType type, int version, string body)
    {
        if (version < 1) throw new DomainException(DomainErrors.LegalDocumentVersionInvalid);
        return new LegalDocument
        {
            Type = type,
            Version = version,
            Body = DomainGuard.AgainstNullOrWhiteSpace(body, DomainErrors.LegalDocumentBodyRequired)
        };
    }

    public void UpdateBody(string body)
    {
        if (Published) throw new DomainException(DomainErrors.LegalDocumentAlreadyPublished);
        Body = DomainGuard.AgainstNullOrWhiteSpace(body, DomainErrors.LegalDocumentBodyRequired);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Published) throw new DomainException(DomainErrors.LegalDocumentAlreadyPublished);
        Published = true;
        PublishedAt = now;
    }
}

/// <summary>
/// An immutable record that a user accepted a specific version of a legal document at a point in time
/// (with the originating IP for audit). Consent is "current" for a document type when the user has a record
/// for that type's active published version.
/// </summary>
public sealed class ConsentRecord : AuditedEntity<ConsentRecordId>
{
    public UserId UserId { get; private set; }
    public LegalDocumentType DocumentType { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset AcceptedAt { get; private set; }
    [MaxLength(45)] public string? Ip { get; private set; }

    private ConsentRecord()
    {
    }

    public static ConsentRecord Accept(UserId userId, LegalDocumentType type, int version, DateTimeOffset now, string? ip)
    {
        if (version < 1) throw new DomainException(DomainErrors.LegalDocumentVersionInvalid);
        return new ConsentRecord
        {
            UserId = userId,
            DocumentType = type,
            Version = version,
            AcceptedAt = now,
            Ip = ip
        };
    }
}
