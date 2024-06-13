using ExLibris.Services;
using PetaPoco;
using System.ComponentModel.DataAnnotations;

namespace ExLibris.Data;

[TableName ("Authors")]
public class Author : ExLibrisBaseModel<Author, Book>, IExLibrisModel {
    [Column, StringLength (255)] public string Name { get; set; } = "";
    [Column, StringLength (255)] public string AdditionalName { get; set; } = "";
    [Column] public string? Description { get; set; }

    /// <summary>著書一覧</summary>
    public List<Book> Books => RelatedItems;

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
        { nameof (Books), "著書" },
    };

    /// <inheritdoc/>
    public override string? RowLabel {
        get => Name;
        set => Name = value ?? "";
    }

    /// <inheritdoc/>
    public override string? [] SearchTargets => [Id.ToString (), Name, AdditionalName, Description, _relatedIds,];

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
    public static string OrderSql => $"{ExLibrisDataSet.GetSqlName<Author> ()}.{ExLibrisDataSet.GetSqlName<Author> ("Name")} ASC";

    /// <inheritdoc/>
    public override Author Clone () => CopyDerivedMembers (base.Clone ());

    /// <inheritdoc/>
    public override Author CopyTo (Author destination) => CopyDerivedMembers (base.CopyTo (destination));

    /// <summary>派生メンバーだけをコピー</summary>
    private Author CopyDerivedMembers (Author destination) {
        destination.Name = string.IsNullOrEmpty (Name) ? "" : new (Name);
        destination.AdditionalName = string.IsNullOrEmpty (AdditionalName) ? "" : new (AdditionalName);
        destination.Description = string.IsNullOrEmpty (Description) ? null : new (Description);
        return destination;
    }

    /// <inheritdoc/>
    public override bool Equals (Author? other) =>
        other != null
        && Id == other.Id
        && Name == other.Name
        && AdditionalName == other.AdditionalName
        && Description == other.Description
        && RelatedIds.ContainsEquals (other.RelatedIds)
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (Id, Name, AdditionalName, Description, _relatedIds);

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Name}{(string.IsNullOrEmpty (AdditionalName) ? "" : $"-{AdditionalName}")} \"{Description}\" [{RelatedIds.Count}]{{{string.Join (",", RelatedItems.ConvertAll (b => $"{b.Id}:{b.Title}"))}}}";

}
