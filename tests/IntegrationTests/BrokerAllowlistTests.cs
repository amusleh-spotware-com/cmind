using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core;
using Core.Accounts;
using Core.Domain;
using Core.Options;
using CTraderOpenApi.Auth;
using CTraderOpenApi.Client;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests;

public class BrokerAllowlistTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Owner = "owner@brokers.local";
    private const string Password = "Owner_Pass_123!";

    private WebApplicationFactory<Program> CreateApp(
        BrokerVerificationResult? verifierResult, params string[] allowedBrokers) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", Owner);
            b.UseSetting("App:OwnerPassword", Password);
            for (var i = 0; i < allowedBrokers.Length; i++)
                b.UseSetting($"App:Accounts:AllowedBrokers:{i}", allowedBrokers[i]);
            if (verifierResult is not null)
                b.ConfigureTestServices(s =>
                {
                    s.RemoveAll<IBrokerVerifier>();
                    s.AddScoped<IBrokerVerifier>(_ => new FakeBrokerVerifier(verifierResult));
                });
        });

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = Owner, Password, RememberMe = true });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Cookie", login.Headers.GetValues("Set-Cookie").First().Split(';')[0]);
        return client;
    }

    private static async Task<Guid> CreateCidAsync(HttpClient client)
    {
        var username = $"ct-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/ctids/", new { Username = username, Password = "cid_pw_123" });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var cids = await client.GetFromJsonAsync<JsonElement>("/api/ctids/");
        return cids.EnumerateArray()
            .First(c => c.GetProperty("username").GetString() == username)
            .GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Unrestricted_deployment_accepts_any_broker_without_verification()
    {
        await using var app = CreateApp(verifierResult: null); // no allowlist, verifier never called
        var client = await LoginAsync(app);
        var cid = await CreateCidAsync(client);

        var add = await client.PostAsJsonAsync($"/api/ctids/{cid}/accounts",
            new { AccountNumber = 111L, Broker = "Any Broker", IsLive = false });

        add.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await client.GetFromJsonAsync<JsonElement>($"/api/ctids/{cid}/accounts");
        accounts.EnumerateArray().Single().GetProperty("broker").GetString().Should().Be("Any Broker");
    }

    [Fact]
    public async Task Restricted_deployment_persists_the_verified_broker_over_the_typed_one()
    {
        await using var app = CreateApp(BrokerVerificationResult.Verified(new BrokerName("Pepperstone")), "Pepperstone");
        var client = await LoginAsync(app);
        var cid = await CreateCidAsync(client);

        // The user typed a bogus broker; verification wins and it is allowed.
        var add = await client.PostAsJsonAsync($"/api/ctids/{cid}/accounts",
            new { AccountNumber = 222L, Broker = "User Typed Nonsense", IsLive = false });

        add.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await client.GetFromJsonAsync<JsonElement>($"/api/ctids/{cid}/accounts");
        accounts.EnumerateArray().Single().GetProperty("broker").GetString().Should().Be("Pepperstone");
    }

    [Fact]
    public async Task Restricted_deployment_rejects_a_verified_disallowed_broker()
    {
        await using var app = CreateApp(BrokerVerificationResult.Verified(new BrokerName("Other Broker")), "Pepperstone");
        var client = await LoginAsync(app);
        var cid = await CreateCidAsync(client);

        var add = await client.PostAsJsonAsync($"/api/ctids/{cid}/accounts",
            new { AccountNumber = 333L, Broker = "Other Broker", IsLive = false });

        add.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var accounts = await client.GetFromJsonAsync<JsonElement>($"/api/ctids/{cid}/accounts");
        accounts.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task Restricted_deployment_surfaces_a_verification_failure()
    {
        await using var app = CreateApp(BrokerVerificationResult.Failed(BrokerVerificationError.LoginFailed), "Pepperstone");
        var client = await LoginAsync(app);
        var cid = await CreateCidAsync(client);

        var add = await client.PostAsJsonAsync($"/api/ctids/{cid}/accounts",
            new { AccountNumber = 444L, Broker = "Pepperstone", IsLive = false });

        add.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task OpenApi_linker_skips_disallowed_accounts_and_links_allowed_ones()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"link-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());
        var application = OpenApiApplication.Create(user.Id, $"app-{Guid.NewGuid():N}",
            new OpenApiClientId("client-1"), [1, 2, 3], new OpenApiRedirectUri("https://app.test/callback"));
        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.OpenApiApplications.Add(application);
            await write.SaveChangesAsync();
        }

        var options = new StaticOptionsMonitor<AppOptions>(new AppOptions
        {
            Accounts = new AccountsOptions { AllowedBrokers = ["Pepperstone"] }
        });
        var ctid = Math.Abs(Guid.NewGuid().GetHashCode()) + 1L;
        var grant = new OpenApiGrant(ctid,
        [
            new OpenApiAccountInfo(10, 5001, true, "Pepperstone"),
            new OpenApiAccountInfo(11, 5002, true, "Other Broker")
        ]);
        var tokens = new OpenApiTokenResponse("access", "refresh", 3600, "bearer");

        Web.OpenApi.OpenApiLinkResult result;
        await using (var link = CreateContext())
        {
            var linker = new Web.OpenApi.OpenApiAccountLinker(
                link, new PassthroughSecretProtector(), TimeProvider.System, options);
            result = await linker.LinkAsync(user.Id, application, grant, tokens, CancellationToken.None);
        }

        result.Linked.Should().Be(1);
        result.SkippedBrokers.Should().ContainSingle().Which.Should().Be("Other Broker");

        await using var read = CreateContext();
        var accounts = await read.TradingAccounts.Where(t => t.CTid.UserId == user.Id).ToListAsync();
        accounts.Should().ContainSingle();
        accounts[0].Broker.Should().Be("Pepperstone");
    }

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private sealed class FakeBrokerVerifier(BrokerVerificationResult result) : IBrokerVerifier
    {
        public Task<BrokerVerificationResult> VerifyAsync(BrokerProbeRequest request, CancellationToken ct) =>
            Task.FromResult(result);
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
