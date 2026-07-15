using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// POST /api/instances/ backs the Run and Backtest dialogs. A parameter set is optional, so the request
// carries a nullable ParamSetId — a null value must deserialize and be handled, NOT blow up the JSON binder
// with a 500 (regression: StartRequest.ParamSetId was a non-nullable Guid and a null crashed the endpoint).
public class InstanceStartHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@inststart.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Execution", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Theory]
    [InlineData("Run")]
    [InlineData("Backtest")]
    public async Task Start_with_a_null_param_set_is_a_clean_bad_request_not_a_500(string type)
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        // Unknown cBot/account so the request short-circuits with a BadRequest — the point is that a null
        // ParamSetId deserializes cleanly and never yields a 500 from the JSON binder.
        var res = await client.PostAsJsonAsync("/api/instances/", new
        {
            CBotId = Guid.NewGuid(),
            TradingAccountId = Guid.NewGuid(),
            Symbol = "EURUSD",
            Timeframe = "h1",
            ParamSetId = (Guid?)null,
            DockerImageTag = "latest",
            Type = type,
            BacktestSettingsJson = (string?)null
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a null parameter set must be accepted and handled, never crash the JSON binder with a 500");
    }
}
