using System.Reflection;
using MySqlConnector;
using PetaPoco;
using ExLibris.Data;

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
    private Database database { get; set; }

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

    /// <summary>初期化が済み、ロード中でない</summary>
    public bool IsReady => IsInitialized && !isLoading;

    /// <summary>(再)読み込み</summary>
    /// <remarks>既に読み込み中なら単に完了を待って戻る、再読み込み中でも以前のデータが有効</remarks>
    public async Task LoadAsync () {
        if (isLoading) {
            while (isLoading) {
                await Task.Delay (WaitInterval);
            }
            return;
        }
        isLoading = true;
        for (var i = 0; i < MaxRetryCount; i++) {
            var result = await GetPairAsync<Book, Author> ();
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

    /// <summary>有効性の検証</summary>
    public bool Valid
        => IsReady
        && _authors != null && !_authors.Exists (i => i.DataSet != this || i.Id <= 0)
        && _books != null && !_books.Exists (i => i.DataSet != this || i.Id <= 0);

    /// <summary>読み込み済み総リストから対象アイテムを得る</summary>
    public T1? GetItemById<T1, T2> (long id)
        where T1 : ExLibrisBaseModel<T1, T2>, new ()
        where T2 : ExLibrisBaseModel<T2, T1>, new()
        => GetAll<T1> ()?.Find (i => i.Id == id);

    /// <summary>読み込み済み総リストから対象アイテムを得る</summary>
    public T1? GetItemById<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new()
        => GetAll<T1> ()?.Find (i => i.Id == item.Id);

    /// <summary>読み込み済み総リストから対象と同名のアイテムを得る</summary>
    public T1? GetItemByName<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new()
        => GetAll<T1> ()?.Find (i => i.UniqueKey.ContainsEquals (item.UniqueKey));

    /// <summary>読み込み済み総リストから対象と同名のアイテムを得る</summary>
    public T1? GetItemByName<T1, T2> (string name)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new()
        => GetAll<T1> ()?.Find (i => i.RowLabel == name);

    /// <summary>SQLで使用するテーブル名またはカラム名を得る</summary>
    /// <param name="name">プロパティ名</param>
    /// <returns>テーブル名またはカラム名</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string GetSqlName<T> (string? name = null) where T : class {
        var type = typeof (T);
        if (name == null) {
            return type.GetCustomAttribute<PetaPoco.TableNameAttribute> ()?.Value ?? type.Name;
        } else {
            return type.GetProperty (name)?.GetCustomAttribute<PetaPoco.ColumnAttribute> ()?.Name ?? name;
        }
    }

    /// <summary>一覧用並び指定SQL</summary>
    private string GetOrderSql<T> () where T : class, IExLibrisModel => T.OrderSql;

    /// <summary>更新用カラム&値SQL</summary>
    /// <remarks>ColumnでありかつVirtualColumnでないプロパティだけを対象とする</remarks>
    private string GetSettingSql<T> (bool withId = false) where T : class {
        var result = string.Empty;
        var properties = typeof (T).GetProperties (BindingFlags.Instance | BindingFlags.Public);
        if (properties != null) {
            result = string.Join (",", Array.ConvertAll (properties, property => {
                var @virtual = property.GetCustomAttribute<VirtualColumnAttribute> ();
                var attribute = property.GetCustomAttribute<ColumnAttribute> ();
                return @virtual == null && attribute != null && (withId || (attribute.Name ?? property.Name) != "Id")
                    ? $"{attribute.Name ?? property.Name}=@{property.Name}"
                    : "";
            }).ToList ().FindAll (i => i != ""));
        }
        return result;
    }

    /// <summary>追加用値SQL</summary>
    /// <remarks>ColumnでありかつVirtualColumnでないプロパティだけを対象とする</remarks>
    public string GetValuesSql<T> (int index = -1, bool withId = false) where T : class {
        var result = string.Empty;
        var properties = typeof (T).GetProperties (BindingFlags.Instance | BindingFlags.Public);
        if (properties != null) {
            result = string.Join (",", Array.ConvertAll (properties, property => {
                var @virtual = property.GetCustomAttribute<VirtualColumnAttribute> ();
                var attribute = property.GetCustomAttribute<ColumnAttribute> ();
                return @virtual == null && attribute != null && (withId || (attribute.Name ?? property.Name) != "Id")
                    ? $"@{property.Name}{(index >= 0 ? $"_{index}" : "")}"
                    : "";
            }).ToList ().FindAll (i => i != ""));
        }
        return result;
    }

    /// <summary>追加用カラムSQL</summary>
    /// <remarks>ColumnでありかつVirtualColumnでないプロパティだけを対象とする</remarks>
    public string GetColumnsSql<T> (bool withId = false) where T : class {
        var result = string.Empty;
        var properties = typeof (T).GetProperties (BindingFlags.Instance | BindingFlags.Public);
        if (properties != null) {
            result = string.Join (",", Array.ConvertAll (properties, property => {
                var @virtual = property.GetCustomAttribute<VirtualColumnAttribute> ();
                var attribute = property.GetCustomAttribute<ColumnAttribute> ();
                return @virtual == null && attribute != null && (withId || (attribute.Name ?? property.Name) != "Id")
                    ? attribute.Name ?? property.Name
                    : "";
            }).ToList ().FindAll (i => i != ""));
        }
        return result;
    }

    /// <summary>アイテムリストから辞書型パラメータを生成する</summary>
    private Dictionary<string, object?> GetParamDictionary<T1, T2> (IEnumerable<T1> values)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var parameters = new Dictionary<string, object?> ();
        var prpperties = new List<PropertyInfo> ();
        foreach (var property in typeof (T1).GetProperties (BindingFlags.Instance | BindingFlags.Public) ?? []) {
            var attribute = property.GetCustomAttribute<ColumnAttribute> ();
            if (attribute != null && property.GetCustomAttribute<VirtualColumnAttribute> () == null
                && (attribute.Name ?? property.Name) != "Id") {
                prpperties.Add (property);
            }
        }
        var i = 0;
        foreach (var value in values) {
            foreach (var property in prpperties) {
                parameters.Add ($"{property.Name}_{i}", property.GetValue (value));
            }
            i++;
        }
        return parameters;
    }

    /// <summary>処理を実行しコミットする、例外またはエラーがあればロールバックする</summary>
    /// <typeparam name="T">返す値の型</typeparam>
    /// <param name="process">処理</param>
    /// <returns>成功またはエラーの状態と値のセット</returns>
    public async Task<Result<T>> ProcessAndCommitAsync<T> (Func<Task<T>> process) {
        var result = default (T)!;
        await database.BeginTransactionAsync ();
        try {
            result = await process ();
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

    /// <summary>一覧を取得</summary>
    private async Task<Result<List<T1>>> GetListAsync<T1, T2> ()
        where T1 : ExLibrisBaseModel<T1, T2>, IExLibrisModel, new()
        where T2 :  ExLibrisBaseModel<T2, T1>, IExLibrisModel, new() {
        var table = GetSqlName<T1> ();
        return await ProcessAndCommitAsync (async () => {
            return await database.FetchAsync<T1> (
                $@"select {table}.*, group_concat({GetSqlName<T2> ()}Id) as _relatedIds
                from {table}
                left join AuthorBook on {table}.Id = AuthorBook.{table}Id
                group by {table}.Id
                order by {GetOrderSql<T1> ()};"
            );
        });
    }

    /// <summary>一覧ペアをアトミックに取得</summary>
    public async Task<(Result<List<T1>> books, Result<List<T2>> authors)> GetPairAsync<T1, T2> ()
        where T1 : ExLibrisBaseModel<T1, T2>, IExLibrisModel, new()
        where T2 : ExLibrisBaseModel<T2, T1>, IExLibrisModel, new() {
        var result = await ProcessAndCommitAsync (async () => {
            var list1 = await GetListAsync<T1, T2> ();
            var list2 = await GetListAsync<T2, T1> ();
            return (list1, list2);
        });
        return result.ValueOrThrow;
    }

    /// <summary>単一アイテムを取得 (Idで特定) 【注意】総リストとは別オブジェクトになる</summary>
    public async Task<Result<T1?>> GetItemByIdAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var table = GetSqlName<T1> ();
        return await ProcessAndCommitAsync (async () => (await database.FetchAsync<T1?> (
            $@"select {table}.*, Group_concat({GetSqlName<T2> ()}Id) as _relatedIds
            from {table}
            left join AuthorBook on {table}.Id = AuthorBook.{table}Id
            where {table}.Id = @Id
            group by {table}.Id;",
            item
        )).Single ());
    }

    /// <summary>単一アイテムを取得 (ユニークキーで特定) 【注意】総リストとは別オブジェクトになる</summary>
    public async Task<Result<T1?>> GetItemByNameAsync<T1, T2> (T1 target)
        where T1 : ExLibrisBaseModel<T1, T2>, IExLibrisModel, new()
        where T2 : ExLibrisBaseModel<T2, T1>, IExLibrisModel, new() {
        var table = GetSqlName<T1> ();
        return await ProcessAndCommitAsync (async () => {
            var result = await database.FetchAsync<T1?> (
                $@"select {table}.*, Group_concat({GetSqlName<T2> ()}Id) as _relatedIds
                from {table}
                left join AuthorBook on {table}.Id = AuthorBook.{table}Id
                where {T1.UniqueKeysSql}
                group by {table}.Id;",
                target
            );
            if (result.Count == 1) {
                return result [0];
            } else if (result.Count <= 0) {
                throw Status.MissingEntry.GetException ();
            } else {
                throw Status.DuplicateEntry.GetException ();
            }
        });
    }

    /// <summary>アイテムの更新サブ処理 トランザクション内専用</summary>
    private async Task<int> UpdateItemAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var table = GetSqlName<T1> ();
        return await database.ExecuteAsync (
            @$"update {table} set {GetSettingSql<T1> ()} where {table}.Id = @Id",
            item
        );
    }

    /// <summary>関係リンク追加サブ処理 トランザクション内専用</summary>
    private async Task<int> AddRelationsAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        if (item.RelatedIds.Count <= 0) { return 0; }
        var valuesSqls = new List<string> ();
        var parameters = new Dictionary<string, object> { { "Id1", item.Id } };
        for (int i = 0; i < item.RelatedIds.Count; i++) {
            valuesSqls.Add ($"(@Id1, @Id2_{i})");
            parameters.Add ($"Id2_{i}", item.RelatedIds [i]);
        }
        return await database.ExecuteAsync (
            $"insert into AuthorBook ({GetSqlName<T1> ()}Id, {GetSqlName<T2> ()}Id) values {string.Join(",", valuesSqls)};",
            parameters
        );
    }

    /// <summary>関係リンク削除サブ処理 トランザクション内専用</summary>
    private async Task<int> RemoveRelationsAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        return await database.ExecuteAsync (
            $"delete from AuthorBook where {GetSqlName<T1> ()}Id = @Id",
            item
        );
    }

    /// <summary>アイテムの更新</summary>
    public async Task<Result<int>> UpdateAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var result = await ProcessAndCommitAsync (async () => {
            await RemoveRelationsAsync<T1, T2> (item);
            await AddRelationsAsync<T1, T2> (item);
            item.Version++;
            return await UpdateItemAsync<T1, T2> (item);
        });
        if (result.IsSuccess && result.Value <= 0) {
            result.Status = Status.MissingEntry;
        }
        return result;
    }

    /// <summary>単一アイテムの追加</summary>
    public async Task<Result<T1>> AddAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var result = await ProcessAndCommitAsync (async () => {
            item.Id = await database.ExecuteScalarAsync<int> (
                @$"insert into {GetSqlName<T1> ()} ({GetColumnsSql<T1> ()}) values ({GetValuesSql<T1> ()});
                select LAST_INSERT_ID ();",
                item
            );
            if (item.Id > 0) {
                await AddRelationsAsync<T1, T2> (item);
                return 1;
            }
            return 0;
        });
        if (result.IsSuccess && result.Value <= 0) {
            result.Status = Status.MissingEntry;
        }
        if (result.IsFailure) {
            item.Id = default;
        }
        // ロード済みに追加
        GetAll<T1> ().Add (item);
        return new (result.Status, item);
    }

    /// <summary>一括アイテムの追加 (個別エラーを許容)</summary>
    public async Task<Result<int>> AddAsync<T1, T2> (IEnumerable<T1> items)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var results = new List<Result<T1>> ();
        foreach (var item in items) {
            var result = await AddAsync<T1, T2> (item);
            results.Add (result);
            if (result.IsFatal) {
                throw result.Exception; // タイムアウトしたら以降は中断
            }
        }
        return new (results.FirstFailedState (), results.FindAll (r => r.IsSuccess).Count);
    }

    /// <summary>変数値を得るためのモデル</summary>
    private class Variable {
        public string? Variable_name { get; set; }
        public int Value { get; set; }
    }

    /// <summary>テーブルの次の自動更新値を得る</summary>
    /// <remarks>MySQL/MariaDBに依存</remarks>
    public async Task<long> GetAutoIncremantValueAsync<T> () where T : class {
        // 開始Idを取得
        var Id = 0L;
        try {
            // 待避と設定 (SQLに勝手に'SELECT'を挿入しない)
            var enableAutoSelectBackup = database.EnableAutoSelect;
            database.EnableAutoSelect = false;
            try {
                try {
                    // 設定 (情報テーブルの即時更新を設定)
                    await database.ExecuteAsync ("set session information_schema_stats_expiry=1;");
                }
                catch (MySqlException ex) when (ex.Message.StartsWith ("Unknown system variable")) {
                    // MariaDBはこの変数をサポートしていない
                    System.Diagnostics.Debug.WriteLine ($"Server not supported 'information_schema_stats_expiry'\n{ex}");
                }
                // 次の自動更新値の取得
                Id = await database.SingleAsync<long> (
                    $"select AUTO_INCREMENT from information_schema.tables where TABLE_NAME='{GetSqlName<T> ()}';"
                );
            }
            finally {
                // 設定の復旧
                database.EnableAutoSelect = enableAutoSelectBackup;
            }
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine ($"Get auto_increment number\n{ex}");
        }
        if (Id <= 0) {
            // 開始Idの取得に失敗
            throw new NotSupportedException ("Failed to get auto_increment value.");
        }
        return Id;
    }

    /// <summary>一括アイテムの追加</summary>
    public async Task<Result<int>> AddRangeAsync<T1, T2> (IEnumerable<T1> items)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        if (items.Count () <= 0) { return new Result<int> (Status.MissingEntry, 0); }
        var result = await ProcessAndCommitAsync (async () => {
            // 開始Idを取得
            var Id = await GetAutoIncremantValueAsync<T1> ();
            if (Id <= 0) {
                // 開始Idの取得に失敗
                throw new NotSupportedException ("Failed to get auto_increment value.");
            }
            // 主アイテムを挿入
            var valuesSqls = new List<string> ();
            for (int i = 0; i < items.Count (); i++) {
                valuesSqls.Add ($"({GetValuesSql<T1> (i)})");
            }
            var result = await database.ExecuteAsync (
                $"insert into {GetSqlName<T1> ()} ({GetColumnsSql<T1> ()}) values {string.Join (",", valuesSqls)};",
                GetParamDictionary<T1, T2> (items)
            );
            // 関係アイテムを挿入
            valuesSqls = new List<string> ();
            var parameters = new Dictionary<string, object> ();
            foreach (var item in items) {
                item.Id = Id++;
                parameters.Add ($"Id1_{item.Id}", item.Id);
                for (int i = 0; i < item.RelatedIds.Count; i++) {
                    valuesSqls.Add ($"(@Id1_{item.Id}, @Id2_{item.Id}_{i})");
                    parameters.Add ($"Id2_{item.Id}_{i}", item.RelatedIds [i]);
                }
            }
            if (valuesSqls.Count > 0) {
                var count = await database.ExecuteAsync (
                    $"insert into AuthorBook ({GetSqlName<T1> ()}Id, {GetSqlName<T2> ()}Id) values {string.Join (",", valuesSqls)};",
                    parameters
                );
                if (count < valuesSqls.Count) {
                    System.Diagnostics.Debug.WriteLine ($"AddRangeAsync: ERROR: insert relations ({count}/{valuesSqls.Count})");
                }
            }
            return result;
        });
        // ロード済みに追加
        GetAll<T1> ().AddRange (items);
        return result;
    }

    /// <summary>単一アイテムの削除</summary>
    public async Task<Result<int>> RemoveAsync<T1, T2> (T1 item)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var result = await ProcessAndCommitAsync (async () => {
            var original = await GetItemByIdAsync<T1, T2> (item);
            if (original.IsSuccess && original.Value != null) {
                if (item.Version == original.Value.Version) {
                    // 関係先は制約によって自動的に削除される
                    return await database.ExecuteAsync (
                        $"delete from {GetSqlName<T1> ()} where Id = @Id",
                        item
                    );
                } else {
                    throw new MyDataSetException ($"Version mismatch between {item.Version} and {original.Value.Version}");
                }
            }
            return 0;
        });
        if (result.IsSuccess && result.Value <= 0) {
            result.Status = Status.MissingEntry;
        }
        // ロード済みから除去
        GetAll<T1> ().Remove (item);
        return result;
    }

    /// <summary>バージョン照合を行うためのモデル</summary>
    private class IdVersion {
        public int Id { get; set; }
        public int Version { get; set; }
    }

    /// <summary>一括アイテムの削除</summary>
    public async Task<Result<int>> RemoveRangeAsync<T1, T2> (IEnumerable<T1> items)
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var table = GetSqlName<T1> ();
        var status = Status.Unknown;
        var result = await ProcessAndCommitAsync (async () => {
            // 要求
            var list = items.ToList ();
            // 既存
            var entries = await database.FetchAsync<IdVersion> (
                $"select * from {table} where Id in (@Ids)",
                new { Ids = list.ConvertAll (i => i.Id) });
            // 一致
            var targets = entries.FindAll (i => list.Exists (j => j.Id == i.Id && j.Version == i.Version)).ConvertAll (i => i.Id);
            // 実行と結果
            status = targets.Count < entries.Count () ? Status.VersionMismatch : (entries.Count () < list.Count ? Status.MissingEntry : Status.Success);
            return await database.ExecuteAsync (
                $"delete from {table} where Id in (@Ids)",
                new { Ids = targets }
            );
        });
        // ロード済みから除去
        var allItems = GetAll<T1> ();
        foreach (var item in items) {
            allItems.Remove (item);
        }
        return new (status, result.Value);
    }

    /// <summary>テーブルが空であればオートインクリメントを初期化する</summary>
    public async Task<Result<int>> ResetAutoIncrementAsync<T1, T2> ()
        where T1 : ExLibrisBaseModel<T1, T2>, new()
        where T2 : ExLibrisBaseModel<T2, T1>, new() {
        var table1 = GetSqlName<T1> ();
        return await ProcessAndCommitAsync (async () => {
            var result = await database.ExecuteScalarAsync<int> (
                @$"set @@empty := (select count(*) = 0 from {table1});
                set @@sql := if(@@empty, 'alter table {table1} auto_increment = 1', 'select ""');
                prepare st from @@sql;
                execute st;
                deallocate prepare st;
                select @@empty;"
            );
            return result;
        }); // `ALTER`を使用するので`SAVEPOINT`は使用不可
    }

}

