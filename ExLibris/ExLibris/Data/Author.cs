using ExLibris.Services;
using PetaPoco;
using System.ComponentModel.DataAnnotations;

namespace ExLibris.Data;

[TableName ("Authors")]
public class Author : ExLibrisBaseModel<Author, Book>, IExLibrisModel {
    [Column, StringLength (255), Required] public string Name { get; set; } = "";
    [Column, StringLength (255)] public string AdditionalName { get; set; } = "";
    [Column] public string? Description { get; set; }
    [Column, StringLength (50)] public string? Interest { get; set; }

    /// <summary>著書一覧</summary>
    public List<Book> Books => RelatedItems;

    /// <summary>関心</summary>
    public static List<string?> InterestOptions = [null, "古", "微", "小", "中", "確認", "購入",];

    /// <summary>関心値</summary>
    public int InterestValue => Math.Max (0, InterestOptions.IndexOf (Interest));

    /// <inheritdoc/>
    public override string StoreURL => $"{base.StoreURL}\"{Name}\"%20{AdditionalName}";

    /// <inheritdoc/>
    public static string TableLabel => "著者";

    /// <inheritdoc/>
    public static string Unit => "名";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new Dictionary<string, string> {
        { nameof (Id), "ID" },
        { nameof (Name), "著者名" },
        { nameof (AdditionalName), "補助名" },
        { nameof (Description), "説明" },
        { nameof (Interest), "関心" },
        { nameof (Books), "著書" },
    };

    /// <inheritdoc/>
    public override string? RowLabel {
        get => Name;
        set => Name = value ?? "";
    }

    /// <inheritdoc/>
    public override string? [] SearchTargets => [Id.ToString (), Name, AdditionalName, Description, Interest, _relatedIds,];

    /// <inheritdoc/>
    public static string RelatedListName => nameof (Books);

    /// <inheritdoc/>
    public override string [] UniqueKeys => [Name, AdditionalName,];

    /// <inheritdoc/>
    public static string UniqueKeysSql {
        get {
            var table = ExLibrisDataSet.GetSqlName<Author> ();
            var name = ExLibrisDataSet.GetSqlName<Author> ("Name");
            var additionalName = ExLibrisDataSet.GetSqlName<Author> ("AdditionalName");
            return $"{table}.{name}<=>@Name and {table}.{additionalName}<=>@AdditionalName";
        }
    }

    /// <inheritdoc/>
    public override Author Clone () => CopyDerivedMembers (base.Clone ());

    /// <inheritdoc/>
    public override Author CopyTo (Author destination) => CopyDerivedMembers (base.CopyTo (destination));

    /// <summary>派生メンバーだけをコピー</summary>
    private Author CopyDerivedMembers (Author destination) {
        destination.Name = string.IsNullOrEmpty (Name) ? "" : new (Name);
        destination.AdditionalName = string.IsNullOrEmpty (AdditionalName) ? "" : new (AdditionalName);
        destination.Description = Description == null ? null : new (Description);
        destination.Interest = Interest == null ? null : new (Interest);
        return destination;
    }

    /// <inheritdoc/>
    public override bool Equals (Author? other) =>
        other != null
        && Id == other.Id
        && Name == other.Name
        && AdditionalName == other.AdditionalName
        && Description == other.Description
        && Interest == other.Interest
        && RelatedIds.ContainsEquals (other.RelatedIds)
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (Id, Name, AdditionalName, Description, _relatedIds);

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Name}{(string.IsNullOrEmpty (AdditionalName) ? "" : $"-{AdditionalName}")} \"{Description}\" {Interest} [{RelatedIds.Count}]{{{string.Join (',', RelatedItems.ConvertAll (b => $"{b.Id}:{b.Title}"))}}}";

}
