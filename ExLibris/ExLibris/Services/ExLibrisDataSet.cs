using System.Reflection;
using Microsoft.AspNetCore.Components;
using MySqlConnector;
using PetaPoco;
using ExLibris.Data;
using static ExLibris.Services.ExLibrisDataSet;

namespace ExLibris.Services;

/// <summary></summary>
public sealed class ExLibrisDataSet {

    /// <summary>待機間隔</summary>
    public const int WaitInterval = 1000 / 60;

    /// <summary>ロードでエラーした場合の最大試行回数</summary>
    private const int MaxRetryCount = 10;

    /// <summary>ロードでエラーした場合のリトライ間隔</summary>
    private const int RetryInterval = 1000 / 30;

    /// <summary>PetaPocoをDI</summary>
    [Inject] private Database database { get; set; }

    /// <summary>コンストラクタ</summary>
    public ExLibrisDataSet (Database database) {
        this.database = database;
    }

    /// <summary>初期化</summary>
    public async Task InitializeAsync() {
        if (!IsInitialized) {
            await LoadAsync ();
            IsInitialized = true;
        }
    }

    /// <summary>初期化済み</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>(再)読み込み</summary>
    /// <remarks>既に読み込み中なら単に完了を待って戻る</remarks>
    public async Task LoadAsync (Database? database = null) {
        if (isLoading) {
            while (isLoading) {
                await Task.Delay (WaitInterval);
            }
            return;
        }
        isLoading = true;
        for (var i = 0; i < MaxRetryCount; i++) {
            var result = await GetPairAsync<Book, Author> (database);
            if (result.books.IsSuccess && result.authors.IsSuccess) {
                (Books, Authors) = (result.books.Value, result.authors.Value);
                isLoading = false;
                return;
            }
            await Task.Delay (RetryInterval);
        }
        throw new TimeoutException ("The maximum number of retries for LoadAsync was exceeded.");
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

    /// <summary>SQLで使用するテーブル名またはカラム名を得る</summary>
    /// <param name="type">クラス型</param>
    /// <param name="name">プロパティ名</param>
    /// <returns>テーブル名またはカラム名</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetSqlName (Type type, string? name = null) {
        if (!type.IsClass)
            throw new ArgumentOutOfRangeException (nameof (type));
        if (name == null) {
            return type.GetCustomAttribute<PetaPoco.TableNameAttribute> ()?.Value ?? type.Name;
        } else {
            return type.GetProperty (name)?.GetCustomAttribute<PetaPoco.ColumnAttribute> ()?.Name ?? name;
        }
    }

    /// <summary>一覧用並び指定SQL</summary>
    private string OrderSql (Type type, string table) {
        if (type == typeof (Book)) {
            return $"order by {GetSqlName (type)}.{GetSqlName (type, "PublishDate")} DESC";
        } else if (type == typeof (Author)) {
            return $"order by {GetSqlName (type)}.{GetSqlName (type, "Name")} ASC";
        }
        return string.Empty;
    }

