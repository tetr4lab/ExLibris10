using System.Reflection;

namespace Tetr4lab;

/// <summary>リビジョン情報</summary>
/// <example><code>
/// if (RevisionInfo.Valid) Console.WriteLine ($"revision: {RevisionInfo.Branch} {RevisionInfo.Id}");
/// </code></example>
/// <settings>
/// - プロジェクトのプロパティ > ビルド > イベント > ビルド前のイベント
///   git branch --show-current > $(ProjectDir)revision.info
///   git rev-parse --short HEAD >> $(ProjectDir) revision.info
/// - `$(ProjectDir)revision.info`のプロパティ
///   ビルドアクションを「埋め込みリソース」に設定
/// </settings>
/// <see href="https://qiita.com/hqf00342/items/b5afa3e6ebc3551884a4">[.NET/C#] アプリにgitのコミットIDを埋め込む</see>
public static class RevisionInfo {

    /// <summary>有効性</summary>
    public static bool Valid { get; private set; } = false;

    /// <summary>ブランチ</summary>
    public static string Branch { get; private set; } = string.Empty;

    /// <summary>コミットID</summary>
    public static string Id { get; private set; } = string.Empty;

    /// <summary>リソースファイル名</summary>
    private const string ResourceName = "revision.info";

    /// <summary>コンストラクタ</summary>
    static RevisionInfo () {
        var asm = Assembly.GetExecutingAssembly ();
        var resName = asm.GetManifestResourceNames ().ToList ().Find (n => n.EndsWith (ResourceName));
        if (resName != null) {
            using (var stream = asm.GetManifestResourceStream (resName)) {
                if (stream != null) {
                    var lines = (new StreamReader (stream)).ReadToEnd ().Replace ("\r\n", "\n").Split (['\r', '\n']);
                    if (lines.Length > 1) {
                        Branch = lines [0].Trim ();
                        Id = lines [1].Trim ();
                        Valid = true;
                    }
                }
            }
        }
    }

}

