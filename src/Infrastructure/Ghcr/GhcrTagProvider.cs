using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Core;
using Core.Constants;
using Core.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ghcr;

public sealed class GhcrTagProvider(HttpClient http, IMemoryCache cache, IOptionsMonitor<CtwOptions> options)
    : IGhcrTagProvider
{
    private const string GhcrHost = "ghcr.io";
    private const string GhcrPrefix = "ghcr.io/";
    private const string DefaultRepository = "spotware/ctrader-console";
    private const string CacheKey = "ghcr-tags";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken ct)
    {
        var image = options.CurrentValue.DefaultDockerImage
            .Replace(GhcrPrefix, string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(image)) image = DefaultRepository;

        return await cache.GetOrCreateAsync(CacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            using var tokenReq = new HttpRequestMessage(HttpMethod.Get,
                $"https://{GhcrHost}/token?scope=repository:{image}:pull&service={GhcrHost}");
            var tokenResp = await http.SendAsync(tokenReq, ct);
            tokenResp.EnsureSuccessStatusCode();
            var tok = await tokenResp.Content.ReadFromJsonAsync<GhcrToken>(cancellationToken: ct);

            using var listReq = new HttpRequestMessage(HttpMethod.Get,
                $"https://{GhcrHost}/v2/{image}/tags/list");
            listReq.Headers.Authorization = new(AuthSchemes.Bearer, tok!.Token);
            var resp = await http.SendAsync(listReq, ct);
            resp.EnsureSuccessStatusCode();
            var list = await resp.Content.ReadFromJsonAsync<TagList>(cancellationToken: ct);
            return (IReadOnlyList<string>)(list?.Tags ?? new List<string>());
        }) ?? Array.Empty<string>();
    }

    private sealed record GhcrToken([property: JsonPropertyName("token")] string Token);
    private sealed record TagList([property: JsonPropertyName("tags")] List<string> Tags);
}
