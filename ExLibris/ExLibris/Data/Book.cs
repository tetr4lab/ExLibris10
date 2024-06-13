using ExLibris.Services;
using PetaPoco;
using System.ComponentModel.DataAnnotations;

namespace ExLibris.Data;

[TableName ("Books")]
public class Book : ExLibrisBaseModel<Book, Author>, IExLibrisModel {
    [Column, StringLength (255)] public string Title { get; set; } = "";
    [Column] public string? Description { get; set; }
    [Column] public DateTime? PublishDate { get; set; }
    [Column, StringLength (255)] public string Publisher { get; set; } = "";
    [Column, StringLength (255)] public string Series { get; set; } = "";
    [Column] public decimal Price { get; set; }

    /// <summary>著者一覧</summary>
    public List<Author> Authors => RelatedItems;

    /// <inheritdoc/>
    public static string TableLabel => "書籍";

    /// <inheritdoc/>
    public static string Unit => "冊";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new Dictionary<string, string> {
        { nameof (Id), "ID" },
        { nameof (Title), "書名" },
        { nameof (Authors), "著者" },
        { nameof (Description), "説明" },
        { nameof (PublishDate), "発売日" },
        { nameof (Publisher), "出版社" },
        { nameof (Series), "叢書" },
        { nameof (Price), "価格" },
    };

    /// <inheritdoc/>
    public override string? RowLabel {
        get => Title;
        set => Title = value ?? "";
    }

    /// <inheritdoc/>
    public override string? [] SearchTargets => [Id.ToString (), Title, Description, PublishDate?.ToShortDateString (), Publisher, Series, $"¥{Price:#,0}", _relatedIds,];

    /// <inheritdoc/>
    public static string RelatedListName => nameof (Authors);

    /// <inheritdoc/>
    public override string [] UniqueKeys => [
        Title,
        Publisher,
        Series,
        PublishDate?.ToShortDateString () ?? "",
    ];

    /// <inheritdoc/>
    public static string UniqueKeysSql {
        get {
            var table = ExLibrisDataSet.GetSqlName<Book> ();
            var title = ExLibrisDataSet.GetSqlName<Book> ("Title");
            var publisher = ExLibrisDataSet.GetSqlName<Book> ("Publisher");
            var series = ExLibrisDataSet.GetSqlName<Book> ("Series");
            var publishdate = ExLibrisDataSet.GetSqlName<Book> ("PublishDate");
            return $"{table}.{title}<=>@Title and {table}.{publisher}<=>@Publisher and {table}.{series}<=>@Series and {table}.{publishdate}<=>@PublishDate";
        }
    }

    /// <inheritdoc/>
    public override Book Clone () => CopyDerivedMembers (base.Clone ());

    /// <inheritdoc/>
    public override Book CopyTo (Book destination) => CopyDerivedMembers (base.CopyTo (destination));

    /// <summary>派生メンバーだけをコピー</summary>
    private Book CopyDerivedMembers (Book destination) {
        destination.Title = string.IsNullOrEmpty (Title) ? "" : new (Title);
        destination.Description = string.IsNullOrEmpty (Description) ? null : new (Description);
        destination.PublishDate = PublishDate;
        destination.Publisher = string.IsNullOrEmpty (Publisher) ? "" : new (Publisher);
        destination.Series = string.IsNullOrEmpty (Series) ? "" : new (Series);
        destination.Price = Price;
        return destination;
    }

    /// <inheritdoc/>
    public override bool Equals (Book? other) =>
        other != null
        && Id == other.Id
        && Title == other.Title
        && Description == other.Description
        && PublishDate == other.PublishDate
        && Publisher == other.Publisher
        && Series == other.Series
        && Price == other.Price
        && RelatedIds.ContainsEquals (other.RelatedIds)
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (Id, Title, Description, PublishDate, Publisher, Series, Price, _relatedIds);

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Title} \"{Description}\" {Publisher} {Series} {PublishDate?.ToShortDateString ()} [{RelatedIds.Count}]{{{string.Join (",", RelatedItems.ConvertAll (a => $"{a.Id}:{a.Name}"))}}}";
}
