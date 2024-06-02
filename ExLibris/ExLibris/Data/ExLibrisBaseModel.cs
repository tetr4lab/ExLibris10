using ExLibris.Services;
using PetaPoco;

namespace ExLibris.Data;

[TableName (""), PrimaryKey ("Id"), ExplicitColumns]
public class ExLibrisBaseModel<T1, T2>
    where T1 : ExLibrisBaseModel<T1, T2>, new()
    where T2 : ExLibrisBaseModel<T2, T1>, new() {
    [Column] public int Id { get; set; }
    [Column] public int Version { get; set; }
    [Column] public string? _relatedIds { get; set; }
    public string? RelatedIds {
        get => _relatedIds;
        set {
            _relatedIds = value;
            _relatedItems = default;
        }
    }
    public List<T2> RelatedItems {
        get => (_relatedItems ??= (RelatedIds ?? "").Split (',').ToList ().ConvertAll (id => DataSet.GetAll<T2> ().Find (item => item.Id == int.Parse (id)) ?? new ()));
        set {
            _relatedIds = string.Join (",", value.ConvertAll (item => item.Id));
            _relatedIds = default;
        }
    }
    public List<T2>? _relatedItems { get; set; }
    public ExLibrisDataSet DataSet { get; set; } = default!;
}
