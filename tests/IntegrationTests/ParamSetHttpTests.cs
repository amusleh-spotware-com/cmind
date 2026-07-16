using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// HTTP round-trip for the param-set endpoints over the real app + Postgres: create, list (filtered by
// cbot), fetch by id, and the create-time validation. (Coverage backfill — integration tier.)
public class ParamSetHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@paramset.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            b.UseSetting("App:Features:Authoring", "true");
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<Guid> UploadCBotAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "bot.algo" },
            { new StringContent($"Host-{Guid.NewGuid():N}"), "name" },
        };
        var upload = await client.PostAsync("/api/cbots/upload", form);
        upload.EnsureSuccessStatusCode();
        return (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_then_list_and_fetch_a_param_set()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var cbotId = await UploadCBotAsync(client);

        var create = await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotId, Name = "aggressive", JsonContent = "{\"risk\":2}" });
        create.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await client.GetAsync($"/api/paramsets/?cbotId={cbotId}")).Content.ReadFromJsonAsync<JsonElement>();
        var mine = list.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "aggressive");
        var id = mine.GetProperty("id").GetGuid();

        var fetched = await client.GetAsync($"/api/paramsets/{id}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_validates_name_and_content()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = Guid.NewGuid(), Name = "", JsonContent = "{}" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = Guid.NewGuid(), Name = "x", JsonContent = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("[1,2,3]")]                          // array root
    [InlineData("{\"Parameters\":{\"Period\":14}}")] // nested object value
    [InlineData("{\"tags\":[1,2]}")]                  // array value
    [InlineData("42")]                                // primitive root
    public async Task Create_rejects_a_non_flat_param_object(string json)
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var cbotId = await UploadCBotAsync(client);

        var res = await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotId, Name = "bad", JsonContent = json });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a parameter set must be a flat name→scalar-value object");
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_for_the_same_cbot_but_allows_it_for_another()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var cbotA = await UploadCBotAsync(client);
        var cbotB = await UploadCBotAsync(client);

        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotA, Name = "default", JsonContent = "{}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Same name, same cBot → rejected (names are unique per cBot).
        var dup = await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotA, Name = "default", JsonContent = "{\"x\":1}" });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A leading/trailing-space variant is the same name after trim → still rejected.
        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotA, Name = "  default  ", JsonContent = "{}" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A different-case variant is the same name → still rejected (names are case-insensitive per cBot).
        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotA, Name = "DEFAULT", JsonContent = "{}" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Same name on a DIFFERENT cBot is fine.
        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotB, Name = "default", JsonContent = "{}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK, "names are unique per cBot, not globally");
    }

    [Fact]
    public async Task Update_rejects_renaming_onto_another_sets_name_for_the_same_cbot()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);
        var cbotId = await UploadCBotAsync(client);

        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotId, Name = "one", JsonContent = "{}" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/paramsets/",
            new { CBotId = cbotId, Name = "two", JsonContent = "{}" })).EnsureSuccessStatusCode();

        var list = await (await client.GetAsync($"/api/paramsets/?cbotId={cbotId}")).Content.ReadFromJsonAsync<JsonElement>();
        var twoId = list.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "two").GetProperty("id").GetGuid();

        // Rename "two" → "one" collides with the existing set.
        var rename = await client.PutAsJsonAsync($"/api/paramsets/{twoId}",
            new { CBotId = cbotId, Name = "one", JsonContent = "{}" });
        rename.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Renaming a set to its OWN name (no-op) is allowed.
        (await client.PutAsJsonAsync($"/api/paramsets/{twoId}",
            new { CBotId = cbotId, Name = "two", JsonContent = "{\"y\":2}" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Fetching_a_missing_param_set_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.GetAsync($"/api/paramsets/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
