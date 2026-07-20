using Core;
using Core.Ai;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    private static AiRun New(string title = "R1") =>
        AiRun.Create(UserId.New(), AiFeature.ReviewCBot, title, "CSharp", "public class Bot {}", Now);

    [Fact]
    public void Create_starts_running_with_the_input()
    {
        var run = New();
        run.Status.Should().Be(AiRunStatus.Running);
        run.Title.Should().Be("R1");
        run.Feature.Should().Be(AiFeature.ReviewCBot);
        run.Source.Should().Contain("Bot");
        run.CreatedAt.Should().Be(Now);
        run.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void Create_defaults_a_blank_title_to_the_feature_name()
    {
        New("   ").Title.Should().Be(nameof(AiFeature.ReviewCBot));
    }

    [Fact]
    public void Complete_stores_the_output_and_finishes()
    {
        var run = New();
        run.Complete("looks good", Now.AddSeconds(3));
        run.Status.Should().Be(AiRunStatus.Completed);
        run.Output.Should().Be("looks good");
        run.FinishedAt.Should().Be(Now.AddSeconds(3));
        run.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Fail_stores_the_error_and_finishes()
    {
        var run = New();
        run.Fail("boom", Now.AddSeconds(1));
        run.Status.Should().Be(AiRunStatus.Failed);
        run.Error.Should().Be("boom");
        run.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void A_terminal_run_cannot_transition_again()
    {
        var run = New();
        run.Complete("done", Now);
        var complete = () => run.Complete("again", Now);
        var fail = () => run.Fail("x", Now);
        complete.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiRunAlreadyFinished);
        fail.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.AiRunAlreadyFinished);
    }
}
