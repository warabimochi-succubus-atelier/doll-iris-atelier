# Doll Iris Atelier（ドール用虹彩生成・実寸出力ツール）

アニメ調のドールアイ用の虹彩をパラメータと乱数シードから作り、A4 印刷用 PDF（±0.1mm 精度）・PNG・SVG に実寸で書き出す Windows 用のアプリです。描画はすべてアプリ内の数式で行い、画像生成 AI は使いません。通信もしません。

このファイルは、リリースの zip に入れる利用者向けの説明です。ソースコード・ビルド方法・仕様は GitHub の [リポジトリ](https://github.com/warabimochi-succubus-atelier/doll-iris-atelier) にあります。

> **English**: Doll Iris Atelier designs anime-style irises for doll eyes and prints them at exact size (A4 PDF, PNG, SVG). Unzip and run `DollIrisGenerator.exe` (desktop app) or `DollIrisCli.exe` (command line). No .NET install is needed (Windows 10 / 11, 64-bit). If SmartScreen warns you, choose *More info* → *Run anyway*. The manuals are in Japanese.

---

## zip の中身

| ファイル | 内容 |
|---|---|
| `DollIrisGenerator.exe` | デスクトップ版（`DollIrisAtelier-vX.Y.Z-win-x64.zip` のみ） |
| `DollIrisCli.exe` | コマンドライン版（`DollIrisAtelier-vX.Y.Z-cli-win-x64.zip` のみ） |
| `README.md` | このファイル |
| [`USER_MANUAL.md`](USER_MANUAL.md) | 使い方の説明書（コマンドライン版は 5 章） |
| [`docs/DESIGN_JSON_FORMAT.md`](docs/DESIGN_JSON_FORMAT.md) | デザインファイル（JSON）の書き方 |
| [`LICENSE`](LICENSE)・[`NOTICE.md`](NOTICE.md) | ライセンスと、ロゴ・アイコンの権利、組み込んでいるソフトウェアについて |
| `third_party/resvg/` | exe に組み込んでいる resvg と、その使うライブラリのライセンス |

## 動作環境

- Windows 10 / 11（64 bit 版）
- .NET などの追加インストールは不要です（exe 1 つで動きます）

## デスクトップ版の起動

1. zip を右クリック →「すべて展開」で好きな場所に展開します
2. 展開したフォルダーの `DollIrisGenerator.exe` をダブルクリックします

- 「Windows によって PC が保護されました」と表示されたら、「詳細情報」→「実行」を押してください（署名していないアプリのため表示されます）
- 新しい版に更新するときは、新しい zip を展開し直します。お気に入りや設定は exe とは別の場所（`%APPDATA%\DollIrisAtelier`）に保存されているので、そのまま引き継がれます
- 使い方は [USER_MANUAL.md](USER_MANUAL.md) にあります。アプリの中では **F1** キーでマニュアル（GitHub 上の最新版）を開けます

## コマンドライン版（DollIrisCli）

デザインファイル（JSON）のひな形作りと検査、SVG・PNG・A4 PDF の一括書き出しをコマンドで行えます。出力はデスクトップ版と同じになります。展開したフォルダーで PowerShell などを開いて使います。

```powershell
.\DollIrisCli.exe template --preset サクラ -o sakura.json
.\DollIrisCli.exe validate sakura.json
.\DollIrisCli.exe svg favorites.irisfav.json -o .\svg
.\DollIrisCli.exe pdf favorites.irisfav.json --template 1-4
```

| コマンド | すること |
|---|---|
| `template` | 全項目がそろったデザインの JSON（ひな形）を書き出す。`--preset 名前` でプリセットを当てはめ、`--list` でプリセットの一覧を出す |
| `validate` | アプリで読み込んだときと同じように読み、書いたとおりにならない所（範囲外の値・知らない項目名・読めない色など）を一覧にする |
| `svg` | SVG を書き出す（お気に入りの一覧なら 1 件ずつ） |
| `png` | 透過 PNG を書き出す（既定 1200 DPI。`--dpi 600` などで変更） |
| `pdf` | A4 PDF を書き出す。`--template` は `standard`・`1-3`・`1-4`・`1-6`・`punch`・`trial` |

- `-o` で出力先、`--force` で上書き（付けないと、すでにあるファイルは上書きしません）
- `svg`・`png` は `--guides` でカットラインとセンターマーク、`--spacing 5` で左右の間隔（mm）
- `pdf` は `--pairs 3` でデザインごとのセット数、`--fill` で最後のページの空きを埋める。用紙からはみ出す配置のときは書き出しません（`--allow-overflow` で書き出す）
- 終了コード: **0** = 成功、**1** = `validate` で警告あり、**2** = 読み込めない・書き出せない
- すべての指定は `.\DollIrisCli.exe --help` で表示できます。詳しくは [USER_MANUAL.md](USER_MANUAL.md) の 5 章、JSON の書き方は [docs/DESIGN_JSON_FORMAT.md](docs/DESIGN_JSON_FORMAT.md) を見てください

## 印刷するとき

PDF は必ず「実際のサイズ（100%）」で、用紙 A4 で印刷してください。

## ブラウザ版

スマホ・タブレット・PC のブラウザで虹彩をデザインできます（インストール不要）: <https://warabimochi-succubus-atelier.github.io/doll-iris-atelier/>
お気に入りを `.irisfav.json` で書き出し、デスクトップ版やコマンドライン版で読み込んで印刷します。

## 不具合の報告・最新版

- 最新版: <https://github.com/warabimochi-succubus-atelier/doll-iris-atelier/releases>
- 不具合の報告: <https://github.com/warabimochi-succubus-atelier/doll-iris-atelier/issues>

## ライセンス

[MIT License](LICENSE) のもとで公開しています。ただし、ロゴとアイコンは対象外です。詳しくは [NOTICE.md](NOTICE.md) を参照してください。
