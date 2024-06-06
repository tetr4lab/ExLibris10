using ExLibris.Services;
using PetaPoco;
using System.Data;

namespace ExLibris.Data;

public interface IExLibrisModel {
    /// <summary>テーブル名</summary>
    public static abstract string TableLabel { get; }
    /// <summary>単位</summary>
    public static abstract string Unit { get; }
    /// <summary>列の名前</summary>
    public static abstract Dictionary<string, string> Label { get; }
    /// <summary>関係リスト名</summary>
    public static abstract string RelatedListName { get; }
    /// <summary>ユニーク識別子群のSQL表現</summary>
    public static abstract string UniqueKeysSql { get; }
}

/// <summary>基底モデル</summary>
/// <typeparam name="T1">自身</typeparam>
/// <typeparam name="T2">関係先</typeparam>
/// <remarks>派生先で必要に応じて`[TableName ("~")]`を加える</remarks>
[PrimaryKey ("Id", AutoIncrement = true), ExplicitColumns]
public abstract class ExLibrisBaseModel<T1, T2>
    where T1 : ExLibrisBaseModel<T1, T2>, new()
    where T2 : ExLibrisBaseModel<T2, T1>, new() {

    /// <summary>行の名前 (代表的なカラムを参照)</summary>
    public abstract string? RowLabel { get; set; }

    /// <summary>検索対象 (複数のカラムを参照)</summary>
    public abstract string? [] SearchTargets { get; }

    /// <summary>ユニーク識別子</summary>
    public string UniqueKey => string.Join ("-", UniqueKeys);

    /// <summary>ユニーク識別子群</summary>
    public abstract string [] UniqueKeys { get; }

    /// <summary>識別子</summary>
    [Column] public int Id { get; set; }

    /// <summary>バージョン</summary>
    [Column] public int Version { get; set; }

    /// <summary>文字列によるIdリスト</summary>
    [Column, VirtualColumn]
    public string? _relatedIds { get; set; }

    /// <summary>数値によるIdリスト</summary>
    public List<int> RelatedIds {
        get => (__relatedIds ??= string.IsNullOrEmpty (_relatedIds)? new () : _relatedIds.Split (',').ToList ().ConvertAll (id => int.TryParse (id, out var Id) ? Id : 0)) ?? new ();
        set {
            _relatedIds = value == null ? null : string.Join (",", value);
            __relatedItems = default;
        }
    }
    protected List<int>? __relatedIds { get; set; }

    /// <summary>関係先インスタンス</summary>
    public List<T2> RelatedItems {
        // RelatedIdsがnullなら_relatedItemsもnullになって、次回に再度生成を試みる。nullでも空のリストを返す。
        get => (__relatedItems ??= DataSet == null ? null : RelatedIds.ConvertAll (id => DataSet.GetAll<T2> ().Find (item => item.Id == id) ?? new ())) ?? new ();
        set {
            _relatedIds = string.Join (",", value.ConvertAll (item => item.Id));
            __relatedItems = default;
        }
    }
    protected List<T2>? __relatedItems { get; set; }

    /// <summary>所属するデータセット</summary>
    public ExLibrisDataSet DataSet { get; set; } = default!;

    /// <summary>クローン</summary>
    public abstract T1 Clone ();

    /// <summary>値のコピー</summary>
    public abstract T1 CopyTo (T1 destination);

    /// <summary>内容の比較</summary>
    public abstract bool Equals (object? obj, bool includeRelation);
}

public static class ExLibrisModelHelper {
    /// <summary>リスト内容の比較</summary>
    public static bool ContainsEquals<T> (this IEnumerable<T> items, IEnumerable<T> others) {
        if (items == null || others == null || items.Count () != others.Count ()) {
            return false;
        }
        foreach (var item in items) {
            if (!others.Contains (item)) { return false; }
        }
        return true;
    }
}
