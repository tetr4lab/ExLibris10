﻿using ExLibris.Services;
using PetaPoco;

namespace ExLibris.Data;

[TableName ("Authors")]
public class Author : ExLibrisBaseModel<Author, Book>, IExLibrisModel {
    [Column] public string Name { get; set; } = "";
    [Column] public string AdditionalName { get; set; } = "";
    [Column] public string? Description { get; set; }

    /// <summary>著書一覧</summary>
    public List<Book> Books => RelatedItems;

    /// <summary>テーブル名</summary>
    public static string TableLabel => "著者";

    /// <summary>単位</summary>
    public static string Unit => "名";

    /// <summary>列の名前</summary>
    public static Dictionary<string, string> Label { get; } = new Dictionary<string, string> {
        { nameof (Id), "ID" },
        { nameof (Name), "著者名" },
        { nameof (AdditionalName), "補助名" },
        { nameof (Description), "説明" },
        { nameof (Books), "著書" },
    };

    /// <summary>行の名前</summary>
    public override string? RowLabel {
        get => Name;
        set => Name = value ?? "";
    }

    /// <summary>検索対象</summary>
    public override string? [] SearchTargets => [Id.ToString (), Name, AdditionalName, Description, string.Join (",", _relatedIds),];

    /// <summary>関係リスト名</summary>
    public static string RelatedListName => nameof (Books);

    /// <summary>ユニークキー群</summary>
    public override string [] UniqueKeys => [Name, AdditionalName,];

    /// <summary>ユニークキー群のSQL表現</summary>
    public static string UniqueKeysSql {
        get {
            var table = ExLibrisDataSet.GetSqlName (typeof (Author));
            var name = ExLibrisDataSet.GetSqlName (typeof (Author), "Name");
            var additionalName = ExLibrisDataSet.GetSqlName (typeof (Author), "AdditionalName");
            return $"{table}.{name}<=>@Name and {table}.{additionalName}<=>@AdditionalName";
        }
    }

    /// <summary>クローン</summary>
    public Author Clone ()
        => new Author {
            DataSet = DataSet,
            Id = Id,
            Name = Name,
            AdditionalName = AdditionalName,
            Description = Description,
            _relatedIds = _relatedIds,
            __relatedIds = default,
            __relatedItems = default,
        };

    /// <summary>値のコピー</summary>
    public Author CopyTo (Author arg) {
        if (arg is Author destination) {
            destination.DataSet = DataSet;
            destination.Id = Id;
            destination.Name = string.IsNullOrEmpty (Name) ? "" : new (Name);
            destination.AdditionalName = string.IsNullOrEmpty (AdditionalName) ? "" : new (AdditionalName);
            destination.Description = string.IsNullOrEmpty (Description) ? null : new (Description);
            destination._relatedIds = new (_relatedIds);
            destination.__relatedIds = default;
            destination.__relatedItems = default;
            return destination;
        }
        throw new ArgumentNullException (nameof (arg));
    }

    /// <summary>値の等価性</summary>
    public bool Equals (Author? other) => Equals ((object?) other);

    /// <summary>値の等価性</summary>
    public bool Equals (object? obj, bool includeRelation) => obj is Author other
        && Id == other.Id
        && Name == other.Name
        && AdditionalName == other.AdditionalName
        && Description == other.Description
        && (!includeRelation || RelatedIds.ContainsEquals (other.RelatedIds))
    ;

    /// <summary>値の等価性</summary>
    public override bool Equals (object? obj) => Equals (obj, true);

    /// <summary>ハッシュの等価性</summary>
    public override int GetHashCode () => HashCode.Combine (Id, Name, AdditionalName, Description, _relatedIds);

    /// <summary>文字列化</summary>
    public override string ToString () => $"{TableLabel} {Id}: {Name}{(string.IsNullOrEmpty (AdditionalName) ? "" : $"-{AdditionalName}")} \"{Description}\" {{{string.Join (",", Books.ConvertAll (b => $"{b.Id}:{b.Title}"))}}}";

}
