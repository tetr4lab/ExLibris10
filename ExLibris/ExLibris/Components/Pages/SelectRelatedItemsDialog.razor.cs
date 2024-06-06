using Microsoft.AspNetCore.Components;
using MudBlazor;
using ExLibris.Data;

namespace ExLibris.Components.Pages;

/// <summary>関係先を選択するダイアログ</summary>
public partial class SelectRelatedItemsDialog<TItems, TItem>
    where TItems : ExLibrisBaseModel<TItems, TItem>, IExLibrisModel, new ()
    where TItem : ExLibrisBaseModel<TItem, TItems>, IExLibrisModel, new () {

    /// <summary>MudBlazorniに渡される自身のインスタンス(MudDialogInstance)</summary>
    [CascadingParameter] private MudDialogInstance MudDialog { get; set; } = new MudDialogInstance ();

    /// <summary>対象項目</summary>
    [Parameter] public TItem Item { get; set; } = new ();

    /// <summary>値の変更を通知</summary>
    [Parameter] public EventCallback OnChangeValue { get; set; }

    /// <summary>関連項目</summary>
    protected List<TItems>? items => Item.DataSet.GetAll<TItems> ()?.OrderBy (i => i.UniqueKey).ToList ();

    /// <summary>関連項目の比較器の実装</summary>
    protected class TItemsComparer : IEqualityComparer<TItems?> {
        /// <summary>比較</summary>
        bool IEqualityComparer<TItems?>.Equals (TItems? x, TItems? y) => x?.Id == y?.Id;
        /// <summary>比較用ハッシュ値</summary>
        int IEqualityComparer<TItems?>.GetHashCode (TItems? obj) => HashCode.Combine (obj?.Id);
    }

    /// <summary>関連項目の比較器のインスタンス</summary>
    protected TItemsComparer comparer = new ();

    /// <summary>選択された項目</summary>
    protected HashSet<TItems> selected { get; set; } = new HashSet<TItems> ();

    /// <summary>選択された項目を表すラベルを得る</summary>
    protected string GetSelectedLabel (List<string>? l = null) => string.Join (", ", selected.ToList ().ConvertAll (i => i.RowLabel));

    /// <summary>オートコンプリートコンポーネント</summary>
    protected MudAutocomplete<TItems> autoComplete { get; set; } = null!;

    /// <summary>オートコンプリートの選択値</summary>
    private TItems? autoCompleteSelected { get; set; }

    /// <summary>オートコンプリートの開閉状態の変化があった</summary>
    protected void OnOpenChanged (bool sw) {
        if (!sw && autoCompleteSelected != null) {
            // 選択されて閉じた
            selected.Add (autoCompleteSelected);
            autoComplete.Clear ();
            StateHasChanged ();
        }
    }

    /// <summary>キーワードを含む関連アイテムを探す</summary>
    protected async Task<IEnumerable<TItems>?> SearchItems (string keyword)
        => await Task.FromResult (string.IsNullOrWhiteSpace (keyword) ? new List<TItems> () : items?.FindAll (i => i.UniqueKey.Contains (keyword)));

    /// <summary>除去ボタンが押された</summary>
    protected void OnRemove (TItems item) {
        if (item != null) {
            selected.Remove (item);
            StateHasChanged ();
        }
    }

    /// <summary>初期化</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        selected = Item.RelatedItems.ToHashSet ();
    }

    /// <summary>取り消し</summary>
    protected void Cancel () => MudDialog.Cancel ();

    /// <summary>承認</summary>
    protected async Task Accept () {
        Item.RelatedItems = selected.ToList ();
        if (OnChangeValue.HasDelegate) {
            await OnChangeValue.InvokeAsync ();
        }
        MudDialog.Close (DialogResult.Ok (true));
    }
}

public static class SelectRelatedItemsDialogHelper {

    /// <summary>関係アイテムの編集ダイアログ</summary>
    public static async Task<DialogResult> SelectRelated<TItems, TItem> (this IDialogService dialogService, TItem item, Action action)
        where TItems : ExLibrisBaseModel<TItems, TItem>, IExLibrisModel, new()
        where TItem : ExLibrisBaseModel<TItem, TItems>, IExLibrisModel, new() {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, DisableBackdropClick = true, };
        var parameters = new DialogParameters {
            ["Item"] = item,
        };
        if (action?.Target != null) {
            parameters.Add ("OnChangeValue", EventCallback.Factory.Create (action.Target, action));
        }
        return await (await dialogService.ShowAsync<SelectRelatedItemsDialog<TItems, TItem>> ($"{TItems.TableLabel}の選択", parameters, options)).Result;
    }

}
