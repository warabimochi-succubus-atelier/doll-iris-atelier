# resvg

アプリは、虹彩の SVG（`SvgExporter` が作るもの）を [resvg](https://github.com/linebender/resvg) で画像にする（`DollIrisGenerator/Services/ResvgRasterizer.cs`）。設計は [docs/design/svg-canonical-rendering.md](../../docs/design/svg-canonical-rendering.md) を参照。

| ファイル | 内容 |
|---|---|
| `win-x64/resvg.dll` | resvg の C API（`crates/c-api`）。ビルドに使ったタグ・コミット・SHA-256 は `VERSION.txt` にある |
| `LICENSE-APACHE`, `LICENSE-MIT` | resvg のライセンス（Apache-2.0 または MIT。どちらかを選べる） |
| `THIRD-PARTY-LICENSES.txt` | DLL に入っている全ライブラリの著作権表示とライセンスの全文（cargo-about で作る。設定は `about/`） |
| `DEPENDENCIES.txt` | DLL に入っているライブラリとライセンスの一覧（`cargo tree`） |

## 作り直し方

1. GitHub の Actions で「Build resvg」を手動で実行する（入力: resvg のタグ）。ワークフローか `about/` を変えて push したときにも動く。
2. できたアーティファクト `resvg-win-x64` をこのフォルダに展開する。
   ```bash
   gh run download <run-id> -n resvg-win-x64 -D third_party/resvg
   ```
3. テストを通す（`ResvgRasterizerTests`）。

ビルドは毎回同じバイト列になるとは限らない（同じタグでも SHA-256 が変わることがある）。DLL と `VERSION.txt` は、必ず同じ実行のアーティファクトのものをそろえて置く。

文字・フォント・SVGZ は使わないので外してビルドしている（ラベルは WPF で描く）。埋め込み画像（星雲の PNG）を描くための `raster-images` は入れている。Visual C++ のランタイム DLL が要らないように、静的リンクでビルドしている。
