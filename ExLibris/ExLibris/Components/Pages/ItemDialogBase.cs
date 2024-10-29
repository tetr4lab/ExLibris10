using ExLibris.Data;
using ExLibris.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using PetaPoco;
using Tetr4lab;
using static ExLibris.Services.ExLibrisDataSet;
using Status = ExLibris.Services.Status;

namespace ExLibris.Components.Pages;

public class ItemDialogBase<TItem1, TItem2> : ComponentBase, IDisposable
        where TItem1 : ExLibrisBaseModel<TItem1, TItem2>, IExLibrisModel, new()
        where TItem2 : ExLibrisBaseModel<TItem2, TItem1>, IExLibrisModel, new() {

    [Inject] protected NavigationManager NavManager { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;
    [CascadingParameter] protected MudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>データセット</summary>
    protected ExLibrisDataSet DataSet { get; set; } = null!;

    /// <summary>対象項目</summary>
    [Parameter] public TItem1 Item { get; set; } = null!;

    /// <summary>更新を親に伝える</summary>
    [Parameter] public EventCallback OnStateHasChanged { get; set; }

    /// <summary>対象アイテムの切り替えを親に伝える</summary>
    [Parameter] public EventCallback<TItem2> OnChangeDialog { get; set; }

    /// <summary>検索文字列設定</summary>
    [Parameter] public EventCallback<string> SetFilterText { get; set; }

    /// <summary>エディットモードの初期フォーカス対象 (@refで指定)</summary>
    protected MudTextField<string>? FocusTarget { get; set; }

    /// <summary>新規判定</summary>
    protected bool IsNewItem => Item.Id == default;

    /// <summary>ダイアログ切り替え</summary>
    protected async void ChangeDialog (TItem2 item) {
        MudDialog.Close ();
        if (OnChangeDialog.HasDelegate) {
            await OnChangeDialog.InvokeAsync (item);
        }
    }

    /// <summary>アイテムリストの更新があった</summary>
    protected void OnDataSetChanged () {
        if (OnStateHasChanged.HasDelegate) {
            OnStateHasChanged.InvokeAsync ();
        }
    }

    /// <summary>モード切り替え</summary>
    protected bool OnEdit {
        get => _onEdit;
        set {
            if (_onEdit != value) {
                MudDialog.Options.CloseButton = !value;
                MudDialog.Options.CloseOnEscapeKey = !value;
                MudDialog.Options.BackdropClick = !value;
                MudDialog.SetTitle (value ? editorTitle : originalTitle);
                MudDialog.SetOptions (MudDialog.Options);
                _onEdit = value;
                _wasOnEditThisRanderFrame = value;
                StateHasChanged ();
            }
        }
    }
    protected bool _onEdit = false;

    /// <summary>この描画フレームで編集モードになった</summary>
    protected bool _wasOnEditThisRanderFrame = false;

    /// <summary>編集中で更新がある</summary>
    protected bool Updated => OnEdit && _backupItem != null && !Item.Equals (_backupItem);

    /// <summary>関係アイテムの編集</summary>
    protected async void OnEditRelatedItems () {
        await DialogService.SelectRelated<TItem2, TItem1> (Item, () => StateHasChanged ());
    }

    /// <summary>編集開始</summary>
    protected void StartEdit () {
        _backupItem = Item.Clone ();
        OnEdit = true;
    }
    protected TItem1? _backupItem;

    /// <summary>タイムアウトなどになった</summary>
    private async Task OperationTimeout (string operation, Status status = Status.CommandTimeout) {
        await DataSet.LoadAsync ();
        var messages = new [] {
                $"{status.GetName ()}により{operation}できませんでした。",
                "時間をおいてやり直してみてください。",
            };
        await DialogService.Confirmation (messages, position: DialogPosition.BottomCenter, cancellationLabel: "");
    }

    /// <summary>不明のエラーが生じた</summary>
    private async Task UnknownError () {
        await DataSet.LoadAsync ();
        var messages = new [] {
                $"不明のエラーが生じました。",
                $"エラーに至る詳細な操作を管理者へ連絡してください。",
            };
        await DialogService.Confirmation (messages, position: DialogPosition.BottomCenter, cancellationLabel: "");
        Snackbar.Add ("編集を破棄しました。");
    }

    /// <summary>対象が消失した</summary>
    private async Task MissingEntry (bool onDelete = false) {
        await DataSet.LoadAsync ();
        var messages = new [] {
            $"{(onDelete ? "対象を削除" : "編集を保存")}できませんでした。",
            "対象は既に削除されていました。",
            $"{(onDelete ? "対象" : "編集")}: {Item}",
            onDelete ? null : "編集を破棄します。",
        };
        var dialogResult = await DialogService.Confirmation (messages, acceptionColor: Color.Warning, cancellationLabel: "");
        if (!onDelete) {
            Snackbar.Add ("編集を破棄しました。");
        }
    }

    /// <summary>バージョン不整合が生じた</summary>
    private async Task VersionMissmatch (bool onDelete = false) {
        await DataSet.LoadAsync ();
        var original = DataSet.GetItemById<TItem1, TItem2> (Item);
        var messages = new [] {
            $"{(onDelete ? "対象を削除" : "編集を保存")}できませんでした。",
            "他所で更新された可能性があります。",
            $"{(onDelete ? "対象" : "編集")}: {Item}",
            original != null ? $"競合: {original}" : null,
            onDelete ? null : "編集を破棄します。",
        };
        var dialogResult = await DialogService.Confirmation (messages, acceptionColor: Color.Warning, cancellationLabel: "");
        if (!onDelete) {
            Snackbar.Add ("編集を破棄しました。");
        }
    }

    /// <summary>エントリが重複した</summary>
    private async Task DuplicateEntry (string operation) {
        await DataSet.LoadAsync ();
        var existence = DataSet.GetOtherItemByName<TItem1, TItem2> (Item);
        var messages = new [] {
            $"編集を{operation}できませんでした。",
            $"名前「{Item.UniqueKey}」が重複しています。",
            $"編集: {Item}",
            $"競合: {existence}",
        };
        var dialogResult = await DialogService.Confirmation (messages, position: DialogPosition.BottomCenter, cancellationLabel: "");
    }

    /// <summary>関係先が保存できなかった</summary>
    private async Task RelatedListUpdateFailed () {
        await DataSet.LoadAsync ();
        // リンク切れの関係先を破棄
        var relationIds = Item.RelatedIds;
        Item.RelatedIds = Item.RelatedIds.FindAll (DataSet.ExistsById<TItem2, TItem1>);
        var exception = relationIds.Except (Item.RelatedIds).ToList ();
        var messages = new [] {
            $"{exception.Count:N0}件の{TItem2.TableLabel}{{Id: {string.Join (',', exception)}}}が保存できませんでした。",
            $"編集中に他所で編集された可能性があります。",
        };
        await DialogService.Confirmation (messages, position: DialogPosition.BottomCenter, cancellationLabel: "");
    }

    /// <summary>ダイアログの状態変更</summary>
    private void Update (bool close, bool endEdit = true) {
        if (close || endEdit) {
            _backupItem = null;
            OnEdit = false;
            if (close) {
                MudDialog.Close (DialogResult.Ok (true));
            }
        }
        // 表示の更新
        StateHasChanged ();
        OnDataSetChanged ();
    }

    /// <summary>削除</summary>
    protected async void Delete () {
        // 確認
        var contents = new [] { $"この{TItem1.TableLabel}を完全に削除します。", $"「{Item.Id}: {Item.RowLabel}」" };
        var dialogResult = await DialogService.Confirmation (contents, title: $"{TItem1.TableLabel}削除", position: DialogPosition.BottomCenter, acceptionLabel: "Delete", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
        if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
            // 実際の削除
            var result = await DataSet.RemoveAsync<TItem1, TItem2> (Item);
            var close = false;
            // 報告
            var title = $"{TItem1.TableLabel}「{Item.Id}: {Item.RowLabel}」";
            switch (result.Status) {
                case Status.Success:
                    Snackbar.Add ($"{title}を削除しました。");
                    close = true;
                    break;
                case Status.CommandTimeout:
                case Status.DeadlockFound:
                    await OperationTimeout ($"{title}を削除", result.Status);
                    break;
                case Status.MissingEntry:
                    await MissingEntry (onDelete: true);
                    close = true;
                    break;
                case Status.VersionMismatch:
                    await VersionMissmatch (onDelete: true);
                    break;
                default:
                    await UnknownError ();
                    break;
            }
            // ダイアログの状態変更
            Update (close);
        }
    }

   /// <summary>エラー</summary>
    protected virtual bool IsError => !string.IsNullOrEmpty (ErrorReport);

    /// <summary>エラー報告</summary>
    protected virtual string ErrorReport {
        get {
            // ToDo: required
            foreach (var property in typeof (TItem1).GetProperties (BindingFlags.Instance | BindingFlags.Public) ?? []) {
                if (property.GetCustomAttribute<RequiredAttribute> () != null && property.GetCustomAttribute<ColumnAttribute> () != null) {
                    var value = property.GetValue (Item);
                    if (value == default || (value is string str && string.IsNullOrEmpty (str))) {
                        return "必須項目を入力してください。";
                    }
                }
            }
            if (DataSet.ExistsOtherByName<TItem1, TItem2> (Item)) {
                return $"同じ{TItem1.TableLabel}が存在します。";
            } else {
                return string.Empty;
            }
        }
    }

    /// <summary>セーブ中</summary>
    protected bool isSaving = false;

    /// <summary>セーブして編集を終了</summary>
    protected virtual async void SaveAsync () {
        if (isSaving || _backupItem == null) { return; }
        if (IsError) {
            Snackbar.Add ("保存するためにはエラーの解消が必要です。");
            return;
        }
        isSaving = true;
        var updated = Updated;
        var endEdit = false;
        var close = false;
        if (IsNewItem) {
            // 新規
            // 実際の追加
            var result = await DataSet.AddAsync<TItem1, TItem2> (Item);
            switch (result.Status) {
                case Status.Success:
                    Snackbar.Add ("編集を追加しました。");
                    endEdit = true;
                    break;
                case Status.ForeignKeyConstraintFails:
                    await RelatedListUpdateFailed ();
                    break;
                case Status.CommandTimeout:
                case Status.DeadlockFound:
                    await OperationTimeout ("編集を追加", result.Status);
                    break;
                case Status.DuplicateEntry:
                    await DuplicateEntry ("追加");
                    break;
                default:
                    await UnknownError ();
                    endEdit = close = true;
                    break;
            }
        } else if (updated) {
            // 更新
            // 実際の更新
            var result = await DataSet.UpdateAsync<TItem1, TItem2> (Item);
            switch (result.Status) {
                case Status.Success:
                    Snackbar.Add ("編集を保存しました。");
                    endEdit = true;
                    break;
                case Status.ForeignKeyConstraintFails:
                    await RelatedListUpdateFailed ();
                    break;
                case Status.CommandTimeout:
                case Status.DeadlockFound:
                    await OperationTimeout ("編集を保存", result.Status);
                    break;
                case Status.MissingEntry:
                    await MissingEntry ();
                    endEdit = close = true;
                    break;
                case Status.VersionMismatch:
                    await VersionMissmatch ();
                    endEdit = true;
                    break;
                case Status.DuplicateEntry:
                    await DuplicateEntry ("保存");
                    break;
                default:
                    await UnknownError ();
                    endEdit = close = true;
                    break;
            }
        } else {
            Snackbar.Add ("更新はありませんでした。");
        }
        // ダイアログの状態変更
        Update (close, endEdit);
        isSaving = false;
    }

    /// <summary>原状復帰して編集を終了</summary>
    protected async void CancelEdit () {
        if (_backupItem != null) {
            if (!Item.Equals (_backupItem)) {
                // 確認
                var messages = new [] {
                    "編集内容が失われます。",
                    "編集を破棄しますか?",
                };
                var dialogResult = await DialogService.Confirmation (messages, position: DialogPosition.BottomCenter, acceptionLabel: "Discard Editing", acceptionIcon: Icons.Material.Filled.Delete, acceptionColor: Color.Warning);
                if (dialogResult == null || dialogResult.Canceled) {
                    // 破棄を取り消し
                    return;
                }
                // 実際の破棄
                _backupItem.CopyTo (Item);
                // 報告
                Snackbar.Add ("編集を破棄しました。");
            }
            // 編集の終了
            _backupItem = null;
            OnEdit = false;
            if (IsNewItem) {
                // 新規アイテムだったらダイアログを閉じる
                MudDialog.Cancel ();
            }
        }
    }

    //// <summary>絞りこんで関係先ページを表示</summary>
    protected async Task FilterAndNavigate (string mark, string uri) {
        var filter = string.Join ("|", Item.RelatedItems.ConvertAll (i => $"{mark}{i.Id}."));
        await SetFilterText.InvokeAsync (filter);
        NavManager.NavigateTo (uri);
    }

    /// <summary>元のタイトル</summary>
    protected string originalTitle = string.Empty;

    /// <summary>編集時タイトル</summary>
    protected readonly string editorTitle = $"{TItem1.TableLabel}編集";

    /// <summary>初期化</summary>
    protected override void OnInitialized () {
        base.OnInitialized ();
        originalTitle = MudDialog.Title ?? "";
        DataSet = Item.DataSet;
        if (IsNewItem) {
            // 新規なら編集モードへ
            StartEdit ();
        }
    }

    /// <summary>描画完了後処理</summary>
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        if (_wasOnEditThisRanderFrame) {
            // 編集モード開始時
            _wasOnEditThisRanderFrame = false;
            if (firstRender && IsNewItem) {
                // 編集モードに切り替わるのを待つ
                await TaskEx.DelayOneFrame;
            }
            // フォーカスをセット
            if (FocusTarget != null && string.IsNullOrEmpty (FocusTarget.Value)) {
                await FocusTarget.FocusAsync ();
            }
        }
        await base.OnAfterRenderAsync (firstRender);
    }

    /// <summary>破棄時</summary>
    public void Dispose () {
        CancelEdit ();
    }

}
