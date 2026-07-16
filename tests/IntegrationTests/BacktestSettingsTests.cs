using FluentAssertions;
using Web.Json;
using Xunit;

namespace IntegrationTests;

// Pure test of the dialog settings builder (lives here because it references the Web assembly; no DB).
public class BacktestSettingsTests
{
    [Fact]
    public void ToJson_omits_spread_when_blank_so_tick_mode_derives_it()
    {
        var json = BacktestSettings.ToJson(null, null, "tick", "10000", "0", spread: null);

        json.Should().NotContain("spread");
        json.Should().Contain("\"dataMode\":\"tick\"");
    }

    [Fact]
    public void ToJson_includes_an_explicit_spread()
    {
        var json = BacktestSettings.ToJson(null, null, "m1", "10000", "0", spread: "1.5");

        json.Should().Contain("\"spread\":\"1.5\"");
    }
}
