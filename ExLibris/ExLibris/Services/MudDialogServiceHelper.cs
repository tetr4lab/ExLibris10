using Microsoft.AspNetCore.Components;
using MudBlazor;
using ExLibris.Data;
using ExLibris.Components.Pages;

namespace ExLibris.Services;

public static class MudDialogServiceHelper {

    /// <summary>アイテム用ダイアログを開く</summary>
    public static async Task<IDialogReference> OpenItemDialog<T1, T2> (this IDialogService service, T1 item, Func<T2, Task> changed, Action updated, EventCallback<string> setFilterText) {
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true, };
        var parameters = new DialogParameters { };
        if (changed.Target != null) {
            parameters.Add ("OnChangeDialog", EventCallback.Factory.Create (changed.Target, changed));
        }
        if (updated.Target != null) {
            parameters.Add ("OnStateHasChanged", EventCallback.Factory.Create (updated.Target, updated));
        }
        parameters.Add ("SetFilterText", setFilterText);
        if (item is Book book) {
            parameters.Add ("Item", book);
            return await service.ShowAsync<BookDialog> ($"{Book.TableLabel}詳細", parameters, options);
        } else if (item is Author author) {
            parameters.Add ("Item", author);
            return await service.ShowAsync<AuthorDialog> ($"{Author.TableLabel}詳細", parameters, options);
        }
        throw new ArgumentNullException (nameof (item));
    }

    /// <summary>リロードの確認</summary>
    public static async Task<DialogResult?> ReloadConfirmation (this IDialogService dialogService, IEnumerable<string?> message, string cancellationLabel = "")
        => await Confirmation (dialogService, message, title: "リロードの確認", acceptionLabel: "Reload", acceptionColor: Color.Success, acceptionIcon: Icons.Material.Filled.Refresh, cancellationLabel: cancellationLabel);

    /// <summary>汎用の確認</summary>
    public static async Task<DialogResult?> Confirmation (this IDialogService dialogService, IEnumerable<string?> message, string? title = null, MaxWidth width = MaxWidth.Small, DialogPosition position = DialogPosition.Center, string acceptionLabel = "Ok", Color acceptionColor = Color.Success, string? acceptionIcon = Icons.Material.Filled.Check, string cancellationLabel = "Cancel", Color cancellationColor = Color.Default) {
        var options = new DialogOptions { MaxWidth = width, FullWidth = true, Position = position, BackdropClick = false, };
        var parameters = new DialogParameters {
            ["Contents"] = message,
            ["AcceptionLabel"] = acceptionLabel,
            ["AcceptionColor"] = acceptionColor,
            ["AcceptionIcon"] = acceptionIcon,
            ["CancellationLabel"] = cancellationLabel,
            ["CancellationColor"] = cancellationColor,
        };
        return await (await dialogService.ShowAsync<ConfirmationDialog> (title ?? "確認", parameters, options)).Result;
    }

}