public static class ExceptionToErrorHelper {
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
        { (typeof (MySqlException), "Data too long for column"), Status.DataTooLong },
    };
    /// <summary>例外がエラーか判定して該当するエラー状態を出力する</summary>
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
    /// <summary>例外はデッドロックである</summary>
    public static bool IsDeadLock (this Exception ex) => ex is MySqlException && ex.Message.StartsWith ("Deadlock found");
    /// <summary>逆引き</summary>
    public static Exception GetException (this Status status) {
        if (ExceptionToErrorDictionary.ContainsValue (status)) {
            return new MyDataSetException (ExceptionToErrorDictionary.First (p => p.Value == status).Key.message);
        }
        return new Exception ("Unknown exception");
    }
}

public static class StatusHelper {
    /// <summary>結果の状態の名前</summary>
    private static readonly Dictionary<Status, string> StatusNameDictionary;
    /// <summary>コンストラクタ</summary>
    static StatusHelper () {
        StatusNameDictionary = new () {
            { Status.Success, "成功" },
            { Status.Unknown, "不詳の失敗" },
            { Status.MissingEntry, "エントリの消失" },
            { Status.DuplicateEntry, "エントリの重複" },
            { Status.CommandTimeout, "タイムアウト" },
            { Status.VersionMismatch, "バージョンの不整合" },
            { Status.ForeignKeyConstraintFails, "外部キー制約の違反" },
            { Status.DeadlockFound, "デッドロック" },
            { Status.DataTooLong, "サイズ超過" },
        };
    }
    /// <summary>結果の状態の名前</summary>
    public static string GetName (this Status status)
        => StatusNameDictionary .ContainsKey (status)
        ? StatusNameDictionary [status]
        : throw new ArgumentOutOfRangeException ($"Invalid status value {status}.");
    /// <summary>結果の一覧から最初に見つかった失敗状態を返す、失敗がなければ成功を返す</summary>
    public static Status FirstFailedState<T> (this List<Result<T>> results)
        => results.Find (r => r.IsFatal)?.Status ?? results.Find (r => r.IsFailure)?.Status ?? Status.Success;
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
    /// <summary>サイズ超過</summary>
    DataTooLong,
}

/// <summary>結果の状態と値</summary>
public class Result<T> {
    public Status Status { get; internal set; }
    public T Value { get; init; } = default!;
    public string StatusName => Status.GetName ();
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
    public Exception Exception => Status.GetException ();
    /// <summary>文字列化</summary>
    public override string ToString () => $"{{{Status}: {Value}}}";
}

/// <summary>内部で使用する例外</summary>
[Serializable]
internal class MyDataSetException : Exception {
    internal MyDataSetException () : base () { }
    internal MyDataSetException (string message) : base (message) { }
    internal MyDataSetException (string message, Exception innerException) : base (message, innerException) { }
}

/// <summary>仮想カラム属性</summary>
/// <remarks>計算列から(PetaPocoにマッピングさせて)取り込むが、フィールドが実在しないので書き出さない</remarks>
[AttributeUsage (AttributeTargets.Property | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public class VirtualColumnAttribute : Attribute {
    public VirtualColumnAttribute () { }
}