    /// <summary>更新用カラムSQL</summary>
    /// <remarks>ColumnでありかつVirtualColumnでないプロパティだけを対象とする</remarks>
    private string GetSettingSql (Type type, bool withId = false) {
        var result = string.Empty;
        if (type.IsClass) {
            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public);
            if (properties != null) {
                result = string.Join (",", Array.ConvertAll (properties, property => {
                    var @virtual = property.GetCustomAttribute<VirtualColumnAttribute> ();
                    var attribute = property.GetCustomAttribute<ColumnAttribute> ();
                    return @virtual == null && attribute != null && (withId || (attribute.Name ?? property.Name) != "Id") ? $"{attribute.Name ?? property.Name}=@{property.Name}" : "";
                }).ToList ().FindAll (i => i != ""));
            }
        }
        return result;
    }

    /// <summary>追加用カラムSQL</summary>
    /// <remarks>ColumnでありかつVirtualColumnでないプロパティだけを対象とする</remarks>
    public string GetValuesSql (Type type, bool withId = false) {
        var result = string.Empty;
        if (type.IsClass) {
            var properties = type.GetProperties (BindingFlags.Instance | BindingFlags.Public);
            if (properties != null) {
                var columns = string.Join (",", Array.ConvertAll (properties, property => {
                    var @virtual = property.GetCustomAttribute<VirtualColumnAttribute> ();
                    var attribute = property.GetCustomAttribute<ColumnAttribute> ();
                    return @virtual == null && attribute != null && (withId || (attribute.Name ?? property.Name) != "Id") ? attribute.Name ?? property.Name : "";
                }).ToList ().FindAll (i => i != ""));
                var values = string.Join (",", Array.ConvertAll (properties, property => {
                    var @virtual = property.GetCustomAttribute<VirtualColumnAttribute> ();
                    var attribute = property.GetCustomAttribute<ColumnAttribute> ();
                    return @virtual == null && attribute != null && (withId || (attribute.Name ?? property.Name) != "Id") ? $"@{property.Name}" : "";
                }).ToList ().FindAll (i => i != ""));
                result = $"({columns}) values ({values})";
            }
        }
        return result;
    }

    /// <summary>一覧を取得</summary>
    private async Task<Result<List<T1>>> GetListAsync<T1, T2> (Database? database = null)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 :  ExLibrisBaseModel<T2, T1>, new() {
        var table = GetSqlName (typeof (T1));
        return await ProcessAndCommitAsync (async database => {
            return await database.FetchAsync<T1> (
                $@"select {table}.*, Group_concat({GetSqlName (typeof (T2))}Id) as _relatedIds
                from {table}
                left join AuthorBook on {table}.Id = AuthorBook.{table}Id
                group by {table}.Id
                {OrderSql (typeof (T1), table)};"
            );
        }, database);
    }

    /// <summary>一覧ペアをアトミックに取得</summary>
    public async Task<(Result<List<T1>> books, Result<List<T2>> authors)> GetPairAsync<T1, T2> (Database? database = null)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var result = await ProcessAndCommitAsync (async database => {
            var list1 = await GetListAsync<T1, T2> (database);
            var list2 = await GetListAsync<T2, T1> (database);
            return (list1, list2);
        }, database);
        return result.ValueOrThrow;
    }

    /// <summary>処理を実行しコミットする、例外またはエラーがあればロールバックする</summary>
    /// <typeparam name="T">返す値の型</typeparam>
    /// <param name="process">処理</param>
    /// <param name="database">PetaPocoインスタンス</param>
    /// <returns>成功またはエラーの状態と値のセット</returns>
    public async Task<Result<T>> ProcessAndCommitAsync<T> (Func<Database, Task<T>> process, Database? database = null) {
        var result = default (T)!;
        database ??= this.database;
        await database.BeginTransactionAsync ();
        try {
            result = await process (database);
            await database.CompleteTransactionAsync ();
            return new (Status.Success, result);
        }
        catch (Exception ex) when (ex.IsDeadLock ()) {
            await database.AbortTransactionAsync ();
            // デッドロックならエスカレート
            throw;
        }
        catch (Exception ex) when (ex.TryGetStatus (out var status)) {
            // エラー扱いの例外
            await database.AbortTransactionAsync ();
            System.Diagnostics.Debug.WriteLine (ex);
            if (status == Status.CommandTimeout) {
                // タイムアウトならエスカレート
                throw;
            }
            return new (status, result);
        }
        catch (Exception) {
            await database.AbortTransactionAsync ();
            throw;
        }
    }

    /// <summary>結果の状態の名前</summary>
    public static readonly Dictionary<Status, string> StatusName = new () {
        { Status.Success, "成功" },
        { Status.Unknown, "不詳の失敗" },
        { Status.MissingEntry, "エントリの消失" },
        { Status.DuplicateEntry, "エントリの重複" },
        { Status.CommandTimeout, "タイムアウト" },
        { Status.VersionMismatch, "バージョンの不整合" },
        { Status.ForeignKeyConstraintFails, "外部キー制約の違反" },
        { Status.DeadlockFound, "デッドロック" },
    };

    /// <summary>例外メッセージからエラーへの変換</summary>
    internal static readonly Dictionary<(Type type, string message), Status> ExceptionToErrorDictionary = new () {
        { (typeof (MyDataSetException), "Missing entry"), Status.MissingEntry },
        { (typeof (MyDataSetException), "Duplicate entry"), Status.DuplicateEntry },
        { (typeof (MyDataSetException), "The Command Timeout expired"), Status.CommandTimeout },
        { (typeof (MyDataSetException), "Version mismatch"), Status.VersionMismatch },
        { (typeof (MyDataSetException), "Cannot add or update a child row: a foreign key constraint fails"), Status.ForeignKeyConstraintFails },
        { (typeof (MyDataSetException), "Deadlock found"), Status.DeadlockFound },
        { (typeof (MySqlException), "Duplicate entry"), Status.DuplicateEntry },
        { (typeof (MySqlException), "The Command Timeout expired"), Status.CommandTimeout },
        { (typeof (MySqlException), "Version mismatch"), Status.VersionMismatch },
        { (typeof (MySqlException), "Cannot add or update a child row: a foreign key constraint fails"), Status.ForeignKeyConstraintFails },
        { (typeof (MySqlException), "Deadlock found"), Status.DeadlockFound },
    };

}

