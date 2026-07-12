using System.Net;
using System.Text;
using Infrastructure.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CmindCalendarClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(responder(request));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static CmindCalendarClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new HttpClient(new StubHandler(responder)) { BaseAddress = new Uri("http://localhost/api/calendar/") });

    [Fact]
    public async Task GetToken_returns_the_bearer_token()
    {
        var client = Client(_ => Json("""{ "token": "jwt-123", "expiresAt": "2024-01-01T00:00:00Z" }"""));

        (await client.GetTokenAsync("id", "secret")).Should().Be("jwt-123");
    }

    [Fact]
    public async Task GetToken_returns_null_on_failure()
    {
        var client = Client(_ => Json("""{ "title": "invalid_client" }""", HttpStatusCode.Unauthorized));

        (await client.GetTokenAsync("id", "wrong")).Should().BeNull();
    }

    [Fact]
    public async Task GetBlackout_parses_an_in_window_response()
    {
        var client = Client(_ => Json(
            """{ "inBlackout": true, "startsAt": "2024-02-13T13:00:00Z", "endsAt": "2024-02-13T14:00:00Z" }"""));

        var result = await client.GetBlackoutAsync("jwt", "EURUSD");

        result.InBlackout.Should().BeTrue();
        result.Stale.Should().BeFalse();
        result.EndsAt.Should().Be(new DateTimeOffset(2024, 2, 13, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetBlackout_fails_safe_to_in_blackout_on_error()
    {
        var client = Client(_ => Json("""{ "type": "unavailable" }""", HttpStatusCode.ServiceUnavailable));

        var result = await client.GetBlackoutAsync("jwt", "EURUSD");

        result.InBlackout.Should().BeTrue("a data gap must never green-light trading through a release");
        result.Stale.Should().BeTrue();
    }
}
