using MudBlazor;

namespace Web.Components;

public static class CtwTheme
{
    public static MudTheme Dark { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#2962FF",
            Secondary = "#00B8D4",
            Background = "#0E1218",
            Surface = "#1A1F2B",
            AppbarBackground = "#0E1218",
            DrawerBackground = "#141923",
            TextPrimary = "#E6E8EC",
            TextSecondary = "#9BA3AF",
            Success = "#26A69A",
            Error = "#EF5350",
            Warning = "#FFB300",
            Info = "#29B6F6",
            DrawerText = "#E6E8EC",
            AppbarText = "#E6E8EC"
        }
    };
}