public static class ExLibrisDataSetHelper {
    // 例外がエラーか判定して該当するエラー状態を出力する
    public static bool TryGetStatus (this Exception ex, out Status status) {
        foreach (var pair in ExceptionToErrorDictionary) {
            if (ex.GetType () == pair.Key.type && ex.Message.StartsWith (pair.Key.message, StringComparison.CurrentCultureIgnoreCase)) {
                status = pair.Value;
                return true;
            }
        }
        status = Status.Unknown;
        return false;
    }
    /// <summary>ステータス名</summary>
    public static string GetName (this Status status) => StatusName [status];
    /// <summary>ステータスは致命的である</summary>
    public static bool IsFatal (this Status status) => status == Status.DeadlockFound || status == Status.CommandTimeout;
    /// <summary>例外はデッドロックである</summary>
    public static bool IsDeadLock (this Exception ex) => ex is MySqlException && ex.Message.StartsWith ("Deadlock found");
    /// <summary>例外はタイムアウトである</summary>
    public static bool IsTimeout (this Exception ex) => ex is MySqlException && ex.Message.StartsWith ("The Command Timeout expired");
}

/// <summary>結果の状態</summary>
public enum Status {
    /// <summary>成功</summary>
    Success = default,
    /// <summary>不詳の失敗</summary>
    Unknown,
    /// <summary>エントリの消失</summary>
    MissingEntry,
    /// <summary>エントリの重複</summary>
    DuplicateEntry,
    /// <summary>タイムアウト</summary>
    CommandTimeout,
    /// <summary>バージョンの不整合</summary>
    VersionMismatch,
    /// <summary>外部キー制約の違反</summary>
    ForeignKeyConstraintFails,
    /// <summary>デッドロック</summary>
    DeadlockFound,
}

/// <summary>結果の状態と値</summary>
public class Result<T> {
    public Status Status { get; internal set; }
    public T Value { get; init; } = default!;
    public string StatusName => ExLibrisDataSet.StatusName [Status];
    internal Result () { }
    internal Result (Status status, T value) { Status = status; Value = value; }
    /// <summary>成功である</summary>
    public bool IsSuccess => Status == Status.Success;
    /// <summary>失敗である</summary>
    public bool IsFailure => Status != Status.Success;
    /// <summary>致命的である</summary>
    public bool IsFatal => Status == Status.CommandTimeout || Status == Status.DeadlockFound;
    /// <summary>成功なら値、失敗なら例外</summary>
    public T ValueOrThrow => IsSuccess ? Value : throw new NotSupportedException (Status.ToString ());
    /// <summary>逆引き</summary>
    public Exception Exception {
        get {
            if (ExceptionToErrorDictionary.ContainsValue (Status)) {
                return new MyDataSetException (ExceptionToErrorDictionary.First (p => p.Value == Status).Key.message);
            } else {
                return new Exception ("Unknown exception");
            }
        }
    }
}

/// <summary>内部で使用する例外</summary>
[Serializable]
internal class MyDataSetException : Exception {
    internal MyDataSetException () : base () { }
    internal MyDataSetException (string message) : base (message) { }
    internal MyDataSetException (string message, Exception innerException) : base (message, innerException) { }
}

/// <summary>仮想カラム</summary>
/// <remarks>計算列から(PetaPocoにマッピングさせて)取り込むが、フィールドが実在しないので書き出さない</remarks>
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class VirtualColumnAttribute : Attribute {
    public VirtualColumnAttribute () { }
}
