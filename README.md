---
title: .NET 8 / Blazor Web App / MudBlazor / PetaPoco+MySqlConnector で MySQL/MariaDB を使う
tags: Blazor ASP.NET MudBlazor PetaPoco MySQL MariaDB
---
# .NET 8 / Blazor Web App / MudBlazor / PetaPoco+MySqlConnector で MySQL/MariaDB を使う

## はじめに
- この記事では、.NET 8.0で、シンプルなBlazor Web Appを作ってみます。
  - PetaPocoとMySqlConnectorを使ってMySQL/MariaDBのデータを扱います。
    - EF Coreは使いません。
    - SQLビルダーは使わず、直にガリガリ書きます。
  - Microsoft.AspNetCore.Authentication.Googleを使って認証を行います。
    - Microsoft.AspNetCore.Identityは使いません。
  - MudBlazorを使ってUIを構成します。
  - 基本的にサーバサイドでレンダリングします。
- この記事では、以下のような読者像を想定しています。
  - C#と.NETを囓っている
  - データベースのスキーマとSQLになじみがある
  - Blazorのチュートリアルを済ませた
  - MudBlazorを使ったことがある。
- この記事では以下には言及しません。
  - データベースの設計
  - MySQL/MariaDBの使い方
  - MudBlazorの使い方
  - PetaPocoの使い方
  - ツール類の使用方法
- ドキュメントとコードが異なる場合は、コードが優先されます。
  - コードの更新に対して、ドキュメントが適切に更新されていない場合があります。

### 環境
- Windows 11 Pro 22H2
  - VisualStudio 2022 17.10.3
  - PetaPoco 6.0.677
  - MySqlConnector 2.3.7
  - MudBlazor 7.3.0
  - Ubuntu 20.04 (wsl2)
    - MySql 8.0.37
- Debian 12.5
  - Microsoft.AspNetCore.App 8.0.7
  - Microsoft.NETCore.App 8.0.7
  - MariaDB 15.1

### 仕様
#### 題材と機能
- 書籍と著者のテーブルを閲覧、編集、追加、削除可能なアプリを作ります。
    - 書籍と著者は多対多の関係で、中間テーブルを使います。
- 公開されているコミックス発売日情報(tsv)を取り込めるようにします。
  - このサイトでは、月毎に発売される予定のコミックスの情報が個別のtsvファイルで公開されています。

https://complex.matrix.jp/comics/

#### ホスティングモデル
- プロジェクトはHybridですが、サーバ側レンダリングのみを使用します。
- オンプレミスな ASP.NET Core対応サーバを使います。

https://zenn.dev/tetr4lab/articles/ad947ade600764#os%E5%B0%8E%E5%85%A5

#### UIフレームワーク
- MudBlazorを使用します。
  - いくつか見たのですが、JSに触らずに済ませたい私には、MudBlazor以外にありませんでした。

https://mudblazor.com/docs/overview

https://zenn.dev/tetr4lab/articles/74bd50585434ab

#### データベース周り
- MariaDB/MySQLをサーバに使用します。

https://mariadb.com/docs/

https://dev.mysql.com/doc/

- MySqlConnectorをプロバイダに使用します、

https://mysqlconnector.net/

- PetaPocoをORマッパーに使用します。
  - EF Core、Dapper、MySqlConnectorを直接使うなどを試した上で、PetaPocoに落ち着きました。
  - EF Coreは、高機能で手軽に使えるのですが、重いのと、行儀良く使おうとすると学習コストが高いのがネックです。
  - Dapperは、軽くて学習コストも低いのですが、トランザクションをネストさせようとすると、面倒な上に美しくありません。
  - PetaPocoは、軽くて学習コストが低く、自前のネスト・トランザクションをサポートしています。このサポートが決め手になりました。
  - ここでの学習コストにSQLやサーバ運用は含まれません。

https://github.com/CollaboratingPlatypus/PetaPoco/wiki

#### 排他制御
- 楽観的排他制御のみを行って、競合が生じた場合は操作を断念します。

#### アカウント管理
- このプロジェクトではアカウント管理を行いません。
  - 認証としてGoogle OAuthを使用します。
- ローカルネットワーク内で、数人で使うことを想定しています。

https://zenn.dev/tetr4lab/articles/1946ec08aec508

