using System.Net;
using Infrastructure.Calendar;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class WebhookDeliveryTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => responder(request);
    }

    [Fact]
    public void Sign_is_a_deterministic_lowercase_hmac_sha256_hex()
    {
        var signature = WebhookDelivery.Sign("secret", "payload");
        signature.Should().Be(WebhookDelivery.Sign("secret", "payload"));
        signature.Should().HaveLength(64);
        signature.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public async Task Deliver_posts_the_payload_with_the_signature_header()
    {
        string? capturedSignature = null;
        string? capturedBody = null;
        var handler = new StubHandler(async request =>
        {
            capturedSignature = request.Headers.GetValues(WebhookDelivery.SignatureHeader).First();
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var delivery = new WebhookDelivery(new HttpClient(handler));

        var ok = await delivery.DeliverAsync("http://localhost/hook", "sec", "{\"a\":1}", CancellationToken.None);

        ok.Should().BeTrue();
        capturedBody.Should().Be("{\"a\":1}");
        capturedSignature.Should().Be($"sha256={WebhookDelivery.Sign("sec", "{\"a\":1}")}");
    }

    [Fact]
    public async Task Deliver_returns_false_on_a_non_success_or_thrown_error()
    {
        var fail = new WebhookDelivery(new HttpClient(
            new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)))));
        (await fail.DeliverAsync("http://localhost/h", "s", "{}", CancellationToken.None)).Should().BeFalse();

        var thrown = new WebhookDelivery(new HttpClient(
            new StubHandler(_ => throw new HttpRequestException("boom"))));
        (await thrown.DeliverAsync("http://localhost/h", "s", "{}", CancellationToken.None)).Should().BeFalse();
    }
}
