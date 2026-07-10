using Core.Options;
using Microsoft.Extensions.Options;
using MudBlazor;
using Web.Components;

namespace Web.Branding;

public interface IBrandingThemeProvider
{
    MudTheme Theme { get; }
    BrandingOptions Branding { get; }
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
        });
    }

    public MudTheme Theme => _theme;
    public BrandingOptions Branding => _branding;

    public void Dispose() => _subscription?.Dispose();
}
