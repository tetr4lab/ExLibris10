﻿using ExLibris.Data;
using ExLibris.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Tetr4lab;
using ExLibris.Utilities;
using static ExLibris.Services.ExLibrisDataSet;

namespace ExLibris.Components.Pages;

public class ItemListBase<TItem1, TItem2> : ComponentBase, IDisposable
    where TItem1 : ExLibrisBaseModel<TItem1, TItem2>, IExLibrisModel, new()
    where TItem2 : ExLibrisBaseModel<TItem2, TItem1>, IExLibrisModel, new() {

    /// <summary>列挙する最大数</summary>
    protected const int MaxListingNumber = 500;

    [Inject] protected NavigationManager NavManager { get; set; } = null!;
    [Inject] protected ExLibrisDataSet DataSet { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;

    /// <summary>検索文字列</summary>
    [CascadingParameter (Name = "Filter")] protected string? FilterText { get; set; }

    /// <summary>セクションラベル設定</summary>
    [CascadingParameter (Name = "Section")] protected EventCallback<string> SetSectionTitle { get; set; }

    /// <summary>セッション数の更新</summary>
    [CascadingParameter (Name = "Session")] protected EventCallback<int> UpdateSessionCount { get; set; }

    /// <summary>項目一覧</summary>
    protected List<TItem1>? items => DataSet.GetAll<TItem1> ();

    /// <summary>選択項目</summary>
    protected TItem1 selectedItem { get; set; } = new TItem1 ();

    /// <summary>複数選択項目</summary>
    protected HashSet<TItem1> selectedItems { get; set; } = new HashSet<TItem1> ();

    /// <summary>複数選択可否</summary>
    protected bool allowMultiSelection {
        get => _allowMultiSelection && SessionCounter.Count <= 1;
        set {
            if (_allowMultiSelection != value) {
                selectedItems = new ();
            }
            _allowMultiSelection = value;
        }
    }
    protected bool _allowMultiSelection = false;

    /// <summary>初期化</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        await SetSectionTitle.InvokeAsync ($"{typeof (TItem1).Name}s");
        // セッション数の変化を購読
        SessionCounter.Subscribe (this, () => InvokeAsync (StateHasChanged));
    }

    /// <summary>破棄</summary>
    public void Dispose () {
        SessionCounter.Unsubscribe (this);
    }

    /// <summary>描画後処理</summary>
    protected override async void OnAfterRender (bool firstRender) {
        base.OnAfterRender (firstRender);
        if (firstRender) {
            await DataSet.InitializeAsync ();
            StateHasChanged ();
        }
    }

    /// <summary>表示の更新</summary>
    protected void Update () { }// `=> StateHasChanged();`の処理は、コールバックを受けた時点で内部的に呼ばれているため、明示的な呼び出しは不要

    /// <summary>項目詳細・編集</summary>
    protected async void EditItem (TItem1 item) {
        if (!allowMultiSelection) {
            await OpenDialog (item);
        }
    }

    /// <summary>ダイアログを開く</summary>
    protected async Task OpenDialog (TItem1 item)
        => await (await DialogService.OpenItemDialog<TItem1, TItem2> (item, OpenRelationDialog, Update)).Result;

    /// <summary>関係ダイアログを開く</summary>
    protected async Task OpenRelationDialog (TItem2 item)
        => await (await DialogService.OpenItemDialog<TItem2, TItem1> (item, OpenDialog, Update)).Result;

    /// <summary>表示の更新と反映待ち</summary>
    protected async Task StateHasChangedAsync () {
        StateHasChanged ();
        await TaskEx.DelayOneFrame;
    }

    /// <summary>項目追加</summary>
    protected async void AddItem () {
        if (_isAdding) { return; }
        _isAdding = true;
        await StateHasChangedAsync ();
        await (await DialogService.OpenItemDialog<TItem1, TItem2> (new TItem1 { DataSet = DataSet, }, OpenRelationDialog, Update)).Result;
        _isAdding = false;
        StateHasChanged ();
    }
    protected bool _isAdding;

    /// <summary>項目一括削除</summary>
    protected async void DeleteItem () {
        if (SessionCounter.Count > 1) {
            Snackbar.Add ("複数の利用者がいる場合は使用できません。");
            return;
        }
        if (_isDeleting) { return; }
        _isDeleting = true;
        await StateHasChangedAsync ();
        if (allowMultiSelection && selectedItems.Count > 0) {
            // 確認ダイアログ
            var targetCount = selectedItems.Count;
            var contents = selectedItems.ToList () [..Math.Min (MaxListingNumber, selectedItems.Count)]
                .ConvertAll (i => $"「{i.Id}: {i.RowLabel}」");
            if (selectedItems.Count > MaxListingNumber) {
                contents.Add ($"他 {selectedItems.Count - MaxListingNumber}{TItem1.Unit}");
            }
            contents.Insert (0, $"以下の{TItem1.TableLabel}({targetCount}{TItem1.Unit})を完全に削除します。");
            var dialogResult = await DialogService.Confirmation (contents, title: $"{TItem1.TableLabel}一括削除", width: MaxWidth.ExtraLarge, position: DialogPosition.BottomCenter, acceptionLabel: "Delete", acceptionColor: Color.Error);
            if (!dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                var resetAutoIncrement = new Result<int> (Status.Unknown, 0);
                // プログレスダイアログ
                dialogResult = await DialogService.Progress (async update => {
                    // 実際の削除
                    var result = await DataSet.RemoveRangeAsync<TItem1, TItem2> (selectedItems);
                    // リロード
                    await DataSet.LoadAsync ();
                    // 削除されたものをチェックから除外
                    var items = selectedItems;
                    selectedItems = new HashSet<TItem1> ();
                    var allItems = DataSet.GetAll<TItem1> ();
                    if (allItems != null) {
                        if (allItems.Count > 0) {
                            // 残りがある
                            foreach (var item in items) {
                                var finded = allItems.Find (i => item.Id == i.Id);
                                if (finded != null) {
                                    selectedItems.Add (finded);
                                }
                            }
                        } else {
                            // 全て削除されていたらオートインクリメントを初期化
                            resetAutoIncrement = await DataSet.ResetAutoIncrementAsync<TItem1, TItem2> ();
                        }
                    }
                    // 表示に反映
                    StateHasChanged ();
                    // 報告
                    var messages = new [] {
                        $"{TItem1.TableLabel} {result.Value}/{targetCount}を削除しました。",
                        result.Value < targetCount ? "一部または全部が削除できませんでした。" : null,
                        result.Status == Status.CommandTimeout
                          || result.Status == Status.DeadlockFound
                          ? $"{result.StatusName}があったので、時間をおいてやり直してみてください。" : null,
                        resetAutoIncrement.IsSuccess && resetAutoIncrement.Value > 0 ? $"{TItem1.TableLabel}が空になったので、Idを初期化しました。" : null,
                    };
                    Snackbar.Add (string.Join ("", messages), result.Value < targetCount ? Severity.Warning : Severity.Normal);
                    update (ProgressDialog.Acceptable, messages);
                }, indeterminate: true, title: "削除中", message: $"選択した{TItem1.TableLabel}を削除中です。アプリを終了しないでください。");
            }
        } else {
            Snackbar.Add ("項目が選択されていません。");
        }
        _isDeleting = false;
        StateHasChanged ();
    }
    protected bool _isDeleting;

    /// <summary>デフォルト項目数の設定</summary>
    protected override Task OnAfterRenderAsync (bool firstRender) {
        if (_table != null && !_inited) {
            _inited = true;
            _table.SetRowsPerPage (_pageSizeOptions [1]);
        }
        return base.OnAfterRenderAsync (firstRender);
    }
    protected bool _inited;
    protected MudTable<TItem1>? _table;
    /// <summary>項目数の選択肢</summary>
    protected int [] _pageSizeOptions = new [] { 10, 20, 25, 50, 100, 200, MaxListingNumber, };

    /// <summary>全ての検索語に対して対象列のどれかが真であれば真を返す</summary>
    protected bool FilterFunc (TItem1 item) {
        if (item != null && FilterText != null) {
            foreach (var word in FilterText.Split ([' ', '　', '\t', '\n'])) {
                if (!string.IsNullOrEmpty (word) && !Any (item.SearchTargets, word)) { return false; }
            }
            return true;
        }
        return false;
        // 対象文字列のどれかがキーワードを含んでいれば真を返す
        bool Any (IEnumerable<string?> targets, string word) {
            foreach (var target in targets) {
                if (!string.IsNullOrEmpty (target) && target.Contains (word)) { return true; }
            }
            return false;
        }
    }

}
