using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

public class JournalHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@journal.local";
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
    public async Task Empty_journal_reports_no_history()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.GetAsync("/api/journal");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("summary").GetProperty("total").GetInt32().Should().Be(0);
        body.GetProperty("summary").GetProperty("insights").EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Journal_requires_authentication()
    {
        await using var app = CreateApp();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/journal");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }

    [Fact]
    public async Task Notes_round_trip_create_list_edit_delete()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var create = await client.PostAsJsonAsync("/api/journal/notes",
            new { Title = "Overtraded", Body = "Took 3 trades on a plan of 1", Symbol = "eurusd" });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        created.GetProperty("title").GetString().Should().Be("Overtraded");
        created.GetProperty("symbol").GetString().Should().Be("EURUSD");

        var list = await client.GetFromJsonAsync<JsonElement>("/api/journal/notes");
        list.EnumerateArray().Should().ContainSingle(n => n.GetProperty("id").GetGuid() == id);

        var edit = await client.PutAsJsonAsync($"/api/journal/notes/{id}",
            new { Title = "Overtraded — fixed", Body = "Now sticking to the plan", Symbol = (string?)null });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = await edit.Content.ReadFromJsonAsync<JsonElement>();
        edited.GetProperty("title").GetString().Should().Be("Overtraded — fixed");
        edited.GetProperty("symbol").ValueKind.Should().Be(JsonValueKind.Null);

        var delete = await client.DeleteAsync($"/api/journal/notes/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/journal/notes");
        after.EnumerateArray().Should().NotContain(n => n.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Creating_a_note_without_a_title_is_rejected()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PostAsJsonAsync("/api/journal/notes", new { Title = "", Body = "x", Symbol = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Be("domain.journal.note_title_required");
    }

    [Fact]
    public async Task Editing_a_missing_note_returns_not_found()
    {
        await using var app = CreateApp();
        var client = await LoginAsync(app);

        var response = await client.PutAsJsonAsync($"/api/journal/notes/{Guid.NewGuid()}",
            new { Title = "x", Body = (string?)null, Symbol = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Notes_require_authentication()
    {
        await using var app = CreateApp();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/api/journal/notes");
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Redirect, HttpStatusCode.Found);
    }
}
