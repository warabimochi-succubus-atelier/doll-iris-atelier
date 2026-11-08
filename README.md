# Doll Iris Atelier (ドール用虹彩生成・実寸出力ツール) v0.10.0

## Overview (English)

**Doll Iris Atelier** is a Windows desktop app (WPF / .NET 10) for designing anime-style irises for doll eyes — ball-jointed dolls (SD / DD / MDD), Obitsu, resin and glass-cabochon eyes — and printing them at exact physical size.

- **Parametric, not AI**: every iris is drawn from geometry and a random seed. The same parameters and seed always give the same result. It works fully offline.
- **Real millimetres**: you set the iris diameter (4–20 mm). The A4 PDF is accurate to ±0.1 mm and has a 1 cm scale on each page, so you can check the print with a ruler.
- **Rich design controls**: color presets and gradients (linear, radial, conic), pupil shapes (round, cat slit, heart, star, gem cut), radial fibers, limbal ring, glitter, aurora and nebula layers, lens-style rings, decorative symbols, highlights, oval / tilted irises, and odd eyes. The right eye is always the mirror image of the left.
- **Compare side by side**: split the preview into up to 9 cells, each with its own design and its own undo history.

### Basic workflow

The app is built around two buttons in the top toolbar:

1. **Design** an iris in the settings tabs (or press ✨ *Randomize attributes* for ideas).
2. **⭐ Add to favorites** — keeps the design. Favorites are saved automatically and stay after you restart the app. The design you are editing is *not* saved on its own.
3. **🖨️ Add to A4 sheet** — puts the design on the print sheet. You can also select several favorites in the *History / Favorites* tab and add them all at once.
4. **📄 Export A4 PDF** and print it at **actual size (100%)**.

PNG (600 / 1200 DPI) and SVG export are also available for use in other tools. The user interface is in Japanese. See [USER_MANUAL.md](USER_MANUAL.md) for the full manual and [SPECIFICATION.md](SPECIFICATION.md) for the specification (both in Japanese).

---

## 概要

アニメ調のドールアイ用虹彩を、パラメータと乱数シードから自動生成し、A4 印刷用 PDF（±0.1mm 精度）・PNG・SVG に実寸で書き出す Windows 向けデスクトップアプリケーションです。詳しい仕様は [SPECIFICATION.md](SPECIFICATION.md) を参照してください。

### 基本の流れ
1. 設定タブで虹彩を作る（迷ったら **✨ 属性ランダム生成**）
2. 気に入ったら **⭐ お気に入りに追加** で残す（編集中のデザインは自動保存されません）
3. 印刷したいデザインを **🖨️ A4シートに追加**（お気に入りからまとめて追加することもできます）
4. **📄 A4 PDF 出力** で書き出し、「実際のサイズ（100%）」で印刷

---

## 🌟 主な機能と Section 20 拡張機能対応

### 1. 楕円虹彩対応 (Support for Elliptical Irises)
- 正円（Circle）、縦長楕円（Vertical Oval）、横長楕円（Horizontal Oval）に対応。
- 楕円の縦横比率（0.60〜2.00）を細かく制御可能。
- **楕円チルト（傾き角度）対応**: `-45°〜+45°` のチルト角度を指定可能。右目は自動的に鏡像チルト（対称角度）に同期します。

### 2. レンズ風パターン対応 (Lens-Style Patterns)
- カメラレンズやフレネルレンズのような同心円状の微細屈折溝（Fresnel Rings）を合成。
- 屈折リング数（2〜20本）、リング線幅、線色、濃度、中心屈折光（コースティクスグロー）をパラメータ調整可能。

### 3. オッドアイテンプレート対応 (Odd-Eye Templates)
- 左右異色瞳（オッドアイ）のワンクリックテンプレートを搭載：
  - サファイア ＆ ゴールド (蒼金)
  - ルビー ＆ サファイア (紅蒼)
  - アメジスト ＆ エメラルド (紫翠)
  - ローズピンク ＆ シルバー (桜銀)
  - アンバー ＆ キャットグリーン (猫目双色)
