using ExLibris.Services;
using PetaPoco;

namespace ExLibris.Data;

[TableName ("Books")]
public class Book : ExLibrisBaseModel<Book, Author>, IExLibrisModel {
    [Column] public string Title { get; set; } = "";
    [Column] public string? Description { get; set; }
    [Column] public DateTime? PublishDate { get; set; }
    [Column] public string Publisher { get; set; } = "";
    [Column] public string Series { get; set; } = "";
    [Column] public decimal Price { get; set; }

    /// <summary>著者一覧</summary>
    public List<Author> Authors => RelatedItems;

    /// <summary>テーブル名</summary>
    public static string TableLabel => "書籍";

    /// <summary>単位</summary>
    public static string Unit => "冊";

    /// <summary>列の名前</summary>
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

    /// <summary>行の名前</summary>
    public override string? RowLabel {
        get => Title;
        set => Title = value ?? "";
    }

    /// <summary>検索対象</summary>
    public override string? [] SearchTargets => [Id.ToString (), Title, Description, PublishDate?.ToShortDateString (), Publisher, Series, $"¥{Price:#,0}", string.Join (",", _relatedIds),];

    /// <summary>関係リスト名</summary>
    public static string RelatedListName => nameof (Authors);

    /// <summary>ユニークキー群</summary>
    public override string [] UniqueKeys => [
        Title,
        Publisher,
        Series,
        PublishDate?.ToShortDateString () ?? "",
    ];

    /// <summary>ユニークキー群のSQL表現</summary>
    public static string UniqueKeysSql {
        get {
            var table = ExLibrisDataSet.GetSqlName (typeof (Book));
            var title = ExLibrisDataSet.GetSqlName (typeof (Book), "Title");
            var publisher = ExLibrisDataSet.GetSqlName (typeof (Book), "Publisher");
            var series = ExLibrisDataSet.GetSqlName (typeof (Book), "Series");
            var publishdate = ExLibrisDataSet.GetSqlName (typeof (Book), "PublishDate");
            return $"{table}.{title}<=>@Title and {table}.{publisher}<=>@Publisher and {table}.{series}<=>@Series and {table}.{publishdate}<=>@PublishDate";
        }
    }

    /// <summary>クローン</summary>
    public Book Clone ()
        => new Book {
            DataSet = DataSet,
            Id = Id,
            Title = Title,
            Description = Description,
            PublishDate = PublishDate,
            Publisher = Publisher,
            Series = Series,
            Price = Price,
            _relatedIds = _relatedIds,
            __relatedIds = default,
            __relatedItems = default,
        };

    /// <summary>値のコピー</summary>
    public Book CopyTo (Book arg) {
        if (arg is Book destination) {
            destination.DataSet = DataSet;
            destination.Id = Id;
            destination.Title = string.IsNullOrEmpty (Title) ? "" : new (Title);
            destination.Description = string.IsNullOrEmpty (Description) ? null : new (Description);
            destination.PublishDate = PublishDate;
            destination.Publisher = string.IsNullOrEmpty (Publisher) ? "" : new (Publisher);
            destination.Series = string.IsNullOrEmpty (Series) ? "" : new (Series);
            destination.Price = Price;
            destination._relatedIds = new (_relatedIds);
            destination.__relatedIds = default;
            destination.__relatedItems = default;
            return destination;
        }
        throw new ArgumentNullException (nameof (arg));
    }

    /// <summary>値の等価性</summary>
    public bool Equals (Book? other) => Equals ((object?) other);

    /// <summary>値の等価性</summary>
    public bool Equals (object? obj, bool includeRelation) => obj is Book other
        && Id == other.Id
        && Title == other.Title
        && Description == other.Description
        && PublishDate == other.PublishDate
        && Publisher == other.Publisher
        && Series == other.Series
        && Price == other.Price
        && (!includeRelation || RelatedIds.ContainsEquals (other.RelatedIds))
    ;

    /// <summary>値の等価性</summary>
    public override bool Equals (object? obj) => Equals (obj, true);

    /// <summary>ハッシュの等価性</summary>
    public override int GetHashCode () => HashCode.Combine (Id, Title, Description, PublishDate, Publisher, Series, Price, _relatedIds);

    /// <summary>文字列化</summary>
    public override string ToString () => $"{TableLabel} {Id}: {Title} \"{Description}\" {Publisher} {Series} {PublishDate?.ToShortDateString ()} {{{string.Join (",", Authors.ConvertAll (a => $"{a.Id}:{a.Name}"))}}}";
}
