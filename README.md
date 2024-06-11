---
title: .NET 8 / Blazor Web App / PetaPoco + MySqlConnector で MySQL(MariaDB) を使う
tags: Blazor ASP.NET PetaPoco MySQL MariaDB
---
# .NET 8 / Blazor Web App / PetaPoco + MySqlConnector で MySQL(MariaDB) を使う

## はじめに
- この記事では、.NET 8.0で、シンプルなBlazor Web Appを作ってみます。
    - PetaPocoとMySqlConnectorを使ってMySQL/MariaDBのデータを扱います。
      - EF Coreは使いません。
      - SQLビルダーは使わず、直にガリガリ書きます。
    - Microsoft.AspNetCore.Authentication.Googleを使って認証を行います。
        - Microsoft.AspNetCore.Identityは使いません。
    - MudBlazorを使って表示を構成します。
    - 基本的にサーバサイドでレンダリングします。
- この記事では、以下のような読者像を想定しています。
    - C#と.NETを囓っている
    - データベースのスキーマとSQLになじみがある
    - MudBlazorを使ったことがある。
    - Blazorのチュートリアルを済ませた
- この記事では以下には言及しません。
    - データベースの設計
    - MySQL/MariaDBの使い方
    - PetaPocoの使い方
    - ツール類の使用方法

### 環境
- Windows 11 Pro 22H2
  - VisualStudio 2022 17.9.6
  - PetaPoco 6.0.677
  - MySqlConnector 2.3.7
  - MudBlazor 6.20.0
  - Ubuntu 20.04 (wsl2)
    - MySql 8.0.36
- Debian 12.5
  - Microsoft.AspNetCore.App 8.0.6
  - Microsoft.NETCore.App 8.0.6
  - MariaDB 15.1

### 仕様
#### 題材と機能
- 書籍と著者のテーブルを閲覧、編集、追加、削除可能なアプリを作ります。
    - 書籍と著者は多対多の関係で、中間テーブルを使います。
