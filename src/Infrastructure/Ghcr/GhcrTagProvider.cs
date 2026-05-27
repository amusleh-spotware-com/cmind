using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Ghcr;

public sealed class GhcrTagProvider : IGhcrTagProvider
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly string _image;

    public GhcrTagProvider(HttpClient http, IMemoryCache cache, IConfiguration cfg)
    {
        _http = http;
        _cache = cache;
        _image = cfg["Ctw:DefaultDockerImage"]?.Replace("ghcr.io/", "", StringComparison.Ordinal)
                 ?? "spotware/ctrader-console";
    }

    public async Task<IReadOnlyList<string>> GetTagsAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync("ghcr-tags", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var token = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(":"));
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://ghcr.io/token?scope=repository:{_image}:pull&service=ghcr.io");
            var tokenResp = await _http.SendAsync(req, ct);
            tokenResp.EnsureSuccessStatusCode();
            var tokDoc = await tokenResp.Content.ReadFromJsonAsync<GhcrToken>(cancellationToken: ct);
            using var listReq = new HttpRequestMessage(HttpMethod.Get,
                $"https://ghcr.io/v2/{_image}/tags/list");
            listReq.Headers.Authorization = new("Bearer", tokDoc!.Token);
            var resp = await _http.SendAsync(listReq, ct);
            resp.EnsureSuccessStatusCode();
            var list = await resp.Content.ReadFromJsonAsync<TagList>(cancellationToken: ct);
            return (IReadOnlyList<string>)(list?.Tags ?? new List<string>());
        }) ?? Array.Empty<string>();
    }

    private sealed record GhcrToken([property: JsonPropertyName("token")] string Token);
    private sealed record TagList([property: JsonPropertyName("tags")] List<string> Tags);
}