## プロジェクトの構成
- VisualStudioで新規「Blazor Web App」プロジェクトを以下の想定で作ります。
    - フレームワークは`.NET 8.0`にします。
    - 認証の種類は「なし」にします。
    - HTTPS用の構成にします。
    - `Interactive render mode`は`Server`または`Auto(Server and WebAssembly)`にします。
    - `Interactivity location`は`Per page/component`にします。
    - ソリューションをプロジェクトと同じディレクトリに配置しません。
      - 試行錯誤で、プロジェクト名が同じで異なる名前のソリューションを複数作るためです。
- 以下のNuGetパッケージを導入します。
    - `Microsoft.AspNetCore.Authentication.Google`
    - `PetaPoco`
    - `MySqlConnector`
      - `MySql.Data`ではありませんが、そちらでも大差なく可能なようです。(未検証)
    - `MudBlazor`

### ビルド前処理
- VisualStudioのビルド前イベントで、gitから得たリビジョン情報をテキストファイルに格納します。(`ExLibris/Utilities/RevisionInfo.cs`)
  - ファイルはビルド時にリソースとして埋め込み、ランタイムにリソースから情報を抽出して利用します。

https://qiita.com/hqf00342/items/b5afa3e6ebc3551884a4

### パブリッシュ後処理
- `.csproj`に、発行後に(ファイルがあれば)シェルスクリプトを起動するように仕込み、スクリプト中でscpでサーバに転送してデプロイしています。

https://learn.microsoft.com/ja-jp/visualstudio/msbuild/how-to-extend-the-visual-studio-build-process

https://zenn.dev/tetr4lab/articles/ad947ade600764#%E5%B1%95%E9%96%8B%E3%81%AE%E8%87%AA%E5%8B%95%E5%8C%96

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

#### モデルクラスの作成
- 主テーブルの分だけモデルクラスを作ります。
  - 先述の基底クラスを継承しインターフェイスを実装したクラスとして定義します。
  - 著者 (`ExLibris/Components/Data/Author.cs`)
  - 書籍 (`ExLibris/Components/Data/Book.cs`)
- 中間テーブルはモデル化せず、PetaPocoと直接やりとりするサービス内でのみ扱います。

##### 属性
  - `[Table]`
    - クラスに付与して、テーブル名を定めます。
  - `[Column]`
    - プロパティに付与して、取り込みと書き出しで使用するカラムであることを示します。
    - プロパティ名と異なるカラム名を規定できます。
  - `[VirtualColumn]`
    - `Column`とともに指定して、「取り込むけれど書き出さない」カラムであることを示します。

### データベースのスキーマ
- DBへのアクセスを減らすために、DBで処理できることはある程度DBで済ませる方針です。
- 必須カラムを`not null`に設定します。
- ユニーク制約を設定します。
- 外部キー制約で中間テーブルの行が自動的に削除されるように構成します。
- トリガーによって、行バージョンの不整合を検出してエラーにします。
- collationは、C#での比較に合わせてutf8mb4_binを使います。
- この記事では、DBの設計や操作は扱いません。

```sql:MariaDB
CREATE TABLE `AuthorBook` (
  `AuthorsId` bigint(20) NOT NULL,
  `BooksId` bigint(20) NOT NULL,
  PRIMARY KEY (`AuthorsId`,`BooksId`),
  KEY `IX_AuthorBook_BooksId` (`BooksId`),
  CONSTRAINT `FK_AuthorBook_Authors_AuthorsId` FOREIGN KEY (`AuthorsId`) REFERENCES `Authors` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_AuthorBook_Books_BooksId` FOREIGN KEY (`BooksId`) REFERENCES `Books` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;
CREATE TABLE `Authors` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Version` int(11) NOT NULL DEFAULT 0,
  `Name` varchar(255) NOT NULL,
  `AdditionalName` varchar(255) NOT NULL,
  `Description` longtext DEFAULT NULL,
  `Interest` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Authors_Name_AdditionalName` (`Name`,`AdditionalName`)
) ENGINE=InnoDB AUTO_INCREMENT=88754 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;
CREATE TABLE `Books` (
  `Id` bigint(20) NOT NULL AUTO_INCREMENT,
  `Version` int(11) NOT NULL DEFAULT 0,
  `Title` varchar(255) NOT NULL DEFAULT '',
  `Description` longtext DEFAULT NULL,
  `PublishDate` datetime(6) DEFAULT NULL,
  `Publisher` varchar(255) DEFAULT NULL,
  `Series` varchar(255) DEFAULT NULL,
  `Price` int(11) NOT NULL,
  `Action` varchar(50) DEFAULT NULL,
  `Result` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`Id`)
  UNIQUE KEY `IX_Books_Title_Publisher_Series_PublishDate` (`Title`,`Publisher`,`Series`,`PublishDate`)
) ENGINE=InnoDB AUTO_INCREMENT=601181 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_bin;
```

#### トリガー
- 主テーブルへの更新時に行バージョンの不整合を検出してエラーにするトリガーです。

```sql:MariaDB
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

