using Microsoft.AspNetCore.Components;
using PetaPoco;
using ExLibris.Data;

namespace ExLibris.Services;

/// <summary></summary>
public sealed class ExLibrisDataSet {

    /// <summary>待機間隔</summary>
    public const int WaitInterval = 1000 / 60;

    /// <summary>PetaPocoをDI</summary>
    [Inject] private Database database { get; set; }

    /// <summary>コンストラクタ</summary>
    public ExLibrisDataSet (Database database) {
        this.database = database;
    }

    /// <summary>初期化</summary>
    public async Task InitializeAsync() {
        if (!Initialized) {
            await LoadAsync ();
            Initialized = true;
        }
    }

    /// <summary>初期化済み</summary>
    public bool Initialized { get; private set; }

    /// <summary>(再)読み込み</summary>
    /// <remarks>既に読み込み中なら単に完了を待って戻る</remarks>
    public async Task LoadAsync() {
        if (isLoading) {
            while (isLoading) {
                await Task.Delay (WaitInterval);
            }
            return;
        }
        isLoading = true;
        await database.BeginTransactionAsync ();
        try {
            Books = await database.FetchAsync<Book> (
                @"select Books.*, Group_concat(AuthorsId) as _relatedIds
                from Books
                left join AuthorBook on Books.Id = AuthorBook.BooksId
                group by Books.Id;");
            Authors = await database.FetchAsync<Author> (
                @"select Authors.*, Group_concat(BooksId) as _relatedIds
                from Authors
                left join AuthorBook on Authors.Id = AuthorBook.AuthorsId
                group by Authors.Id;");
            await database.CompleteTransactionAsync ();
        }
        catch {
            await database.AbortTransactionAsync ();
            throw;
        }
        isLoading = false;
    }
    private bool isLoading;

    /// <summary>指定クラスのモデルインスタンスを取得</summary>
    public List<T> GetAll<T>() where T : class => (
            typeof(T) == typeof(Author) ? Authors as List<T> :
            typeof(T) == typeof(Book) ? Books as List<T> : null
        ) ?? new();

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Author> Authors {
        get => _authors;
        set => (_authors = value).ForEach(author => author.DataSet = this);
    }
    private List<Author> _authors = new();

    /// <summary>ロード済みのモデルインスタンス</summary>
    public List<Book> Books {
        get => _books;
        set => (_books = value).ForEach(book => book.DataSet = this);
    }
    private List<Book> _books = new();
}
