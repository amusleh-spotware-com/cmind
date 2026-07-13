using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

// Append-only InstanceLog record + the AppSetting key/value with timestamped updates. (WS-1 Core backfill.)
public class InstanceLogAndAppSettingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InstanceLog_create_captures_the_line()
    {
        var instanceId = InstanceId.New();

        var log = InstanceLog.Create(instanceId, Now, "err", "boom");

        log.InstanceId.Should().Be(instanceId);
        log.Time.Should().Be(Now);
        log.Stream.Should().Be("err");
        log.Line.Should().Be("boom");
        log.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void AppSetting_create_and_set_value_stamp_the_time()
    {
        var setting = AppSetting.Create("theme", "dark", Now);

        setting.Key.Should().Be("theme");
        setting.Value.Should().Be("dark");
        setting.UpdatedAt.Should().Be(Now);

        setting.SetValue("light", Now.AddMinutes(5));
        setting.Value.Should().Be("light");
        setting.UpdatedAt.Should().Be(Now.AddMinutes(5));
    }
}
