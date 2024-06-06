using System.Collections;
using System.Collections.Generic;
using Tetr4lab;

namespace ComicsData;

/// <summary>コミック</summary>
public class Comic {
    public int Id { get; set; }
    public string Title { get; set; }
    public List<string> Authors { get; set; }
    public DateTime PublishDate { get; set; }
    public string Publisher { get; set; }
    public string Series { get; set; }
    public int Price { get; set; }
    public Comic (string tsv) {
        var cols = tsv.Split (['\t']);
        Title = cols.Length > 0 ? cols [0] : "";
        Authors = cols.Length > 1 ? cols [1].Split (['/']).ToList ().ConvertAll (a => a.Trim ()).Distinct ().ToList () : new List<string> ();
        var date = (cols.Length > 2 ? cols [2] : "").Replace ("上", "01").Replace ("中", "14").Replace ("下", "28").Replace ("??", "01").Replace ("?", "01");
        if (date.EndsWith ('.') || date.EndsWith ('/') || date.EndsWith ('-')) {
            date += "1";
        }
        if (DateTime.TryParse (date, out var dateTime)) {
            PublishDate = dateTime;
        }
        Publisher = cols.Length > 3 ? cols [3] : "";
        Series = cols.Length > 4 ? cols [4] : "";
        if (cols.Length > 5 && int.TryParse (cols [5], out var price)) { Price = price; }
        if (cols.Length > 6 && int.TryParse (cols [6], out var id)) { Id = id; }
    }
    public override string ToString () => $"{PublishDate.ToShortDateString ()} '{Title}' by {string.Join ("/", Authors)} // {Publisher} ({Series}) ¥{Price:#,0} :{Id}";
}

/// <summary>コミックデータ</summary>
public sealed class ComicsData : IDisposable, IEnumerable {

    private HttpClient httpClient;

    /// <summary>最も古いデータ</summary>
    public static readonly DateTime MostOldPublishDate = new DateTime (1987, 9, 1);

    /// <summary>日時</summary>
    public DateTime Date { get; init; } = DateTime.MinValue;

    /// <summary>生データ</summary>
    private string? rawData { get; set; }

    /// <summary>データのURL</summary>
    public static string TsvUri (DateTime date) => $"https://complex.matrix.jp/comics/tsv/c{date.Year:D4}{date.Month:D2}.csv";

    /// <summary>データのURL</summary>
    private string tsvUri => TsvUri (Date);

    /// <summary>取得済</summary>
    public bool IsLoaded => !isLoading && rawData != null;

    /// <summary>取得中</summary>
    private bool isLoading { get; set; }

    /// <summary>コミックス</summary>
    private IList<Comic> Comics { get; set; } = new List<Comic> ();

    /// <summary>反復子</summary>
    public IEnumerator GetEnumerator () => Comics.GetEnumerator ();

    /// <summary>リストを得る</summary>
    public List<Comic> ToList () => Comics.ToList ();

    /// <summary>行数</summary>
    public int Count => Comics.Count;

    /// <summary>コンストラクタ</summary>
    public ComicsData (HttpClient httpClient) {
        this.httpClient = httpClient;
    }

    /// <summary>コンストラクタ</summary>
    public ComicsData (HttpClient httpClient, DateTime date) : this (httpClient) {
        Date = date;
    }

    /// <summary>データ取得</summary>
    public async Task<bool> LoadAsync () {
        var result = false;
        if (rawData != null) {
            result = true;
        } else if (isLoading) {
            await TaskEx.DelayWhile (() => isLoading);
            result = rawData != null;
        } else {
            isLoading = true;
            using (var message = await httpClient.GetAsync (tsvUri, HttpCompletionOption.ResponseHeadersRead)) {
                if (message.IsSuccessStatusCode && message.StatusCode == System.Net.HttpStatusCode.OK) {
                    rawData = await message.Content.ReadAsStringAsync ();
                    foreach (var row in rawData.Replace ("\r\n", "\n").Split (['\n', '\r'])) {
                        var comic = new Comic (row);
                        if (comic.PublishDate == default) {
                            // パースに失敗しているなら、ブロックの日付(xxxx/xx/01)の翌月の1日前(==当月の末日)で補完する
                            comic.PublishDate = Date.AddMonths (1).AddDays (-1);
                        }
                        if (comic.Id > 0) {
                            Comics.Add (comic);
                        }
                    }
                    result = true;
                }
                isLoading = false;
            }
        }
        return result;
    }

    /// <summary>破棄</summary>
    public async void Dispose () {
        await TaskEx.DelayWhile (() => isLoading);
        rawData = null;
        Comics.Clear ();
    }

    /// <summary>文字列化</summary>
    public override string ToString () => $"{Date.Year:D4}/{Date.Month:D2} [{(IsLoaded ? Count.ToString () : "not loaded")}]\n{rawData}";

}
