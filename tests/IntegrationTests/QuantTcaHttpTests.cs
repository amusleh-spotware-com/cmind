using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class QuantTcaHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@tca.local";
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
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    [Fact]
    public async Task Computes_slippage_for_a_buy()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/tca", new
        {
            ArrivalPrice = 1.1000,
            Side = "Buy",
            Fills = new[] { new { Price = 1.1010, Quantity = 100.0 }, new { Price = 1.1020, Quantity = 100.0 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("averageFillPrice").GetDouble().Should().BeApproximately(1.1015, 1e-9);
        body.GetProperty("slippageBps").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Execution_schedule_sums_to_total()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/quant/execution-schedule", new
        {
            TotalQuantity = 100.0,
            Slices = 5,
            RiskAversion = 2.0,
            Volatility = 0.02,
            TemporaryImpact = 0.1
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var slices = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slices").EnumerateArray()
            .Select(s => s.GetProperty("quantity").GetDouble()).ToArray();
        slices.Should().HaveCount(5);
        slices.Sum().Should().BeApproximately(100.0, 1e-6);
    }

    [Fact]
    public async Task Rejects_missing_fills()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var response = await client.PostAsJsonAsync("/api/quant/tca", new { ArrivalPrice = 1.1000, Side = "Buy" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