- 右目専用のベース色・内周色・外周色・ライン色・反射光色の個別指定に対応（サイズ・形状・位置整合性は完全同期）。

### 4. 虹彩プリセット共有 (Shared Iris Presets)
- 現在のデザインの見た目（サイズ・形状・傾き・シード・オッドアイ以外のすべて。色・瞳孔・放射ライン・ラメ・外輪・オーロラ層・星雲・ハイライト・装飾記号・レンズ風）をワンクリックで新規プリセット登録可能。
- **プリセットのエクスポート & インポート**: プリセットを JSON ファイル（`*.irispalette.json`）として書き出し・読み込みでき、他のドール作家との共有が可能。

### 5. SVG 出力 (SVG Export)
- 現在のパラメータから W3C SVG ベクターデータを生成し、Illustrator・Inkscape など他のツール用に出力（アプリでの SVG の読み込み・編集には対応しません）。
- 上部ツールバーの「💾 SVG 保存」でファイル書き出し、「📋 SVG コピー」でクリップボードへコピー。

### 6. PDF テンプレート追加 (Addition of PDF Templates)
- 用途やドール規格に応じた 6 種類の専用レイアウトテンプレートを搭載：
  1. **標準レイアウト (自動面付け)**: サイズと余白から最適な配置数を自動計算。
  2. **1/3 ドール向けシート (SD / DD / 虹彩 10〜12mm)**: レジン盛りやカボション作業用の余白を広く取った 4列×8行。
  3. **1/4 ドール向けシート (MDD / 虹彩 8〜10mm)**: 5列×10行の高密度配置。
  4. **1/6 ドール向けシート (オビツ / 虹彩 5〜7mm)**: 8列×13行の大容量配置。
  5. **クラフトパンチ専用グリッド (穴あけ器用)**: 市販の小型円形パンチ（虹彩 12mm まで）の挿入口に合わせた等間隔配置（4列×7行）。
  6. **カラー見本・試作用シート**: 光沢写真用紙等でのプリンター発色確認用。

### 7. 生成履歴管理 (Generation History Management)
- 変更操作やシード更新の履歴を自動タイムライン記録。
- **Undo (元に戻す) / Redo (やり直す)** を完備。
- 過去の任意の生成ステートをワンクリックで瞬時に復元。

### 8. お気に入り登録 (Ability to Save Favorites)
- 気に入った虹彩デザインを「⭐ お気に入りに追加」ボタンでワンクリック保存（自動で保存され、次回起動時も残ります）。
- お気に入り一覧パネルからベース色・名前・日時を確認してワンクリックで復元。複製・削除も可能。
- 複数のお気に入りを選んで、まとめて A4 シートに追加可能（すでにシートにあるデザインは数量 +1）。
- お気に入り一覧のインポート／エクスポート（`*.irisfav.json`）に対応。デザイン単体の JSON もインポート可能。

---

## 📖 ユーザーマニュアル

操作方法は [USER_MANUAL.md](USER_MANUAL.md) を参照してください。

---

## 🚀 起動方法

### 実行ファイルを直接起動
```powershell
.\DollIrisGenerator\bin\Release\net10.0-windows\DollIrisGenerator.exe
```

### CLI から起動
```powershell
dotnet run --project .\DollIrisGenerator
```

---

## 🧪 テストの実行

```powershell
dotnet test .\DollIrisGenerator.Tests
```
`main` へのプルリクエストでは、同じテストが GitHub Actions でも自動実行されます。主な対象: 楕円チルト、レンズ風パターン、オッドアイ、パレット共有、PDFテンプレート、履歴・Undo/Redo（スライダー操作を含む）、お気に入り、JSON復元、SVG XML妥当性、A4 PDF生成（面付け・改ページ・異なるサイズの混在・日本語ラベル）、PNG/PDF画像の描画内容、傾けた楕円の出力とカットライン、線幅の実寸連動、1cm スケール、用紙からのはみ出し検出、±0.1mm精度計算、読み込みファイルの安全性（サイズ上限・値の範囲）、お気に入りのデータ保護。