- 公開されているコミックス発売日情報(tsv)を取り込めるようにします。
「[コミックス検索について](https://complex.matrix.jp/comics/)」

#### ホスティングモデル
- Serverをメインとしつつ、一応Hybridですが、MudBlazorの都合上、サーバ側レンダリングのみになります。
- オンプレミスな ASP.NET Core対応サーバを使います。
「[Windows から Blazor Web App をデプロイする Debian Server の構成](https://zenn.dev/tetr4lab/articles/ad947ade600764#os%E5%B0%8E%E5%85%A5)」 (Zenn)

#### UIフレームワーク
- MudBlazorを使用します。
「[.NET 8 の Blazor Web App で MudBlazor を使う](https://zenn.dev/tetr4lab/articles/74bd50585434ab)」 (Zenn)

#### 排他制御
- 楽観的排他制御のみを行って、競合が生じた場合は操作を断念します。

#### アカウント管理
- このプロジェクトではアカウント管理を行いません。
  - 認証としてGoogle OAuthを使用します。
「[Blazor Web App でOAuth認証を最小規模で使う (ASP.NET Core 8.0)](https://zenn.dev/tetr4lab/articles/1946ec08aec508)」 (Zenn)
- ローカルネットワーク内で、数人で使うことを想定しています。

## プロジェクトの構成
- VisualStudioで新規「Blazor Web App」プロジェクトを以下の想定で作ります。
    - フレームワークは`.NET 8.0`にします。
    - 認証の種類は「なし」にします。
    - HTTPS用の構成にします。
    - `Interactive render mode`は`Server`または`Auto(Server and WebAssembly)`にします。
    - `Interactivity location`は`Per page/component`にします。
    - ソリューションをプロジェクトと同じディレクトリに配置しません。
      - プロジェクト名が同じで異なる名前のソリューションを複数作るためです。
        - SQLiteのスタンドアローン版、EF Core版、PetaPocoでWebAPI版などを試しました。
- 以下のNuGetパッケージを導入します。
    - `Microsoft.AspNetCore.Authentication.Google`
    - `PetaPoco`
    - `MySqlConnector`
    - `MudBlazor`

### ビルド前処理
- VisualStudioのビルド前イベントで、gitから得たリビジョン情報を格納したテキストファイルをリソースとして埋め込みます。
    - ランタイムにリソースから情報を抽出して利用しています。
("ExLibris/Utilities/RevisionInfo.cs")

### パブリッシュ後処理
- `.csproj`に、発行後に(ファイルがあれば)バッチを起動するように仕込み、scpでサーバに転送してデプロイしています。

### 資料

https://github.com/CollaboratingPlatypus/PetaPoco/wiki

https://mysqlconnector.net/

https://mudblazor.com/docs/overview

https://mariadb.com/docs/

https://dev.mysql.com/doc/

https://qiita.com/hqf00342/items/b5afa3e6ebc3551884a4

## データベースの構成
### データベースの基礎設計
- 書籍 主テーブル
    - Id、バージョン、タイトル、詳細、出版(発売)日、出版社、叢書、価格
- 著者 主テーブル
    - Id、バージョン、名前、補助名、詳細
- 著者-書籍 中間テーブル
    - 著者Id、書籍Id

### テーブルの一般化
- 二つの主テーブルを共通に扱えるように一般化します。
    - これにより、コンポーネントのコードを可能な範囲で共有します。
- テーブル
    - 疑似列: テーブル名、単位、行名、列名、検索対象、関連リスト名、関係Idリスト、関連リスト、識別子、識別子のSQL(`where`)表現
    - 機能: 複製、更新、比較
- テーブル名と列名および単位は全行で共通(`static`)で、他は行毎に個別です。

### モデル
#### 基底クラスとインターフェイスの作成
- 一般化した設計に合わせて、モデルのベースを用意します。
- 基底クラス (`ExLibris/Data/ExLibrisBaseModel.cs`)
  - モデル(テーブル)に共通なカラム(プロパティ)と機能(疑似カラム)を実装します。
- インターフェイス (`ExLibris/Data/IExLibrisModel.cs`)
  - モデルごとに全レコードで共通なフィールド(疑似カラム)を規定します。
- モデル
  - 基底クラスを継承し、インターフェイスを実装します。
  - 著者 (`ExLibris/Components/Data/Author.cs`)
  - 書籍 (`ExLibris/Components/Data/Book.cs`)
- 属性
  - `[Table]`
    - テーブル名を定めます。
  - `[Column]`
    - 取り込みと書き出しで使用するカラムであることを規定します。
    - プロパティ名と異なるカラム名を規定できます。
  - `[VirtualColumn]`
    - `Column`とともに指定して、「取り込むけれど書き出さない」カラムであることを規定します。

#### モデルクラスの作成
- 主テーブルの分だけモデルクラスを作ります。
    - 先述の基底クラスを継承しインターフェイスを実装したクラスとして定義します。
- 中間テーブルはモデル化せず、PetaPocoと直接やりとりするサービス内でのみ扱います。

#### データベースのスキーマ
- DBへのアクセスを減らすために、DBで処理できることはある程度DBで済ませる方針です。
  - 必須カラムを`not null`に設定します。
  - ユニーク制約を設定します。
  - 外部キー制約で中間テーブルの行が自動的に削除されるように構成します。
  - トリガーによって、行バージョンの不整合を検出してエラーにします。
  - collationは、C#での比較に合わせてutf8mb4_binを使います。
- この記事では、DBの設計や操作は扱いません。

```bash
$ sudo mariadb-dump exlibris --no-data
# or
$ sudo mysqldump exlibris -d
```

```sql
-- Table structure for table `AuthorBook`
DROP TABLE IF EXISTS `AuthorBook`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `AuthorBook` (
  `AuthorsId` int(11) NOT NULL,
  `BooksId` int(11) NOT NULL,
  PRIMARY KEY (`AuthorsId`,`BooksId`),
  KEY `IX_AuthorBook_BooksId` (`BooksId`),
  CONSTRAINT `FK_AuthorBook_Authors_AuthorsId` FOREIGN KEY (`AuthorsId`) REFERENCES `Authors` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_AuthorBook_Books_BooksId` FOREIGN KEY (`BooksId`) REFERENCES `Books` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;
/*!40101 SET character_set_client = @saved_cs_client */;

-- Table structure for table `Authors`
DROP TABLE IF EXISTS `Authors`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Authors` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Version` int(11) NOT NULL DEFAULT 0,
  `Name` varchar(255) NOT NULL,
  `AdditionalName` varchar(255) NOT NULL,
  `Description` longtext DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Authors_Name_AdditionalName` (`Name`,`AdditionalName`)
) ENGINE=InnoDB AUTO_INCREMENT=88754 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;
/*!40101 SET character_set_client = @saved_cs_client */;

-- Table structure for table `Books`
DROP TABLE IF EXISTS `Books`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8 */;
CREATE TABLE `Books` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `Version` int(11) NOT NULL DEFAULT 0,
  `Title` varchar(255) NOT NULL DEFAULT '',
  `Description` longtext DEFAULT NULL,
  `PublishDate` datetime(6) DEFAULT NULL,
  `Publisher` varchar(255) DEFAULT NULL,
  `Series` varchar(255) DEFAULT NULL,
  `Price` decimal(65,30) NOT NULL,
  PRIMARY KEY (`Id`)
  UNIQUE KEY `IX_Books_Title_Publisher_Series_PublishDate` (`Title`,`Publisher`,`Series`,`PublishDate`)
) ENGINE=InnoDB AUTO_INCREMENT=601181 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;
/*!40101 SET character_set_client = @saved_cs_client */;
```

#### トリガー
- 主テーブルへの更新時に行バージョンの不整合を検出してエラーにするトリガーです。

```sql
delimiter //
create trigger Version_Check_Before_Update_On_Authors
before update on Authors
for each row
begin
    if new.Version <= old.Version then
        signal SQLSTATE '45000'
        set MESSAGE_TEXT = 'Version mismatch detected.';
    end if;
end;//
-- delimiter ;
-- delimiter //
create trigger Version_Check_Before_Update_On_Books
before update on Books
for each row
begin
    if new.Version <= old.Version then
        signal SQLSTATE '45000'
        set MESSAGE_TEXT = 'Version mismatch detected.';
    end if;
end;//
delimiter ;
```

### データベースの接続名
- `secrets.json`に開発用データベースへのパスと接続名などを追加します。

```json:secrets.json
{
    "ConnectionStrings": {
        "Host": "server=localhost;",
        "Account": "user=<user>;<password>;"
    }
}
```

- これらの文字列は、以下のコードで取得されます。

```csharp:ExLibris/Program.cs
builder.Configuration.GetConnectionString ("Host")
builder.Configuration.GetConnectionString ("Account")
```

- 格納先の他の選択肢と製品用の構成については、以下の記事で触れています。

- https://zenn.dev/tetr4lab/articles/1946ec08aec508

## サービスとしてDB入出力を構成する
### DB入出力サービスの構成
- PetaPocoのラッパーサービスを構成します。 (`ExLibris/Services/ExLibrisDataSet.cs`)
    - DBコネクションやSQLをこのサービス内に集約し隠蔽します。
- PetaPocoは、コネクションを渡すのではなく、接続文字列とともにコネクターを指定する方法で使用します。
- このプロジェクトでは、WebAPIは使わず、サービスを直接呼び出します。(サーバ側でのみ使います。)
- 機能として、一覧、追加、更新、削除などを用意します。
- サービスはジェネリックに実装されていて、型引数でモデルに振り分けられます。

### サービスの登録
- Program.csでセッション毎に独立したインスタンスが生成されるサービスとして登録します。

```csharp:ExLibris/Program.cs
builder.Services.AddScoped (_ => (Database) new MySqlDatabase (connectionString, "MySqlConnector"));
builder.Services.AddScoped<ExLibrisDataSet> ();
```

## ページの構成
- 全てのページトップに固定された横並びメニューバーを置きます。
    - バーの背後にページコンテンツが隠れないように、ページコンテンツ上部をパディングします。
- 横幅が狭いときはバーを非表示にして、ドロワーを開くボタンと開いたドロワーを表示します。
    - ボタンとドロワーはページコンテンツを背後に隠します。
- バーの付属物として、検索フィールドとボタンを表示します。
- ページコンテンツのトップに、ページの見出し、セッション数を表示します。

### レイアウト
- レイアウト(`ExLibris/Components/Layout/MainLayout.razor`)に記述することで、全ページで共有します。
    - ナビゲーションバーはレイアウトに直接記述せず、コンポーネントにします。
- ナビゲーションバーの検索文字列、ボタンの押下、ページの見出しなどの情報は、レイアウトで保持して、ページとの間で共有します。
- ナビゲーションバーには、検索文字列を更新するコールバック先をパラメータで渡します。
- ページには、ページの見出しを更新するコールバック先と検索文字列を渡します。
    - ページへ`@Body`越しに値を渡す際に、カスケーディングパラメータを使います。
- ページトップのパディング、タイトル、セッション数といったヘッダ部分はレイアウトに配置されます。

### ナビゲーションバー
- レイアウトで配置されます。(`ExLibris/Components/Layout/NavBar.razor`)
- 一部内容がホームページの一部コンテンツとして再利用されます。
    - レイアウトから使われた場合は有効なパラメータが渡されていますが、ページから使われた場合はそれがありません。

### ページタイトル
- タイトルは、レイアウト側で配置しているため、各ページでは、初期化の際にタイトルを親へ通知する必要があります。

### セッション数
- レイアウトレベルで、ページ毎に組み込まれるコンポーネントです。(`ExLibris/Components/Pages/SessionCounter.razor`)
  - 実際には、「セッション数」でなく「インスタンス数」で、自身のインスタンスを活殺に合わせてリストし、その数を表示します。
    - 全てのページに一つだけ、必ず配置されることが前提になっています。
    - コンポーネントを配置しないページはセッション外と見なされます。
  - 別途、静的メソッドとして、セッション数の更新を通知するサービスを提供します。
- 蛇足
  - 行儀良く書くなら、別クラスのシングルトンサービスとして実装するべき(?)インスタンスの管理機構を、簡易に静的メンバーを使って済ませています。
  - `IDisposable`を実装することで、破棄される際のトリガーを得ます。
  - 別セッション(スレッド)での更新が他のセッションへ伝達された場合、そのタイミングで直接[`ComponentBase.StateHasChanged()`](https://learn.microsoft.com/ja-jp/dotnet/api/microsoft.aspnetcore.components.componentbase.statehaschanged?view=aspnetcore-7.0)を呼ぶことができません。
    - エラーして、`The current thread is not associated with the Dispatcher. Use InvokeAsync() to switch execution to the Dispatcher when triggering rendering or component state.`と表示されます。
    - 書かれている通りに[`ComponentBase.InvokeAsync()`](https://learn.microsoft.com/ja-jp/dotnet/api/microsoft.aspnetcore.components.componentbase.invokeasync?view=aspnetcore-7.0)を使うことで、同期コンテキスト(該当セッションのBlzaor UIスレッド)で動作させることができます。

## 一覧、詳細、編集、追加、削除
- 書籍と著者で同様のものを作るので、雛形を用意して、それを継承する形でそれぞれを作ります。
- ページは一覧の一つだけにして、他はダイアログとして実装します。
  - MudBlazorでは表のインライン編集も可能ですが、今回は使用しません。
- 一覧に複数項目の選択機能を付けて、一括削除を可能にします。
- 詳細ダイアログで閲覧/編集モードを切り替えるようにします。

- 

### 一覧
### 詳細と編集
### 追加
### 削除
### 一括削除

## おわりに
### 次に向けて
- (4)では、

### あとがき
- 執筆者は、Blazor、ASP.NETともに初学者ですので、誤りもあるかと思います。
    - お気づきの際は、是非コメントや編集リクエストにてご指摘ください。
    - あるいは、「それでも解らない」、「自分はこう捉えている」などといった、ご意見、ご感想も歓迎いたします。
