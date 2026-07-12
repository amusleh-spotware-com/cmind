using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.OpenApi;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class OpenApiAppResolverTests
{
    private static OpenApiApplication Shared() => OpenApiApplication.CreateShared(
        UserId.New(), "shared", new OpenApiClientId("cid"), [1], new OpenApiRedirectUri("https://app.test/cb"));

    private static OpenApiApplication Personal() => OpenApiApplication.Create(
        UserId.New(), "mine", new OpenApiClientId("cid"), [1], new OpenApiRedirectUri("https://app.test/cb"));

    [Fact]
    public async Task Resolves_shared_for_any_user_when_shared_exists()
    {
        var shared = Shared();
        var resolver = new OpenApiAppResolver(new FakeRepo(shared, Personal()));

        (await resolver.IsSharedModeAsync(CancellationToken.None)).Should().BeTrue();
        (await resolver.ResolveForUserAsync(UserId.New(), CancellationToken.None)).Should().BeSameAs(shared);
    }

    [Fact]
    public async Task Resolves_personal_app_when_no_shared_exists()
    {
        var personal = Personal();
        var resolver = new OpenApiAppResolver(new FakeRepo(null, personal));

        (await resolver.IsSharedModeAsync(CancellationToken.None)).Should().BeFalse();
        (await resolver.ResolveForUserAsync(UserId.New(), CancellationToken.None)).Should().BeSameAs(personal);
    }

    private sealed class FakeRepo(OpenApiApplication? shared, OpenApiApplication? personal) : IOpenApiApplicationRepository
    {
        public Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, UserId owner, CancellationToken ct)
            => Task.FromResult<OpenApiApplication?>(null);

        public Task<OpenApiApplication?> GetByIdAsync(OpenApiApplicationId id, CancellationToken ct)
            => Task.FromResult<OpenApiApplication?>(null);

        public Task<OpenApiApplication?> GetByUserAsync(UserId owner, CancellationToken ct)
            => Task.FromResult(personal);

        public Task<OpenApiApplication?> GetSharedAsync(CancellationToken ct)
            => Task.FromResult(shared);

        public Task AddAsync(OpenApiApplication application, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(OpenApiApplication application, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
