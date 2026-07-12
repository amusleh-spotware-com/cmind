using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Ai;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IntegrationTests;

// WS-4 launch gate (public-launch-readiness.md): multi-tenant isolation over the real HTTP stack.
// A signed-in user must never see or mutate another user's resources. Exercised against the user-scoped
// AI provider endpoints (/api/ai/my-providers, scoped by ICurrentUser): a credential created by one user
// is invisible to another and cannot be deleted by them. This is the template for every user-owned
// resource — extend it as new user-scoped endpoints are added.
public class TenantIsolationTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@tenant.local";
    private const string OwnerPassword = "Owner_Pass_123!";
    private const string UserPassword = "Str0ngPass!";
    private const string ProvisionSecret = "provision-secret-abcdefghijklmnop";

    private WebApplicationFactory<Program> CreateApp() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", OwnerPassword);
            b.UseSetting("App:Features:Ai", "true");
            b.UseSetting("App:Features:Registration", "true");
            b.UseSetting("App:Registration:Enabled", "true");
            b.UseSetting("App:Registration:Api:Enabled", "true");
            b.UseSetting("App:Registration:Api:Secret", ProvisionSecret);
        });

    private static async Task<HttpClient> ProvisionAndLoginAsync(WebApplicationFactory<Program> app, string email)
    {
        var admin = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var provision = new HttpRequestMessage(HttpMethod.Post, "/api/provision")
        {
            Content = JsonContent.Create(new { Email = email, Password = UserPassword }),
        };
        provision.Headers.Add("X-Provision-Secret", ProvisionSecret);
        (await admin.SendAsync(provision)).StatusCode.Should().Be(HttpStatusCode.OK);

        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = UserPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    private static object ProviderBody() => new
    {
        Id = (Guid?)null,
        Kind = AiProviderKind.Anthropic,
        BaseUrl = "https://api.anthropic.com/",
        Model = "claude-opus-4-8",
        ApiKey = "secret-key",
        MaxTokens = 8000,
        Capabilities = (object?)null,
        Activate = true,
    };

    private static IEnumerable<Guid> Ids(JsonElement list) =>
        list.EnumerateArray().Select(e => e.GetProperty("id").GetGuid());

    [Fact]
    public async Task A_users_ai_credential_is_invisible_and_immutable_to_another_user()
    {
        await using var app = CreateApp();
        var alice = await ProvisionAndLoginAsync(app, $"alice-{Guid.NewGuid():N}@tenant.local");
        var bob = await ProvisionAndLoginAsync(app, $"bob-{Guid.NewGuid():N}@tenant.local");

        var created = await alice.PutAsJsonAsync("/api/ai/my-providers", ProviderBody());
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var aliceId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Bob cannot see Alice's credential.
        var bobList = await (await bob.GetAsync("/api/ai/my-providers")).Content.ReadFromJsonAsync<JsonElement>();
        Ids(bobList).Should().NotContain(aliceId, "another user's AI credential must not be listed");

        // Bob's delete of Alice's id is scoped to his own user — a no-op, not a cross-tenant delete.
        (await bob.DeleteAsync($"/api/ai/my-providers/{aliceId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Alice's credential survives Bob's attempt.
        var aliceList = await (await alice.GetAsync("/api/ai/my-providers")).Content.ReadFromJsonAsync<JsonElement>();
        Ids(aliceList).Should().Contain(aliceId, "a user's own credential must not be deletable by another user");
    }
}
