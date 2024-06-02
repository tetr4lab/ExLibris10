using Microsoft.AspNetCore.Components;
using PetaPoco;
using ExLibris.Data;

namespace ExLibris.Services;

public sealed class ExLibrisDataSet
{
    [Inject] private Database database { get; set; }
    public ExLibrisDataSet(Database database)
    {
        this.database = database;
    }
    public async Task Initialize()
    {
        await LoadAsync();
    }
    public bool Initialized { get; private set; }
    public async Task LoadAsync()
    {
        if (isLoading) { return; }
        isLoading = true;
        Initialized = false;
        Books = await database.FetchAsync<Book>(
            @"select Books.*, Group_concat(AuthorsId) as _relatedIds
                from Books
                left join AuthorBook on Books.Id = AuthorBook.BooksId
                group by Books.Id;");
        Authors = await database.FetchAsync<Author>(
            @"select Authors.*, Group_concat(BooksId) as _relatedIds
                from Authors
                left join AuthorBook on Authors.Id = AuthorBook.AuthorsId
                group by Authors.Id;");
        isLoading = false;
        Initialized = true;
    }
    private bool isLoading;
    public List<T> GetAll<T>() where T : class => (
            typeof(T) == typeof(Author) ? Authors as List<T> :
            typeof(T) == typeof(Book) ? Books as List<T> : null
        ) ?? new();
    public List<Author> Authors
    {
        get => _authors;
        set => (_authors = value).ForEach(author => author.DataSet = this);
    }
    private List<Author> _authors = new();
    public List<Book> Books
    {
        get => _books;
        set => (_books = value).ForEach(book => book.DataSet = this);
    }
    private List<Book> _books = new();
}
