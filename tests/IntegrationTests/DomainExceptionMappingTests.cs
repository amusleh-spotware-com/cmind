using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// R4 gate (C-01/C-02/C-03): domain/persistence failures on /api routes must map to the correct HTTP
// status via Web.Security.DomainExceptionHandler — never a raw 500 with a stack trace. A broken
// invariant (DomainException) is the client's fault → 400; a unique-constraint violation → 409.
public class DomainExceptionMappingTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@domainmap.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Duplicate_cid_username_returns_409_not_500()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/ctids/", new { Username = "dupuser", Password = "p" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await client.PostAsJsonAsync("/api/ctids/", new { Username = "dupuser", Password = "p" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, "a duplicate cID username is a conflict, not a 500");
    }

    [Fact]
    public async Task Missing_broker_on_account_returns_400_not_500()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/ctids/", new { Username = "brokertest", Password = "p" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await (await client.GetAsync("/api/ctids/")).Content.ReadFromJsonAsync<JsonElement>();
        var id = list.EnumerateArray().Single(c => c.GetProperty("username").GetString() == "brokertest")
            .GetProperty("id").GetGuid();

        // Empty broker breaks the TradingAccount invariant → DomainException → 400 (not a raw 500).
        var res = await client.PostAsJsonAsync($"/api/ctids/{id}/accounts",
            new { AccountNumber = 5009999L, Broker = "", IsLive = false, Label = "x" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a broken domain invariant is a 400, not a 500");
    }

    [Fact]
    public async Task Duplicate_account_number_returns_409_not_500()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/ctids/", new { Username = "dupacct", Password = "p" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await (await client.GetAsync("/api/ctids/")).Content.ReadFromJsonAsync<JsonElement>();
        var id = list.EnumerateArray().Single(c => c.GetProperty("username").GetString() == "dupacct")
            .GetProperty("id").GetGuid();

        var account = new { AccountNumber = 5007777L, Broker = "Pepperstone", IsLive = false, Label = "a" };
        (await client.PostAsJsonAsync($"/api/ctids/{id}/accounts", account))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var dup = await client.PostAsJsonAsync($"/api/ctids/{id}/accounts", account);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict, "a duplicate account number is a conflict, not a 500");
    }
}
