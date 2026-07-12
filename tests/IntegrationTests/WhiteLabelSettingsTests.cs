using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Exercises the white-label owner settings over the real app + Postgres: the snapshot, that an owner
/// override beats configuration and takes effect live (through the decorated options monitor, observable in
/// the rendered page), secret masking, revert/reset, validation, feature-flag delegation and RBAC.
/// </summary>
public class WhiteLabelSettingsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@wl.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(IReadOnlyDictionary<string, string?>? settings = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
                b.UseSetting(key, value);
        });

    private static HttpClient NewClient(WebApplicationFactory<Program> app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task LoginOwnerAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> SnapshotAsync(HttpClient client)
        => await (await client.GetAsync("/api/whitelabel/")).Content.ReadFromJsonAsync<JsonElement>();

    private static JsonElement Row(JsonElement snapshot, string key)
        => snapshot.EnumerateArray().Single(e => e.GetProperty("key").GetString() == key);

    [Fact]
    public async Task Snapshot_reports_config_and_default_sources()
    {
        await using var app = CreateApp(new Dictionary<string, string?>
        {
            ["App:Branding:ProductName"] = "AcmeFX"
        });
        var client = NewClient(app);
        await LoginOwnerAsync(client);

        var snapshot = await SnapshotAsync(client);
        var productName = Row(snapshot, "branding.productName");
        productName.GetProperty("value").GetString().Should().Be("AcmeFX");
        productName.GetProperty("source").GetString().Should().Be("Config");

        Row(snapshot, "branding.requireMfa").GetProperty("source").GetString().Should().Be("Default");
    }

    [Fact]
    public async Task Owner_override_beats_config_and_takes_effect_live()
    {
        await using var app = CreateApp(new Dictionary<string, string?>
        {
            ["App:Branding:ProductName"] = "AcmeFX"
        });
        var client = NewClient(app);
        await LoginOwnerAsync(client);

        var put = await client.PutAsJsonAsync("/api/whitelabel/branding.productName", new { value = "OwnerBrand" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Live effect: the rendered page head reflects the override without a redeploy.
        var html = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        html.Should().Contain("<title>OwnerBrand</title>");

        Row(await SnapshotAsync(client), "branding.productName").GetProperty("source").GetString().Should().Be("Owner");

        var delete = await client.DeleteAsync("/api/whitelabel/branding.productName");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reverted = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        reverted.Should().Contain("<title>AcmeFX</title>");
    }

    [Fact]
    public async Task Secret_is_masked_but_persists()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        await LoginOwnerAsync(client);

        var put = await client.PutAsJsonAsync("/api/whitelabel/email.password", new { value = "sup3r-secret" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var row = Row(await SnapshotAsync(client), "email.password");
        row.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        row.GetProperty("hasValue").GetBoolean().Should().BeTrue();
        row.GetProperty("source").GetString().Should().Be("Owner");
    }

    [Fact]
    public async Task Reset_clears_all_overrides()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        await LoginOwnerAsync(client);

        await client.PutAsJsonAsync("/api/whitelabel/branding.requireMfa", new { value = "True" });
        await client.PutAsJsonAsync("/api/whitelabel/accounts.allowedBrokers", new { value = "Pepperstone" });

        var reset = await client.PostAsync("/api/whitelabel/reset", null);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var snapshot = await SnapshotAsync(client);
        Row(snapshot, "branding.requireMfa").GetProperty("hasOverride").GetBoolean().Should().BeFalse();
        Row(snapshot, "accounts.allowedBrokers").GetProperty("hasOverride").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Invalid_value_and_unknown_key_are_rejected()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        await LoginOwnerAsync(client);

        (await client.PutAsJsonAsync("/api/whitelabel/branding.nodesUi", new { value = "Bogus" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/whitelabel/branding.primaryColor", new { value = "not-a-color" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/whitelabel/does.not.exist", new { value = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Feature_flag_override_delegates_to_feature_gate()
    {
        await using var app = CreateApp();
        var client = NewClient(app);
        await LoginOwnerAsync(client);

        var put = await client.PutAsJsonAsync("/api/whitelabel/features.copyTrading", new { value = "false" });
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        Row(await SnapshotAsync(client), "features.copyTrading").GetProperty("source").GetString().Should().Be("Owner");

        // Cross-check the existing feature endpoint sees the same override (single store, no divergence).
        var features = await (await client.GetAsync("/api/features/")).Content.ReadFromJsonAsync<JsonElement>();
        features.EnumerateArray().Single(e => e.GetProperty("flag").GetString() == "CopyTrading")
            .GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Non_owner_cannot_reach_the_api()
    {
        await using var app = CreateApp();
        var client = NewClient(app);

        var resp = await client.GetAsync("/api/whitelabel/");
        resp.IsSuccessStatusCode.Should().BeFalse();
        ((int)resp.StatusCode).Should().BeOneOf(401, 403, 302);
    }
}
