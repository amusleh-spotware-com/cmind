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
            { new StringContent("Host"), "name" },
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

    [Fact]
    public async Task Fetching_a_missing_param_set_is_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        (await client.GetAsync($"/api/paramsets/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
