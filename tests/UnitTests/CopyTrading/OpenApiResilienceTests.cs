using CTraderOpenApi;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OpenApiResilienceTests
{
    [Fact]
    public void Backoff_grows_exponentially_then_caps_and_resets()
    {
        var policy = new BackoffPolicy(
            TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(1000), 2.0, () => 1.0);

        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(100));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(200));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(400));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(800));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(1000));
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(1000));

        policy.Reset();
        policy.NextDelay().Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Backoff_jitter_scales_between_half_and_full()
    {
        var low = new BackoffPolicy(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), 2.0, () => 0.0);
        var high = new BackoffPolicy(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1), 2.0, () => 1.0);

        low.NextDelay().Should().Be(TimeSpan.FromMilliseconds(50));
        high.NextDelay().Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Classify_marks_client_auth_failure_fatal()
        => OpenApiError.Classify("CH_CLIENT_AUTH_FAILURE", null, null).Kind
            .Should().Be(OpenApiErrorKind.Fatal);

    [Fact]
    public void Classify_marks_expired_token_token_invalid()
        => OpenApiError.Classify("OA_AUTH_TOKEN_EXPIRED", null, null).Kind
            .Should().Be(OpenApiErrorKind.TokenInvalid);

    [Fact]
    public void Classify_marks_maintenance_when_end_timestamp_present()
    {
        var endsAt = TestClock.Now.AddHours(2);

        var error = OpenApiError.Classify("ANY", null, endsAt);

        error.Kind.Should().Be(OpenApiErrorKind.Maintenance);
        error.MaintenanceEndsAt.Should().Be(endsAt);
    }

    [Fact]
    public void Classify_defaults_unknown_code_to_recoverable()
        => OpenApiError.Classify("SOME_TRANSIENT_GLITCH", null, null).Kind
            .Should().Be(OpenApiErrorKind.Recoverable);
}