### データベースの接続文字列
- `secrets.json`に開発用データベースのアドレスとアカウントを追加します。

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

https://zenn.dev/tetr4lab/articles/1946ec08aec508

## サービスとしてDB入出力を構成する

### Database のラッパー
- `PetaPoco.Database`クラスのラッパーを用意します。(`ExLibris/Services/MySqlDatabase.cs`)
- `Database`を直接使用すると、MySqlConnectorの例外が表に出てこなくなります。
- `Database`を継承したクラスを用意して、`OnException`を`override`することで、任意に情報を取り出せるようになります。

### DB入出力サービスの構成
- PetaPocoのラッパーサービスを構成します。 (`ExLibris/Services/ExLibrisDataSet.cs`)
    - PetaPocoとのやりとりをこのサービス内に集約し隠蔽します。
- PetaPocoは、コネクションを渡すのではなく、接続文字列とともにコネクターを指定する方法で使用します。
- 外部に提供されるサービスは、トランザクション内で処理され、エラーが生じた場合はロールバックされます。
  - 内部では、トランザクションのネストを使用できますが、サービスとしては提供されておらず、複数のサービスを組み合わせてトランザクション化するような使い方はできません。
  - 必要に応じてサービスのAPIを拡張する想定です。
- このプロジェクトでは、サーバ側でのみ使うことを前提に、WebAPIは使わず、サービスを直接呼び出します。
- 全てのサービスはジェネリックに実装されて、型引数でモデルに振り分けられます。
- 機能として、一覧、追加、更新、削除などを用意します。
- 初期化時点で、書籍と著者の全データを読み込んで保持します。
  - オンメモリのデータは読み取り用に使用することを前提にしており、編集しても追跡はされません。
  - データベースに対して更新、追加、削除を行うと、オンメモリのデータも変更されますが、リロードするわけではないので、使い方によっては不整合が生じる可能性があります。
  - エラー発生時などのリロードは、アプリケーション階層で制御します。

#### 一覧を取得
- 基本戦略として、ひとつのテーブルを読む際に、一緒に関係先のIdリストも取り出して、読み込み済みモデル・インスタンスとリンクさせたリストを動的に生成します。

##### A. 関係先Idだけのモデル・クラスを用意してマルチマッピングする
- 中間テーブルを`join`して、関係先Idを同時に取り込む方式です。
- 取り込んだ直後は、関係先の数だけ同じIdが複数行存在するので、そのリストを`.GroupBy (i => i.Id).Select (GroupConcat<T1, T2>)`のようにしてまとめます。

##### B. 関係先Idリストをコンマ区切り文字列として取得する
- 中間テーブルを`join`&`group by Id`して、関係先Idを`group_concat`することで、文字列のカラムとして取り込む方式です。
- 文字列は、`.Split (',')`して`List<int>`を動的に生成します。
- さらに、読み込み済みのモデル・インスタンスにリンクさせたリストを動的に生成します。
- いずれの生成結果もキャッシュされます。

###### B-1. リストの生成を、一覧の読み込み時に一括して実施
- 最初の読み込みにまとまった時間がかかります。

###### B-2. リストの生成を、必要になる都度実施
- 一巡するまで、一覧ページの切り替え毎に、ページサイズなりの時間を使います。

###### B-2を採用
- このプロジェクトでは、B-2を採用しています。

#### 単一アイテムの更新
- 中間テーブルに対しては、一括削除と一括挿入を行います。
- エラーが生じた場合はロールバックされます。

#### 単一アイテムの追加
- まず、主テーブルに挿入し、さらに、そのIdを取得します。
- 取得したIdを使用して、中間テーブルに関係を一括挿入します。
- エラーが生じた場合はロールバックされます。

#### 一括アイテムの追加
- 単一アイテムの挿入を繰り返す方式と、バルクインサートでまとめて挿入する方式の双方を実装しています。
- 一括挿入する場合の手順だけを説明します。
  - テーブルの次の自動更新Idを取得します。
  - 主テーブルへ全アイテムを一括挿入します。
  - 先に得たIdを元に、挿入した全アイテムのIdを確定させます。
  - 中間テーブルへ、全アイテムの全関係を一括挿入します。
  - エラーが生じた場合はロールバックされます。

