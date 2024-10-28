using Microsoft.AspNetCore.Components;
using MudBlazor;
using ExLibris.Data;
using ExLibris.Components.Pages;

namespace ExLibris.Services;

public static partial class MudDialogServiceHelper {

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

}
