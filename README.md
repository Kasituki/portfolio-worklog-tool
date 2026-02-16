# 工数集計ツール（WPF + SQL Server）

工数ログCSVを取り込み、期間指定で **月別 / 案件別 / メンバー別**に集計し、
表とグラフで可視化、集計結果をCSV出力できるWindowsデスクトップツールです。

**特徴**
- CSV取込時のバリデーション（必須チェック・型変換）
- 重複取り込み防止（ファイル内重複 / DB重複）
- スキップ理由のログCSV出力（import_errors_yyyyMMdd_HHmmss.csv）
- 投入前の「ドライラン（検査のみ）」対応
- 集計結果のCSV出力

想定用途：WPF / SQL Server / CSV取込 / 集計・可視化 / 例外・重複処理などの実装スキルを確認するためのポートフォリオとして作成しています。

---

## 主な機能

### 1) CSV取込（工数ログ）
- CSVを読み込み、必須チェック・型変換を実施
- **ファイル内重複チェック**（DuplicateInFile）
- **DB重複チェック**（DuplicateInDb）
- スキップした行はログCSVとして出力
- **ドライラン（検査のみ）**に対応（DBへ投入しない）

### 2) 集計・可視化
- 期間（From/To）指定
- 「期間を設定」ボタンで、DB（dbo.WorkLogs）の最小/最大日付を自動セット
- 月別工数（折れ線＋表）
- 案件別工数（TOP10、横棒＋表）
- メンバー別工数（TOP10、横棒＋表）

### 3) CSV出力
- 月別工数 / 案件別工数 / メンバー別工数 をそれぞれCSV出力

---

## CSVフォーマット

ヘッダ行あり（UTF-8推奨）

|列名|必須|例|説明|
|---|---:|---|---|
|WorkDate|◯|2026-02-01|作業日|
|Member|◯|Suzuki|担当者|
|Project|◯|PJ-Alpha|案件名|
|WorkType|◯|Dev|作業区分|
|Hours|◯|7.5|工数（時間）|
|HourlyRate|任意|4500|時給/単価（未指定可）|

※ 読み込み時に `WorkDate / Member / Project / WorkType / Hours / HourlyRate` は Trim（前後空白除去）されます。

---

## 重複判定（キー）

重複判定キーは以下です（ファイル内重複 / DB重複ともに同一キーで判定）。
アプリ側：既存キー照会 + HashSet でスキップ
- `(WorkDate, Member, Project, WorkType)`

キー文字列は以下形式で生成して比較しています：
- `yyyy-MM-dd|Member|Project|WorkType`

DB側：ユニークインデックスで最終防衛
アプリ側は事前にスキップするが、並行実行や取りこぼし対策としてDB側でもユニーク制約で保証
---

## スキップ理由（ログ出力）

スキップ行は、CSVと同じフォルダにログを出力します。

- ファイル名例：`import_errors_yyyyMMdd_HHmmss.csv`
- 出力カラム：
  - RowNumber, Reason, WorkDate, Member, Project, WorkType, Hours, HourlyRate

Reason（出力文字列）は以下です：
- `required_missing`（必須項目欠損）
- `invalid_workdate`（日付型変換エラー）
- `invalid_hours`（工数型変換エラー）
- `duplicate_in_file`（ファイル内重複）
- `duplicate_in_db`（DB重複）
- `parse_error`（解析エラー）
※Hours が空の場合は required_missing、数値変換に失敗した場合は invalid_hours として記録します。

画面上の「エラーログを開く」ボタンから、ログファイルを直接開けます。

---

## ドライラン（検査のみ）

「投入せず検査のみ（ドライラン）」がONの場合、
- CSV読込、必須チェック、型変換、重複チェック、スキップログ出力、サマリ更新は実行
- **DBへの投入（SqlBulkCopy）は行いません**
- サマリの「実投入件数」は `0 件（投入なし）` と表示されます

---

## セットアップ

### 必要環境
- Windows
- .NET 8（Windows Desktop）
- SQL Server（例：SQLEXPRESS）
- SSMS（任意）

### appsettings.json
接続文字列を設定してください（実行フォルダに配置）。
※このリポジトリでは安全のため接続情報をマスクしている場合があります。各自の環境に合わせて変更してください。
※PortfolioApp\bin\Debug\net8.0-windowsに配置

```json

{
  "ConnectionStrings": {
    "PortfolioDb": "Server=*******;Database=PortfolioDb;User Id=****;Password=****;TrustServerCertificate=True;"
  }
}

```

## デモ手順（サンプルCSVで確認）
1. SSMSで `sql/01_create_database.sql` → `sql/02_create_tables.sql` を実行
2. （必要なら）`sql/03_reset_data.sql` を実行してデータを初期化(データ全削除用のsql)
3. PortfolioApp\bin\Debug\net8.0-windows\MyWpfApp.exeからアプリを起動し、`sample/worklogs_sample.csv` をCSV取込（通常 or ドライラン）
4. 「期間を設定」ボタンで全期間をセット
5. 月別 / 案件別 / メンバー別 を取得し、表とグラフを確認

---

## トラブルシュート

- **DBに接続できない**
  - `appsettings.json` の接続文字列（Server/認証方式）を確認してください。
  - SQL Serverサービスが起動しているか確認してください。

- **DB削除ができない（"使用中なので削除できません"）**
  - SSMSで対象DBへの接続を切断してから削除してください。

- **スキップが増えて投入件数が増えない**
  - 重複キー `(WorkDate, Member, Project, WorkType)` が既にDBに存在するとスキップされます。
  - `import_errors_*.csv` を確認してください。

---

## 動作確認（手動テスト観点）

### 期間バリデーション
- [ ] From/To未入力で取得 → 右上Statusにエラー表示（赤）＆取得処理しない
- [ ] From > Toで取得 → 右上Statusにエラー表示（赤）＆取得処理しない

### 期間変更時の挙動
- [ ] From/To変更 → 全タブの表/グラフがクリアされ「未更新」状態になる
- [ ] 期間変更後 → CSV出力ボタンが無効化される

### 取得（正常系）
- [ ] 取得成功 → 表＋グラフが更新され、Statusが「更新済（時刻）」になる
- [ ] 取得中 → 二重実行されない（ボタン無効化/IsLoadingでガード）

### 0件
- [ ] 0件期間で取得 → ダイアログ表示（0件）＋空状態表示、CSV出力は無効のまま

### DataGrid
- [ ] 行数が多い場合 → 縦スクロールバーが表示される

### CSV取込（任意）
- [ ] ドライランON → DBに投入されない（実投入件数=0、ログとサマリは更新される）
- [ ] 必須欠落/型変換エラー → スキップログCSVに出力される
- [ ] 重複（ファイル内/DB） → スキップされ、投入件数が増えない

## リリース手順
1. Releaseビルド（フォルダ配布）

プロジェクト直下で実行：
dotnet publish .\PortfolioApp\MyWpfApp.csproj -c Release -r win-x64 --self-contained false

出力先（例）：
PortfolioApp/bin/Release/net8.0-windows/win-x64/publish/

2. appsettings.json を配置

配布先フォルダ（publishフォルダ）に appsettings.json を置いてください（exeと同じ階層）。
※appsettings.json は作成してください（テンプレは appsettings.template.json）
例：
.../publish/MyWpfApp.exe
.../publish/appsettings.json
※接続文字列は SQL認証 の値に差し替えてください（このリポジトリではマスクしています）。

3) 起動

publish フォルダ内の MyWpfApp.exe を実行します。