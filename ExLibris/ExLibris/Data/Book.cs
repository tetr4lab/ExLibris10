using PetaPoco;

namespace ExLibris.Data;

[TableName ("Books"), PrimaryKey ("Id"), ExplicitColumns]
public class Book : ExLibrisBaseModel<Book, Author> {
    [Column] public string Title { get; set; } = "";
    [Column] public string? Description { get; set; }
    [Column] public DateTime? PublishDate { get; set; }
    [Column] public string Publisher { get; set; } = "";
    [Column] public string Series { get; set; } = "";
    [Column] public decimal Price { get; set; }
    public List<Author> Authors => RelatedItems;
}
