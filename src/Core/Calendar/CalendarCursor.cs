using System.Text;

namespace Core.Calendar;

/// <summary>
/// An opaque keyset pagination cursor over <c>(EffectiveAt, Id)</c> — stable for large history pulls (unlike
/// offset paging, it never skips or repeats a row as data is appended). Encoded as base64 so it is a single
/// URL-safe token; the client treats it as opaque.
/// </summary>
public static class CalendarCursor
{
    public static string Encode(DateTimeOffset effectiveAt, Guid id)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{effectiveAt.UtcTicks}:{id}"));

    public static (DateTimeOffset EffectiveAt, Guid Id)? TryDecode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split(':', 2);
            if (parts.Length == 2 && long.TryParse(parts[0], out var ticks) && Guid.TryParse(parts[1], out var id))
                return (new DateTimeOffset(ticks, TimeSpan.Zero), id);
        }
        catch (FormatException)
        {
            // A malformed cursor is treated as "no cursor" — the caller starts from the beginning.
        }

        return null;
    }
}
