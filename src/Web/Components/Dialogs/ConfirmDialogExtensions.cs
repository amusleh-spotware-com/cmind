using MudBlazor;

namespace Web.Components.Dialogs;

// One-liner for destructive-action confirmation. Call sites pass already-localized strings and gate the
// mutation on the returned bool: `if (!await Dialogs.ConfirmAsync(...)) return;`. This is the single
// approved path — DestructiveActionConfirmTests fails the build on a delete/erase handler that mutates
// without going through it.
public static class ConfirmDialogExtensions
{
    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogs,
        string title,
        string message,
        string confirmText,
        string cancelText,
        Color confirmColor = Color.Error)
    {
        var parameters = new DialogParameters
        {
            { nameof(ConfirmDialog.Title), title },
            { nameof(ConfirmDialog.Message), message },
            { nameof(ConfirmDialog.ConfirmText), confirmText },
            { nameof(ConfirmDialog.CancelText), cancelText },
            { nameof(ConfirmDialog.ConfirmColor), confirmColor },
        };

        var dialog = await dialogs.ShowAsync<ConfirmDialog>(title, parameters);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: true };
    }
}
