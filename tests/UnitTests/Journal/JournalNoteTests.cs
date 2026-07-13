using Core;
using Core.Domain;
using Core.Journal;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class JournalNoteTests
{
    private static readonly UserId Owner = UserId.New();

    [Fact]
    public void Create_trims_title_body_and_uppercases_symbol()
    {
        var note = JournalNote.Create(Owner, "  My lesson  ", "  Kept risk too high  ", " eurusd ");

        note.UserId.Should().Be(Owner);
        note.Title.Should().Be("My lesson");
        note.Body.Should().Be("Kept risk too high");
        note.Symbol.Should().Be("EURUSD");
    }

    [Fact]
    public void Create_treats_blank_body_and_symbol_as_none()
    {
        var note = JournalNote.Create(Owner, "Title", "   ", "   ");

        note.Body.Should().BeEmpty();
        note.Symbol.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_missing_title(string? title)
    {
        var act = () => JournalNote.Create(Owner, title!, "body", null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.JournalNoteTitleRequired);
    }

    [Fact]
    public void Create_rejects_body_over_the_maximum_length()
    {
        var tooLong = new string('x', JournalNote.MaxBodyLength + 1);
        var act = () => JournalNote.Create(Owner, "Title", tooLong, null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.JournalNoteBodyTooLong);
    }

    [Fact]
    public void Edit_updates_all_fields_and_re_enforces_invariants()
    {
        var note = JournalNote.Create(Owner, "Old", "old body", "eurusd");

        note.Edit("New title", "new body", "gbpusd");

        note.Title.Should().Be("New title");
        note.Body.Should().Be("new body");
        note.Symbol.Should().Be("GBPUSD");
    }

    [Fact]
    public void Edit_rejects_blank_title()
    {
        var note = JournalNote.Create(Owner, "Old", "b", null);
        var act = () => note.Edit("  ", "b", null);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.JournalNoteTitleRequired);
    }
}
