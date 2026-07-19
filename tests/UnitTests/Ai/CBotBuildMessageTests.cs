using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

public sealed class CBotBuildMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_all_fields_and_the_timestamp()
    {
        var projectId = CBotSourceProjectId.New();
        var userId = UserId.New();

        var message = CBotBuildMessage.Create(projectId, userId, CBotBuildRole.User, "make it trend-following", Now);

        message.ProjectId.Should().Be(projectId);
        message.UserId.Should().Be(userId);
        message.Role.Should().Be(CBotBuildRole.User);
        message.Content.Should().Be("make it trend-following");
        message.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_coalesces_null_content_to_empty()
    {
        var message = CBotBuildMessage.Create(
            CBotSourceProjectId.New(), UserId.New(), CBotBuildRole.Assistant, null!, Now);

        message.Content.Should().BeEmpty();
        message.Role.Should().Be(CBotBuildRole.Assistant);
    }
}
