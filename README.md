# Doll Iris Atelier (ドール用虹彩生成・実寸出力ツール) v1.0 Plus

仕様書 `#_Automated Iris Generator for Dolls_Specification_v1_0.txt` の全基本要件、および **Section 20「将来の拡張機能」全項目** に完全対応した、Windows 11 向けのアニメ調ドールアイ用虹彩パラメトリック自動生成デスクトップアプリケーションです。

---

## 🌟 主な機能と Section 20 拡張機能対応

### 1. 楕円虹彩対応 (Support for Elliptical Irises)
- 正円（Circle）、縦長楕円（Vertical Oval）、横長楕円（Horizontal Oval）に対応。
- 楕円の縦横比率（0.50〜2.50）を細かく制御可能。
- **楕円チルト（傾き角度）対応**: `-45°〜+45°` のチルト角度を指定可能。右目は自動的に鏡像チルト（対称角度）に同期します。

### 2. レンズ風パターン対応 (Lens-Style Patterns)
- カメラレンズやフレネルレンズのような同心円状の微細屈折溝（Fresnel Rings）を合成。
- 屈折リング数（2〜24本）、リング線幅、線色、濃度、中心屈折光（コースティクスグロー）をパラメータ調整可能。

### 3. オッドアイテンプレート対応 (Odd-Eye Templates)
- 左右異色瞳（オッドアイ）のワンクリックテンプレートを搭載：
  - サファイア ＆ ゴールド (蒼金)
  - ルビー ＆ サファイア (紅蒼)
  - アメジスト ＆ エメラルド (紫翠)
  - ローズピンク ＆ シルバー (桜銀)
  - アンバー ＆ キャットグリーン (猫目双色)
- 右目専用のベース色・内周色・外周色・ライン色・反射光色の個別指定に対応（サイズ・形状・位置整合性は完全同期）。

### 4. 色プリセット共有 (Shared Color Presets)
- 現在のカラー設定をワンクリックで新規プリセット登録可能。
- **パレットのエクスポート & インポート**: カラーパレットを JSON ファイル（`*.irispalette.json`）として書き出し・読み込みでき、他のドール作家との共有が可能。

### 5. SVG 出力 (SVG Export)
- 現在のパラメータから W3C SVG ベクターデータを生成し、Illustrator・Inkscape など他のツール用に出力（アプリでの SVG の読み込み・編集には対応しません）。
- 上部ツールバーの「💾 SVG 保存」でファイル書き出し、「📋 SVG コピー」でクリップボードへコピー。

### 6. PDF テンプレート追加 (Addition of PDF Templates)
- 用途やドール規格に応じた 6 種類の専用レイアウトテンプレートを搭載：
  1. **標準レイアウト (自動面付け)**: サイズと余白から最適な配置数を自動計算。
  2. **1/3 ドール向けシート (SD / DD / 虹彩 10〜12mm)**: レジン盛りやカボション作業用の余白を広く取った 4列×8行。
  3. **1/4 ドール向けシート (MDD / 虹彩 8〜10mm)**: 5列×10行の高密度配置。
  4. **1/6 ドール向けシート (オビツ / ピュアニーモ 虹彩 5〜7mm)**: 8列×13行の大容量配置。
  5. **クラフトパンチ専用グリッド (穴あけ器用)**: 市販の小型円形パンチ（虹彩 12mm まで）の挿入口に合わせた等間隔配置（4列×7行）。
  6. **カラー見本・試作用シート**: 光沢写真用紙等でのプリンター発色確認用。

### 7. 生成履歴管理 (Generation History Management)
- 変更操作やシード更新の履歴を自動タイムライン記録。
- **Undo (元に戻す) / Redo (やり直す)** を完備。
- 過去の任意の生成ステートをワンクリックで瞬時に復元。

### 8. お気に入り登録 (Ability to Save Favorites)
- 気に入った虹彩デザインを「⭐ お気に入りに追加」ボタンでワンクリック保存。
- お気に入り一覧パネルからサムネイル色・名前・日時を確認してワンクリックで復元。
- お気に入り一覧のインポート／エクスポート（`*.irisfav.json`）に対応。

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

### 配布用ファイルの作成
.NET のインストールが不要な形式です。出力された `dist\DollIrisAtelier` フォルダーごと配布します。
```powershell
dotnet publish .\DollIrisGenerator --configuration Release --runtime win-x64 --self-contained true --output .\dist\DollIrisAtelier
```

配布先に .NET 10 デスクトップランタイムがある場合は、`--self-contained false` でサイズを小さくできます。

### バージョンの記録
配布したコミットにはタグを付けると、どの時点のものか後から分かります。
```powershell
git tag v1.0.0
git push origin v1.0.0
```

---

## 📄 ライセンス

[MIT License](LICENSE) のもとで公開しています。ただし、ロゴとアイコンは対象外です。詳しくは [NOTICE.md](NOTICE.md) を参照してください。
