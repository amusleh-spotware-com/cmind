using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core;
using Core.CopyTrading;
using Core.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the copy-trading read endpoints + the feature gate over the real app + Postgres:
// profiles list, public marketplace, and the CopyTrading gate. (Coverage backfill — integration tier.)
public class CopyHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@copyhttp.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(bool copyEnabled = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:CopyTrading", copyEnabled ? "true" : "false");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Profiles_and_marketplace_read_endpoints_respond()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var profiles = await client.GetAsync("/api/copy/profiles");
        profiles.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profiles.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);

        (await client.GetAsync("/api/copy/marketplace")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Copy_endpoints_are_gated_off_when_the_feature_is_disabled()
    {
        await using var app = CreateApp(copyEnabled: false);
        var client = await LoginAsync(app);

        (await client.GetAsync("/api/copy/profiles")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a disabled feature returns 404 for its endpoints");
    }

    [Fact]
    public async Task Editing_a_destination_updates_exposed_settings_and_preserves_advanced_ones()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var create = await client.PostAsJsonAsync("/api/copy/profiles",
            new { Name = "edit-int", SourceAccountId = Guid.NewGuid() });
        create.EnsureSuccessStatusCode();
        var profileId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Add a destination that also sets an ADVANCED field the editor never shows (manage-only + jitter).
        var addDest = await client.PostAsJsonAsync($"/api/copy/profiles/{profileId}/destinations", new
        {
            DestinationAccountId = Guid.NewGuid(),
            Mode = MoneyManagementMode.LotMultiplier,
            Parameter = 1.0,
            SlippagePips = 0.0,
            MaxDelaySeconds = 0,
            Reverse = false,
            CopyStopLoss = true,
            CopyTakeProfit = true,
            Direction = CopyDirectionFilter.Both,
            MinLot = 0.0,
            MaxLot = 0.0,
            ForceMinLot = false,
            MaxDrawdownPercent = 0.0,
            DailyLossLimit = 0.0,
            ManageOnly = true,
            ExecutionJitterMaxMs = 250
        });
        addDest.EnsureSuccessStatusCode();
        var destinationId = (await addDest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Edit only the editor-exposed settings — the advanced fields are NOT sent.
        var put = await client.PutAsJsonAsync($"/api/copy/profiles/{profileId}/destinations/{destinationId}", new
        {
            Mode = MoneyManagementMode.ProportionalBalance,
            Parameter = 2.5,
            SlippagePips = 3.0,
            MaxDelaySeconds = 0,
            Reverse = true,
            CopyStopLoss = true,
            CopyTakeProfit = false,
            Direction = CopyDirectionFilter.LongOnly,
            MinLot = 0.0,
            MaxLot = 0.0,
            ForceMinLot = false,
            MaxDrawdownPercent = 0.0,
            DailyLossLimit = 0.0
        });
        put.EnsureSuccessStatusCode();

        var detail = await (await client.GetAsync($"/api/copy/profiles/{profileId}")).Content.ReadFromJsonAsync<JsonElement>();
        var destination = detail.GetProperty("destinations").EnumerateArray().Single();
        destination.GetProperty("mode").GetString().Should().Be("ProportionalBalance", "the exposed mode was edited");
        destination.GetProperty("riskParameter").GetDouble().Should().Be(2.5);
        destination.GetProperty("slippagePips").GetDouble().Should().Be(3.0);
        destination.GetProperty("reverse").GetBoolean().Should().BeTrue();
        destination.GetProperty("copyTakeProfit").GetBoolean().Should().BeFalse();
        destination.GetProperty("direction").GetString().Should().Be("LongOnly");
        destination.GetProperty("manageOnly").GetBoolean().Should()
            .BeTrue("an advanced field not on the editor must be preserved across an edit");
        destination.GetProperty("executionJitterMaxMs").GetInt32().Should()
            .Be(250, "advanced fields the editor doesn't expose are preserved, not reset");
    }

    [Fact]
    public async Task A_started_profile_reads_as_Starting_while_its_host_is_warming_up()
    {
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:CopyTrading", "true");
            // Force the hosting registry to report the profile as still warming, so the list endpoint's
            // Running→"Starting" derivation is asserted deterministically (a real host needs a live broker).
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<ICopyHostingStatus>();
                s.AddSingleton<ICopyHostingStatus>(new AlwaysWarmingHostingStatus());
            });
        });
        var client = await LoginAsync(app);

        var create = await client.PostAsJsonAsync("/api/copy/profiles",
            new { Name = "warming-int", SourceAccountId = Guid.NewGuid() });
        create.EnsureSuccessStatusCode();
        var profileId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await client.PostAsync($"/api/copy/profiles/{profileId}/start", null)).EnsureSuccessStatusCode();

        var profiles = await (await client.GetAsync("/api/copy/profiles")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = profiles.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == profileId);
        mine.GetProperty("status").GetString().Should().Be("Starting",
            "a Running profile whose host is still warming shows Starting, not a green Running");
    }

    // Reports every profile as warming, so the endpoint's Running→"Starting" mapping is deterministic.
    private sealed class AlwaysWarmingHostingStatus : ICopyHostingStatus
    {
        public void MarkWarming(CopyProfileId profileId) { }
        public void MarkReady(CopyProfileId profileId) { }
        public void Clear(CopyProfileId profileId) { }
        public CopyHostingPhase PhaseOf(CopyProfileId profileId) => CopyHostingPhase.Warming;
    }
}