#### 単一アイテムの削除
- 対象アイテムを取得します。
- バージョンを照合して、問題なければ削除を実施します。
- エラーが生じた場合はロールバックされます。

#### 一括アイテムの削除
- 対象アイテムのうちバージョンの一致するものだけを一括取得します。
- 取得されたアイテムを一括削除します。
- エラーが生じた場合はロールバックされます。

#### オートインクリメントを初期化
- テーブルが空の場合に限り、オートインクリメントの値を1に戻します。

### サービスの登録
- Program.csでセッション毎に独立したインスタンスが生成されるサービスとして登録します。

```csharp:ExLibris/Program.cs
builder.Services.AddScoped (_ => (Database) new MySqlDatabase (connectionString, "MySqlConnector"));
builder.Services.AddScoped<ExLibrisDataSet> ();
```

## ページとコンポーネントの構成
### レイアウト
- レイアウト(`ExLibris/Components/Layout/MainLayout.razor`)に記述することで、全ページで共有します。
  - ナビゲーションバーはレイアウトに直接記述せず、独立したコンポーネントにします。
- ナビゲーションバーの検索文字列、検索ボタンの押下、ページの見出しなどの情報は、レイアウトで保持して、ページとの間で共有します。
  - ページへ`@Body`越しに値を渡す際には、カスケーディングパラメータを使います。

https://zenn.dev/tetr4lab/articles/fdc11955916064

- ナビゲーションバーには、検索文字列を更新するコールバック先をパラメータで渡します。
- ページには、ページの見出しを更新するコールバック先と検索文字列を渡します。
  - ページコンテンツのトップに、ページの見出し、セッション数を表示します。
- ページトップのパディング、タイトル、セッション数といったヘッダ部分はレイアウトに配置されます。

### ナビゲーションバー
- 全てのページトップに固定された横並びメニューバーを置きます。(`ExLibris/Components/Layout/NavBar.razor`)
  - レイアウトで配置されます。
  - バーの背後にページコンテンツが隠れないように、ページコンテンツ上部をパディングします。
- 横幅が狭いときはバーを非表示にして、ドロワーを開くボタンと開いたドロワーを表示します。
  - ボタンとドロワーはページコンテンツを背後に隠します。
- バーの付属物として、検索フィールドと検索ボタンを表示します。
- 一部内容がホームページの一部コンテンツとして再利用されます。
  - レイアウトから使われた場合は有効なパラメータが渡されていますが、ページから使われた場合はそれがありません。

### ページタイトル
- レイアウト側で、ページの上端に配置される文字列です。
- 各ページでは、初期化の際にタイトルを親(`MainLayout.razor`)へ通知する必要があります。

### セッション数
- レイアウト側で、ページの上端に配置されるコンポーネントです。(`ExLibris/Components/Pages/SessionCounter.razor`)
  - 実際には、「セッション数」でなく「インスタンス数」で、自身のインスタンスを活殺に合わせてリストし、その数を表示しています。
    - 全てのページに一つだけ、必ず配置されることが前提になっています。
    - コンポーネントを配置しないページはセッション外と見なされます。
  - 別途、静的メソッドとして、セッション数の更新を通知するサービスを提供します。
