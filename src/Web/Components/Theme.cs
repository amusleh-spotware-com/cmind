using Core.Branding;
using Core.Options;
using MudBlazor;

namespace Web.Components;

public static class Theme
{
    /// <summary>
    /// Builds the MudBlazor theme from white-label <see cref="BrandingOptions"/>. Only the branded palette
    /// entries come from configuration (validated through <see cref="HexColor"/>); typography, layout and
    /// neutral surface tones stay fixed so the product keeps a coherent look across resellers.
    /// </summary>
    public static MudTheme Build(BrandingOptions branding) => new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = new HexColor(branding.PrimaryColor).Value,
            PrimaryContrastText = "#FFFFFF",
            Secondary = new HexColor(branding.SecondaryColor).Value,
            Tertiary = "#3A3A3A",
            Background = new HexColor(branding.BackgroundColor).Value,
            BackgroundGray = "#141414",
            Surface = new HexColor(branding.SurfaceColor).Value,
            AppbarBackground = new HexColor(branding.AppBarColor).Value,
            AppbarText = "#E6E6E6",
            DrawerBackground = "#1F1F1F",
            DrawerText = "#E6E6E6",
            DrawerIcon = new HexColor(branding.PrimaryColor).Value,
            TextPrimary = "#E6E6E6",
            TextSecondary = "#A0A0A0",
            TextDisabled = "#6B6B6B",
            ActionDefault = "#A0A0A0",
            ActionDisabled = "#5A5A5A",
            ActionDisabledBackground = "#2A2A2A",
            LinesDefault = "#333333",
            LinesInputs = "#3A3A3A",
            TableLines = "#2E2E2E",
            TableStriped = "#222222",
            TableHover = "#2E2E2E",
            Divider = "#2E2E2E",
            DividerLight = "#3A3A3A",
            OverlayDark = "rgba(0,0,0,0.6)",
            OverlayLight = "rgba(255,255,255,0.04)",
            Success = new HexColor(branding.SuccessColor).Value,
            Error = new HexColor(branding.ErrorColor).Value,
            Warning = new HexColor(branding.WarningColor).Value,
            Info = new HexColor(branding.InfoColor).Value,
            Dark = "#0E0E0E"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px",
            AppbarHeight = "64px",
            DrawerWidthLeft = "260px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"],
                FontSize = "14px",
                FontWeight = "500",
                LineHeight = "1.55"
            },
            H1 = new H1Typography { FontSize = "2.5rem", FontWeight = "700", LineHeight = "1.2" },
            H2 = new H2Typography { FontSize = "2rem", FontWeight = "700", LineHeight = "1.25" },
            H3 = new H3Typography { FontSize = "1.625rem", FontWeight = "700", LineHeight = "1.3" },
            H4 = new H4Typography { FontSize = "1.375rem", FontWeight = "700", LineHeight = "1.35" },
            H5 = new H5Typography { FontSize = "1.15rem", FontWeight = "700", LineHeight = "1.4" },
            H6 = new H6Typography { FontSize = "1rem", FontWeight = "700", LineHeight = "1.45" },
            Subtitle1 = new Subtitle1Typography { FontSize = "0.95rem", FontWeight = "600" },
            Subtitle2 = new Subtitle2Typography { FontSize = "0.85rem", FontWeight = "600" },
            Button = new ButtonTypography { FontWeight = "700", LetterSpacing = "0.5px" }
        }
    };
}
