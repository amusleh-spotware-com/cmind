using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

public class MfaBackupCodesTests
{
    [Fact]
    public void Generate_returns_requested_count_and_length()
    {
        var codes = MfaBackupCodes.Generate(10, 10);
        codes.Should().HaveCount(10);
        codes.Should().OnlyContain(c => c.Length == 10);
    }

    [Fact]
    public void Generated_codes_avoid_ambiguous_characters()
    {
        var codes = MfaBackupCodes.Generate(50, 10);
        codes.SelectMany(c => c).Should().OnlyContain(ch => !"01ILO".Contains(ch));
    }

    [Fact]
    public void Hash_is_deterministic_and_separator_insensitive()
    {
        MfaBackupCodes.Hash("ABCDE-FGHJK").Should().Be(MfaBackupCodes.Hash("abcdefghjk"));
    }

    [Fact]
    public void Hash_differs_for_different_codes()
    {
        MfaBackupCodes.Hash("AAAAABBBBB").Should().NotBe(MfaBackupCodes.Hash("BBBBBAAAAA"));
    }

    [Fact]
    public void Format_groups_ten_char_code_with_dash()
    {
        MfaBackupCodes.Format("ABCDEFGHJK").Should().Be("ABCDE-FGHJK");
    }
}