- 蛇足
  - 行儀良く書くなら、別クラスのシングルトンサービスとして実装するべき(?)インスタンスの管理機構を、簡易に静的メンバーを使って済ませています。
  - `IDisposable`を実装することで、破棄される際のトリガーを得ます。
  - 別セッション(スレッド)での更新が他のセッションへ伝達された場合、そのタイミングで直接[`ComponentBase.StateHasChanged()`](https://learn.microsoft.com/ja-jp/dotnet/api/microsoft.aspnetcore.components.componentbase.statehaschanged?view=aspnetcore-7.0)を呼ぶことができません。
    - エラーして、`The current thread is not associated with the Dispatcher. Use InvokeAsync() to switch execution to the Dispatcher when triggering rendering or component state.`と表示されます。
    - 実装の通りに[`ComponentBase.InvokeAsync()`](https://learn.microsoft.com/ja-jp/dotnet/api/microsoft.aspnetcore.components.componentbase.invokeasync?view=aspnetcore-7.0)を使うことで、同期コンテキスト(該当セッションのBlzaor UIスレッド)で動作させることができます。

### ホームと更新の取得
- ホーム画面には、収蔵数の表示や更新などの機能を持たせています。
- 先述した「月毎のコミックスの発売日情報(tsv)」を取得し、モデルデータに変換した上で、著者と、書籍に分けてバルクインサートを行います。
  - 情報は「発売予定」なので、委細未定だったり、発売が延期されて再掲載されたり、実際には発売されなかったりしています。
  - 同一の著者が良く似た複数の名前で登録されていたりします。
- 取得ボタンを押すと不足分が検出されて、確認ダイアログが開きます。
  - 複数のセッションが存在する場合は利用できません。
  - 該当月に発売された書籍が1件もない場合に未取り込みと判定します。
    - 1件でも存在すると取り込み対象にならないので、再取り込みを行う場合は、あらかじめ、書籍を対象月で絞り込んで一括削除を行います。
- 取得した情報の登録では以下のような処理を行います。
  - 1ヶ月分の未登録の著者を一括登録します。(トランザクション)
  - 1ヶ月分の書籍、および、書籍と著者との関係を一括登録します。(トランザクション)
    - 元データには、このプロジェクトで規定しているユニーク制約に違反するデータが存在するので、複数の書籍で重複エラーが生じます。
      - エラーがあると、一括登録全体がロールバックされます。
    - エラーが生じた場合は、1冊毎の登録を行うことで、エラーする書籍のみを除外して登録します。

### 一覧と一括操作
- 書籍と著者で同様のページを作るので、基底クラス(`ExLibris/Components/Pages/ItemListBase.cs`)を用意します。
  - 派生クラス: `ExLibris/Components/Pages/AuthorsList.razor`, `ExLibris/Components/Pages/BooksList.razor`
- ページは書籍と著者の一覧だけにして、詳細はダイアログとして実装します。
- 一覧は、`MudTable`で実装し、`MudTableSortLabel`を使って、ソートできるようになっています。
  - 関係先のソートは、タイトルや名前でなくIdで行っています。
  - 関係先のソートを行うと、最初だけ、関係先のリストがまとめて生成されるので少しもたつきます。
  - タイトルや名前でソートするためには、インスタンス生成が必要になり時間がかかるので、その場合は、読み込みをB-1方式に変更した方が良いかもしれません。
- 行をクリックすると、アイテムダイアログが開きます。
- 検索フィールドに入力された文字列を空白文字で分割して、検索対象カラムと照合します。
  - 区切られた全ての語が、検索対象カラムのいずれかに含まれると、条件が成立します。
- 複数項目の選択機能を使うと、一括削除が可能です。
  - 複数のセッションが存在する場合は安全のため利用できません。

### アイテムダイアログ
- 書籍と著者で同様のダイアログを作るので、基底クラス(`ExLibris/Components/Pages/ItemDialogBase.razor`)を用意します。
  - 派生クラス: `ExLibris/Components/Pages/AuthorDialog.razor`, `ExLibris/Components/Pages/BookDialog.razor`
- 閲覧/編集モードが切り替わります。
- 閲覧時は、単体削除と編集モードへの切り替えができます。
- 編集時は、保存とキャンセルができます。
    - 編集開始時と変化がない状態では保存操作はできません。
    - 編集開始時と変化がある状態でキャンセルする際は、編集内容の喪失について確認があります。
    - キャンセルすると、閲覧モードに戻ります。
- 書籍や著者がボタンになっています。
  - 閲覧時には、閲覧対象を切り替えます。
  - 編集時には、入力した文字列で検索して絞り込んだ中から選択できます。

### 関係先選択ダイアログ
- `ExLibris/Components/Pages/SelectRelatedItemsDialog.razor`
- `MudSelect`のような単純な一覧にすると、探しづらいだけでなく重いため、`MudAutocomplete`を使用しています。

### プログレスダイアログ
- `ExLibris/Components/Pages/ProgressDialog.razor`
- プログレスバーや表示内容の更新や操作と外因によるキャンセルの受け付け、完了後の確認などができるようになっています。

### 汎用確認ダイアログ
- `ExLibris/Components/Pages/ConfirmationDialog.razor`
- 「Yes/No」や「Ok」の確認ダイアログです。

## おわりに
- 執筆者は、Blazor、ASP.NET、PetaPocoなど諸々において初学者ですので、誤りもあるかと思います。
    - お気づきの際は、是非コメントや編集リクエストにてご指摘ください。
    - あるいは、「それでも解らない」、「自分はこう捉えている」などといった、ご意見、ご感想も歓迎いたします。
