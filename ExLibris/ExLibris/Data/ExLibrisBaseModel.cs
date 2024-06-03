using ExLibris.Services;
using PetaPoco;
using System.ComponentModel.DataAnnotations;

namespace ExLibris.Data;

/// <summary>基底モデル</summary>
/// <typeparam name="T1">自身</typeparam>
/// <typeparam name="T2">関係先</typeparam>
/// <remarks>派生先で必要に応じて`[TableName ("~")]`を加える</remarks>
[PrimaryKey ("Id", AutoIncrement = true), ExplicitColumns]
public class ExLibrisBaseModel<T1, T2>
    where T1 : ExLibrisBaseModel<T1, T2>, new()
    where T2 : ExLibrisBaseModel<T2, T1>, new() {

    [Column] public int Id { get; set; }
    [Column] public int Version { get; set; }

    ///// <summary>関係先Idリスト</summary>
    //public List<int>? RelatedIds { get; set; }

    ///// <summary>関係先インスタンス</summary>
    //public List<T2> RelatedItems {
    //    // RelatedIdsがnullなら_relatedItemsもnullになって、次回に再度生成を試みる。nullでも空のリストを返す。
    //    get => (_relatedItems ??= RelatedIds?.ConvertAll (Id => DataSet.GetAll<T2> ().Find (item => item.Id == Id) ?? new ())) ?? new ();
    //    set {
    //        RelatedIds = value.ConvertAll (item => item.Id);
    //        _relatedItems = default;
    //    }
    //}

    /// <summary>文字列によるIdリスト</summary>
    [Column, VirtualColumn]
    public string? _relatedIds { get; set; }

    /// <summary>数値によるIdリスト</summary>
    public List<int>? RelatedIds {
        get => (_relatedIds ?? "").Split (',').ToList ().ConvertAll (id => int.TryParse (id, out var Id) ? Id : 0);
        protected set {
            _relatedIds = value == null ? null : string.Join (",", value);
            _relatedItems = default;
        }
    }

    /// <summary>関係先インスタンス</summary>
    public List<T2> RelatedItems {
        // RelatedIdsがnullなら_relatedItemsもnullになって、次回に再度生成を試みる。nullでも空のリストを返す。
        get => (_relatedItems ??= (RelatedIds ?? new ()).ConvertAll (id => DataSet.GetAll<T2> ().Find (item => item.Id == id) ?? new ()));
        set {
            _relatedIds = string.Join (",", value.ConvertAll (item => item.Id));
            _relatedItems = default;
        }
    }
    protected List<T2>? _relatedItems { get; set; }

    /// <summary>所属するデータセット</summary>
    public ExLibrisDataSet DataSet { get; set; } = default!;
}

/// <summary>関係先Idリストをマップするだけのクラス</summary>
public class Related {
    public string? Ids { get; set; }
}
