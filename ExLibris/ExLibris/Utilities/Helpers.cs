using Microsoft.AspNetCore.Components;
using System.Net;
using static ExLibris.Services.ExLibrisDataSet;

namespace Tetr4lab;

public static class Helpers {
}

public static class StringHelper {
    /// <summary>文字列を指定幅に丸める</summary>
    public static string Ellipsis (this string str, int width, string mark = "…") => str.Length <= width ? str : $"{str [0..(width - mark.Length)]}{mark}";

    /// <summary>文字列集合が指定の部分文字列を含むか</summary>
    public static bool SubContains (this IEnumerable<string> list, string target) {
        foreach (var item in list) {
            if (item.Contains (target)) { return true; }
        }
        return false;
    }
}

/// <summary>NavigationManager</summary>
public static class NavigationManagerHelper {
    /// <summary>ページのリロード</summary>
    public static void Reload (this NavigationManager manager, bool forceLoad = true) => manager.NavigateTo (manager.Uri, forceLoad);
}

/// <summary>HttpClient</summary>
public static class HttpClientHelper {
    /// <summary>ヘッダ要求への応答ステータスコードを返す 失敗したら0を返す</summary>
    public static async Task<HttpStatusCode> GetStatusCodeAsync (this HttpClient httpClient, string uri) {
        var request = new HttpRequestMessage (HttpMethod.Head, uri);
        if (request != null) {
            using (var response = await httpClient.SendAsync (request)) {
                return response.StatusCode;
            }
        }
        return 0;
    }
}
