using Core.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CalendarCursorTests
{
    [Fact]
    public void Encode_then_decode_round_trips_the_instant_and_id()
    {
        var instant = new DateTimeOffset(2024, 2, 13, 13, 30, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();

        var decoded = CalendarCursor.TryDecode(CalendarCursor.Encode(instant, id));

        decoded.Should().NotBeNull();
        decoded!.Value.EffectiveAt.Should().Be(instant);
        decoded.Value.Id.Should().Be(id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64!!")]
    [InlineData("bm90LWEtY3Vyc29y")] // base64 of "not-a-cursor" — wrong shape
    public void TryDecode_returns_null_for_missing_or_malformed_cursors(string? cursor)
    {
        CalendarCursor.TryDecode(cursor).Should().BeNull();
    }
}
