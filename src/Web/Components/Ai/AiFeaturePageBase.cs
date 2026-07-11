using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Web.Json;

namespace Web.Components.Ai;

/// <summary>
/// Shared plumbing for the single-purpose AI feature pages (Review, Debate, Sentiment, …). Each page adds
/// its own inputs and calls one of the Post helpers; AI actions stay disabled until a key is configured
/// (reported by <c>AiFeatureNotice</c> via <see cref="OnAiStatus"/>).
/// </summary>
public abstract class AiFeaturePageBase : ComponentBase
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [Inject] protected HttpClient Http { get; set; } = default!;
    [Inject] protected ISnackbar Snack { get; set; } = default!;

    protected bool Loading;
    protected string Output = "";
    protected bool AiEnabled;

    // AI actions are disabled while a request is in flight or until a key is configured.
    protected bool Busy => Loading || !AiEnabled;

    protected void OnAiStatus(bool enabled)
    {
        AiEnabled = enabled;
        StateHasChanged();
    }

    protected async Task PostTextAsync(string url, object body)
    {
        Loading = true;
        Output = "";
        try
        {
            var response = await Http.PostAsJsonAsync(url, body);
            var result = await response.Content.ReadFromJsonAsync<AiTextResponse>();
            if (result is { success: true })
            {
                Output = result.text;
            }
            else
            {
                Snack.Add(result?.error ?? "AI request failed", Severity.Error);
            }
        }
        catch
        {
            Snack.Add("AI request failed", Severity.Error);
        }
        finally
        {
            Loading = false;
        }
    }

    protected async Task PostJsonAsync(string url, object body)
    {
        Loading = true;
        Output = "";
        try
        {
            var response = await Http.PostAsJsonAsync(url, body);
            var text = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
            if (!ok)
            {
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : "AI request failed";
                Snack.Add(error ?? "AI request failed", Severity.Error);
            }
            else
            {
                Output = JsonSerializer.Serialize(root, IndentedJson);
            }
        }
        catch
        {
            Snack.Add("AI request failed", Severity.Error);
        }
        finally
        {
            Loading = false;
        }
    }

    // Tolerate a gated-off/unavailable endpoint (e.g. Authoring disabled -> /api/cbots 404) so a selector
    // load never throws the page into the ErrorBoundary.
    protected async Task<dynamic[]> LoadOrEmptyAsync(string url)
    {
        try
        {
            return await Http.GetDynamicArrayAsync(url);
        }
        catch
        {
            return [];
        }
    }

    protected sealed record AiTextResponse(bool success, string text, string? error);
}
