using PetaPoco;

namespace ExLibris.Data;

[TableName ("Authos")]
public class Author : ExLibrisBaseModel<Author, Book> {
    [Column] public string Name { get; set; } = "";
    [Column] public string AdditionalName { get; set; } = "";
    [Column] public string? Description { get; set; }
    public List<Book> Books => RelatedItems;
}
