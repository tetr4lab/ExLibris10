using ExLibris.Services;
using PetaPoco;

namespace ExLibris.Data;

[TableName (""), PrimaryKey ("Id"), ExplicitColumns]
public class ExLibrisBaseModel<T1, T2>
    where T1 : ExLibrisBaseModel<T1, T2>, new()
    where T2 : ExLibrisBaseModel<T2, T1>, new() {
    [Column] public int Id { get; set; }
    [Column] public int Version { get; set; }
    [Column] protected string? _relatedIds { get; set; }

    /// <summary>コンマ区切り文字列で表現された関係先Id(_relatedIds)へのアクセサー</summary>
    public string? RelatedIds {
        get => _relatedIds;
        protected set {
            _relatedIds = value;
            _relatedItems = default;
        }
    }

    /// <summary>関係先インスタンス</summary>
    public List<T2> RelatedItems {
        get => (_relatedItems ??= (RelatedIds ?? "").Split (',').ToList ().ConvertAll (id => DataSet.GetAll<T2> ().Find (item => item.Id == int.Parse (id)) ?? new ()));
        set {
            _relatedIds = string.Join (",", value.ConvertAll (item => item.Id));
            _relatedIds = default;
        }
    }
    protected List<T2>? _relatedItems { get; set; }

    /// <summary>所属するデータセット</summary>
    public ExLibrisDataSet DataSet { get; set; } = default!;
}
