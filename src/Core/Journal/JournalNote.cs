using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Journal;

/// <summary>
/// A manual, user-authored journal note — a free-text annotation the trader adds to reflect on their
/// own trading (a lesson learned, a rule broken, a plan for next time). Distinct from the derived
/// <see cref="JournalEntry"/> analysis rows, which are computed from instances. Aggregate root owned
/// by the authoring user; edited and deleted only through its own intention-revealing methods.
/// </summary>
public sealed class JournalNote : AuditedEntity<JournalNoteId>
{
    public const int MaxTitleLength = 128;
    public const int MaxBodyLength = 4000;
    public const int MaxSymbolLength = 32;

    public UserId UserId { get; private set; }
    [MaxLength(MaxTitleLength)] public string Title { get; private set; } = default!;
    [MaxLength(MaxBodyLength)] public string Body { get; private set; } = string.Empty;
    [MaxLength(MaxSymbolLength)] public string? Symbol { get; private set; }

    public static JournalNote Create(UserId userId, string title, string? body, string? symbol)
    {
        var note = new JournalNote { UserId = userId };
        note.SetTitle(title);
        note.SetBody(body);
        note.SetSymbol(symbol);
        return note;
    }

    public void Edit(string title, string? body, string? symbol)
    {
        SetTitle(title);
        SetBody(body);
        SetSymbol(symbol);
    }

    private void SetTitle(string title) =>
        Title = Guard(DomainGuard.AgainstNullOrWhiteSpace(title, DomainErrors.JournalNoteTitleRequired),
            MaxTitleLength);

    private void SetBody(string? body)
    {
        var trimmed = body?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxBodyLength) throw new DomainException(DomainErrors.JournalNoteBodyTooLong);
        Body = trimmed;
    }

    private void SetSymbol(string? symbol)
    {
        var trimmed = symbol?.Trim();
        Symbol = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxSymbolLength ? trimmed[..MaxSymbolLength].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }

    private static string Guard(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;
}
