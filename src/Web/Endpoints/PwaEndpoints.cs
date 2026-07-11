using System.Text.Json.Serialization;
using Core.Constants;
using Core.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Web.Endpoints;

/// <summary>
/// Serves the PWA web app manifest dynamically from white-label <see cref="BrandingOptions"/> so a reseller's
/// product name, colours and icons make the installed app their own. Anonymous — the manifest must be
/// reachable before sign-in for the browser's install prompt.
/// </summary>
public static class PwaEndpoints
{
    public static IEndpointRouteBuilder MapPwaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(PwaRoutes.Manifest, (IOptionsMonitor<AppOptions> options) =>
            {
                var branding = options.CurrentValue.Branding;
                var manifest = new WebManifest(
                    Name: branding.ProductName,
                    ShortName: Shorten(branding.ProductName),
                    Description: branding.Description,
                    StartUrl: PwaRoutes.StartUrl,
                    Scope: PwaRoutes.StartUrl,
                    BackgroundColor: branding.BackgroundColor,
                    ThemeColor: branding.AppBarColor,
                    Icons:
                    [
                        new ManifestIcon(PwaRoutes.Icon192, "192x192", IconPng, IconPurposeAny),
                        new ManifestIcon(PwaRoutes.Icon512, "512x512", IconPng, IconPurposeAny),
                        new ManifestIcon(PwaRoutes.Icon512Maskable, "512x512", IconPng, IconPurposeMaskable),
                    ]);
                return Results.Json(manifest, contentType: PwaRoutes.ManifestContentType);
            })
            .AllowAnonymous();
        return app;
    }

    private const string IconPng = "image/png";
    private const string IconPurposeAny = "any";
    private const string IconPurposeMaskable = "maskable";
    private const int ShortNameMaxLength = 12;

    private static string Shorten(string name) =>
        name.Length <= ShortNameMaxLength ? name : name[..ShortNameMaxLength];

    private sealed record WebManifest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("short_name")] string ShortName,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("start_url")] string StartUrl,
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("background_color")] string BackgroundColor,
        [property: JsonPropertyName("theme_color")] string ThemeColor,
        [property: JsonPropertyName("icons")] IReadOnlyList<ManifestIcon> Icons)
    {
        [JsonPropertyName("id")] public string Id => StartUrl;
        [JsonPropertyName("display")] public string Display { get; init; } = "standalone";
        [JsonPropertyName("orientation")] public string Orientation { get; init; } = "portrait-primary";
        [JsonPropertyName("categories")] public IReadOnlyList<string> Categories { get; init; } = ["finance", "productivity"];
    }

    private sealed record ManifestIcon(
        [property: JsonPropertyName("src")] string Src,
        [property: JsonPropertyName("sizes")] string Sizes,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("purpose")] string Purpose);
}