---

## 🛠️ ビルドと配布

必要なもの: Windows、.NET 10 SDK（バージョンは `global.json` を参照）

### ビルド
```powershell
dotnet build .\DollIrisGenerator --configuration Release
```

デザインの計算と SVG 出力は WPF に依存しない `DollIrisGenerator.Core`（`net10.0`）にまとめてあり、アプリはそれを参照します（構成は [SPECIFICATION.md](SPECIFICATION.md) の 2.3）。

### 配布用ファイルの作成
.NET のインストールが不要な形式です。出力された `dist\DollIrisAtelier` フォルダーごと配布します。
```powershell
dotnet publish .\DollIrisGenerator --configuration Release --runtime win-x64 --self-contained true --output .\dist\DollIrisAtelier
```

配布先に .NET 10 デスクトップランタイムがある場合は、`--self-contained false` でサイズを小さくできます。

### バージョンの記録
配布したコミットにはタグを付けると、どの時点のものか後から分かります。
```powershell
git tag v0.10.0
git push origin v0.10.0
```

---

## 📱 ブラウザ版 (PWA・試作)

スマホやタブレットのブラウザで虹彩をデザインし、デスクトップ版で印刷するための版です（`DollIrisGenerator.Web`、Blazor WebAssembly）。
デザインの計算はデスクトップ版と同じ `DollIrisGenerator.Core` を使うので、同じパラメータからは同じ模様になります。

- **できること**: パラメータ編集とプレビュー、虹彩プリセット・オッドアイテンプレート、ランダム生成、元に戻す／やり直す、お気に入り（ブラウザ内に保存）
- **デスクトップ版への受け渡し**: 「お気に入り」タブで `.irisfav.json` を書き出し、デスクトップ版の「履歴・お気に入り」タブの「📥 インポート」で読み込みます。デスクトップ版で書き出したファイルもブラウザ版で読み込めます
- **できないこと**: A4 PDF・PNG・SVG の出力（印刷はデスクトップ版で行います）
- 一度開けばオフラインでも動きます。外部への通信はしません（アプリ自身の更新の確認だけ）。お気に入りはこの端末のブラウザにだけ保存されるので、残したいものは書き出しておいてください

### ローカルで起動
```powershell
dotnet run --project .\DollIrisGenerator.Web
```
表示された URL（既定は http://localhost:5062）をブラウザで開きます。

### 配布用ファイルの作成
発行では .NET のコードを WebAssembly に事前コンパイル（AOT）します。初回だけ wasm-tools ワークロードを入れてください（`dotnet build` / `dotnet run` には不要です）。
```powershell
dotnet workload install wasm-tools
```
```powershell
dotnet publish .\DollIrisGenerator.Web --configuration Release --output .\dist\web
```
発行には数分かかります。プレビューの再描画は AOT なしの約 1.6 倍（星雲の計算は約 4 倍）速くなり、代わりにダウンロードは約 2.3 倍（Brotli 圧縮で約 2.1 MB → 約 4.9 MB）になります。

`dist\web\wwwroot` の中身を静的ファイルとして HTTPS で公開します。サブフォルダー（GitHub Pages の `https://<ユーザー>.github.io/<リポジトリ>/` など）に置く場合は、`index.html` の `<base href="/" />` をそのフォルダー（例: `/doll-iris-atelier/`）に書き換えてください。

---

## 📄 ライセンス

[MIT License](LICENSE) のもとで公開しています。ただし、ロゴとアイコンは対象外です。詳しくは [NOTICE.md](NOTICE.md) を参照してください。
