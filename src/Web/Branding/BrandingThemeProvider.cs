using Core.Options;
using Microsoft.Extensions.Options;
using MudBlazor;
using Web.Components;

namespace Web.Branding;

public interface IBrandingThemeProvider
{
    MudTheme Theme { get; }
    BrandingOptions Branding { get; }

    // Raised when the branding overlay changes at runtime (owner edits an override in Settings →
    // Deployment). A single Blazor circuit subscribes so an open session re-renders the app-bar
    // product name/logo without a full reload.
    event Action? Changed;
}

public sealed class BrandingThemeProvider : IBrandingThemeProvider, IDisposable
{
    private readonly IDisposable? _subscription;
    private MudTheme _theme;
    private BrandingOptions _branding;

    public BrandingThemeProvider(IOptionsMonitor<AppOptions> options)
    {
        _branding = options.CurrentValue.Branding;
        _theme = Web.Components.Theme.Build(_branding);
        _subscription = options.OnChange(updated =>
        {
            _branding = updated.Branding;
            _theme = Web.Components.Theme.Build(updated.Branding);
            Changed?.Invoke();
        });
    }

    public MudTheme Theme => _theme;
    public BrandingOptions Branding => _branding;

    public event Action? Changed;

    public void Dispose() => _subscription?.Dispose();
}
