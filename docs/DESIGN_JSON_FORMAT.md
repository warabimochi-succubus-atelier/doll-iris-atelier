# デザインファイル（JSON）の書き方

Doll Iris Atelier のデザインは、すべて **数値と色を並べた JSON ファイル** で表せます。このページは、その形式（項目・範囲・書き方）の説明です。

- 絵を描くのは常にアプリ内の数式です。ファイルに入っているのは数値と色だけで、同じファイルなら何度読み込んでも同じ目になります。**画像生成 AI は使いません。**
- アプリはファイルを読み込むだけで、通信はしません。
- 手で書く、スクリプトで量産する、AI チャットに書いてもらうなど、どんな方法で作ったファイルでも同じように読み込めます（[6. 作り方の例](#6-作り方の例)）。
- 古いバージョンのアプリでは、新しい項目（重ね方・繊維ノイズなど）が無視されることがあります。最新版で使ってください。

---

## 1. 読み込み方

ファイルは **UTF-8** で保存します。

- **デスクトップ版**: 「履歴・お気に入り」タブの **📥 インポート** でファイルを選ぶ → お気に入りに追加されるので「復元」で編集中のデザインにする
- **ブラウザ版**: お気に入りの「ファイルを読み込む」でファイルを選ぶ → 「復元」

アプリで作ったデザインを JSON にしたいときは、お気に入りに追加して **📤 エクスポート** で `*.irisfav.json` として書き出します（形は [2. ファイルの形](#2-ファイルの形) の「複数のデザイン」）。

---

## 2. ファイルの形

### デザイン 1 つ（`〇〇.json`）

[5. テンプレート](#5-テンプレート) の形のオブジェクトをそのまま書きます。

### 複数のデザイン（`〇〇.irisfav.json`）

配列にし、各要素の `Parameters` にデザイン（テンプレートと同じ形）を入れます。

```json
[
  { "Name": "案1", "Note": "", "Parameters": { "Name": "案1", "SizeMm": 10.0, "...": "..." } },
  { "Name": "案2", "Note": "", "Parameters": { "Name": "案2", "SizeMm": 10.0, "...": "..." } }
]
```

---

## 3. 書き方のルール

### JSON の約束

- コメント（`//` や `/* */`）、末尾のカンマ、`NaN`、`Infinity` は書けません。
- 項目名は [5. テンプレート](#5-テンプレート) と大文字・小文字まで同じにします（`raycount` のように違う名前は無視されます）。
- 書かなかった項目はアプリの既定値になります。意図しない見た目を避けるため、テンプレートの全項目を書くことを勧めます。
- 選択肢（形の種類など）は文字ではなく数値で書きます。真偽値は `true` / `false`。
- 単色は `"#RRGGBB"`（例 `"#2979FF"`）。色名（`"blue"`）や `rgb()` は使えません。
- 値は [4. 項目一覧](#4-項目一覧) の範囲に収めます（範囲外はアプリが範囲内に丸めますが、意図とずれます）。
- `Name` にはデザイン名を入れます。

### 座標と向き

- 位置 X / Y は虹彩の半径を 1 とした値です。X は右がプラス、Y は **下がプラス**（上は -）。中心が 0, 0。
- 書くのは左目です。右目はアプリが自動で左右反転して作ります（傾き・渦巻きの向き・記号も鏡像）。
- 角度: 光の向き・円周グラデーションは 0° = 右、90° = 下、270° = 上。直線グラデーションは 0° = 左→右、90° = 上→下。
  - **CSS とは角度の基準が違います。** CSS は `linear-gradient` が 0deg = 下→上・90deg = 左→右、`conic-gradient` が 0deg = 上から始まります。CSS の角度をそのまま書くと 90° ずれるので、直線の角度も円周の `from` も「CSS の角度 − 90」（マイナスになったら 360 を足す）にしてください。
- 不透明度（`〜Opacity`）は **1 = 不透明、0 = 見えない** です。アプリの画面の「透明度」とは向きが逆で、画面の透明度 X% は `Opacity` = 1 − X/100 になります（透明度 15% → 0.85）。
- ハイライトは左上（X, Y がともにマイナス）に置くと自然です。

### 色のグラデーション（「グラデ可」の項目だけ）

単色の代わりに次の文字列が使えます。色は 2〜5 個、位置は 0%〜100%。色に `"#AARRGGBB"`（先頭 2 桁が不透明度）も使えます。

| 種類 | 書き方の例 | 注意 |
|---|---|---|
| 直線 | `"linear-gradient(90deg, #FFFFFF 0%, #80D8FF 50%, #0D47A1 100%)"` | |
| 放射 | `"radial-gradient(#FF4081 0%, #1A0033 100%)"` | 角度は書かない |
| 円周 | `"conic-gradient(from 270deg, #FF80AB 0%, #80D8FF 50%, #FF80AB 100%)"` | 一周させるなら最初と最後を同じ色に |

「グラデ可」でない項目に書くと、最初の色だけが使われます。

### 重ね方（`AuroraBlend` / `GlitterBlend` / `ToneBandBlend` / `CollaretteBlend` / 各記号・ハイライトの `Blend`）

| 値 | 重ね方 |
|---|---|
| 0 | 通常 |
| 1 | スクリーン（明るくなるだけ・発光） |
| 2 | 覆い焼き（強い発光） |
| 3 | オーバーレイ |
| 4 | ソフトライト |

---

## 4. 項目一覧

範囲の「比率」は虹彩の半径に対する割合です。

### 基本

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `Name` | 文字列 | デザイン名 |
| `SizeMm` | 4.0〜20.0 | 虹彩（色の部分）の直径 mm。アイ全体ではない。目安: アイ 18mm→9.0、20mm→10.0、22mm→11.0、24mm→12.0 |
| `Shape` | 0 正円, 1 縦長楕円, 2 横長楕円 | 虹彩の形 |
| `AspectRatio` | 0.6〜2.0 | Shape 1 は高さ = SizeMm × この値、Shape 2 は幅 = SizeMm × この値。Shape 0 では使わない（1.0 にする）。自然なのは 1.05〜1.3 |
| `TiltAngle` | -45〜45（度） | 楕円の傾き。プラスで時計回り（左目）。右目は自動で逆向き |
| `OutlineTaper` | -1〜1 | 輪郭の上下の幅の差。プラスで上が広く下がすぼむ（卵形・盾形）、マイナスで下が広い。0 で楕円のまま |
| `OutlineTopFlat` | 0〜1 | 輪郭の上の端を平らにする（角は丸いまま）。0 でオフ |
| `OutlineBottomFlat` | 0〜1 | 輪郭の下の端を平らにする。0 でオフ |
| `OutlineSquareness` | 0〜1 | 楕円から角の丸い四角に近づける。0 でオフ。輪郭の 4 項目がすべて 0 なら楕円 |
| `Seed` | 0 以上の整数 | 放射ライン・ラメ・星雲などの乱れ方が変わる |

### 虹彩の地の色（単色のみ。グラデ不可）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `BaseColor` | 色 | 中間の色（主役の色） |
| `InnerColor` | 色 | 中心側の色（明るめにすると透明感が出る） |
| `OuterColor` | 色 | 外周側の色（暗めにすると締まる） |
| `InnerGradientRadius` | 0.1〜0.9 | 内周色の広がり |
| `OuterGradientRadius` | 0.3〜1.2 | 外周色が始まる位置 |
| `BottomHighlightColor` | 色 | 下側の照り返しの色 |
| `BottomHighlightIntensity` | 0〜1 | 照り返しの強さ |
| `LightStrength` | 0〜1 | 明るい場所を光の向きへずらす（0 でオフ） |
| `LightAngle` | 0〜360 | 光の来る向き（270 = 上、225 前後 = 左上） |
| `LightRightEyeFlip` | 0 鏡像, 2 反転しない（省略時 0） | 右目で光の向きを左右反転するか。2 にすると両目で同じ向きから光が入る（ハイライトも 2 にすると、光源が固定された自然な光） |

### オーロラ層（虹彩全体に重ねる色）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `AuroraColor` | 色（グラデ可） | 円周グラデにするとオーロラ風 |
| `AuroraOpacity` | 0〜1 | 0 でオフ。0.2〜0.5 程度が使いやすい |
| `AuroraBlend` | 重ね方 | |

### 星雲（雲のような模様。宇宙・銀河系に）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `NebulaColor` | 色（グラデ可） | |
| `NebulaOpacity` | 0〜1 | 0 でオフ |
| `NebulaDensity` | 0〜1 | 雲の量 |
| `NebulaScale` | 0.5〜3.0 | 大きいほど細かい |
| `NebulaSwirl` | -1〜1 | 渦巻き（マイナスで逆回り） |

### 繊維ノイズ（本物の目のような細かい繊維）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `FiberNoise` | 0〜1 | 0 でオフ。0.15〜0.45 が自然 |
| `FiberNoiseScale` | 0.5〜2.0 | 大きいほど細かい |

### 色の段（瞳孔の縁から外周までの同心の色の帯。アニメ塗りの段階）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `ToneBandIntensity` | 0〜1 | 強さ（不透明度）。0 でオフ（既定） |
| `ToneBandCount` | 2〜5 | 帯の数（既定 3） |
| `ToneBandInnerColor` | 色（単色） | いちばん内側の帯の色（既定 `#FFFFFF`） |
| `ToneBandOuterColor` | 色（単色） | いちばん外側の帯の色（既定 `#000000`）。間の帯は等分した色 |
| `ToneBandSoftness` | 0〜1 | 境目のぼかし。0 でくっきり（既定 0.1） |
| `ToneBandBlend` | 重ね方 | 既定 4（ソフトライト）。白 → 黒のまま使うと、虹彩の色を残して内側を明るく・外側を暗くする。0（通常）で帯の色で塗る |

### 瞳孔

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `PupilShape` | 0 正円, 2 猫目スリット, 3 ハート, 4 5芒星（星）, 5 4芒星, 6 三日月, 7 クロス, 8 ダイヤ, 9 桜の花びら, 10 蝶々 | 装飾記号と同じ形の一覧（番号は記号・ハイライトと違う）。1 は使わない（以前の縦長楕円。正円として読み込まれる）。縦長の瞳孔は正円の上下左右の長さで作る（左右 0.75・上下 1.25 が以前の縦長楕円） |
| `PupilSizeRatio` | 0.10〜0.60 | 瞳孔の直径の比率（0.25〜0.40 が一般的） |
| `PupilColor` | 色（グラデ可） | 通常は黒に近い色 |
| `PupilOpacity` | 0〜1 | 1 で不透明 |
| `PupilSoftness` | 0〜0.30 | 輪郭のぼかし |
| `PupilCornerRounding` | 0〜1 | 猫目・ハート・星などの角の丸み |
| `PupilStarCoreScale` | 0.5〜2.0（省略時 1.0） | 5芒星・4芒星の瞳孔の芯（くびれ）の太さ |
| `PupilStretchTop`, `PupilStretchBottom`, `PupilStretchLeft`, `PupilStretchRight` | 0.5〜2.0（省略時 1.0） | 瞳孔の上下左右の長さ（装飾記号と同じ）。右目は左右が入れ替わり鏡像になる |
| `PupilRightEyeFlip` | 0 鏡像, 2 反転しない（省略時 0） | 右目の瞳孔を左右反転するか（三日月の向き・左右の長さ・宝石カット） |
| `PupilFacetIntensity` | 0〜1 | 宝石のカット面（0 でオフ） |
| `PupilFacetCount` | 4〜24 | カット面の数 |

### 瞳孔まわりの輪（瞳孔の縁から外へ広がるギザギザの色の輪）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `CollaretteIntensity` | 0〜1 | 強さ（不透明度）。0 でオフ（既定） |
| `CollaretteColor` | 色（グラデ可） | 既定 `#FFD54F`（金色）。放射グラデーションは瞳孔の縁 → 輪の外縁 |
| `CollaretteWidth` | 0.05〜0.50 | 瞳孔の縁から輪の外縁（山の先）までの幅の比率（既定 0.20）。瞳孔の大きさ + 幅は 0.95 までに抑えて描く |
| `CollaretteJagged` | 0〜1 | 外縁の谷の深さ。0 で円（既定 0.5） |
| `CollaretteSpikeCount` | 8〜48 | 外縁の山の数（既定 20） |
| `CollaretteSoftness` | 0〜1 | 外縁のぼかし。0 でくっきり（既定 0.4） |
| `CollaretteBlend` | 重ね方 | 既定 0（通常）。1（スクリーン）で光るような輪 |

### 放射ライン（瞳孔から外へ伸びる線）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `RayCount` | 0〜120 | 0 でなし。画面のスライダーは偶数刻みですが、奇数を書いてもその本数で描きます（スライダーを動かすと偶数にそろいます） |
| `RayColor` | 色（グラデ可） | |
| `RayThickness` | 0.5〜4.0 | 線の太さ |
| `RayLengthRatio` | 0.30〜0.95 | 線の長さ |
| `RayInnerRadiusRatio` | 0.1〜0.8 | 線の根元の位置（通常は瞳孔より少し外の 0.26〜0.40） |
| `RayIntensity` | 0〜1 | 濃さ |
| `RayRandomness` | 0〜1 | 長さ・向きの乱れ |
| `RayTaper` | 0〜1 | 先細り |
| `RayWidthVariation` | 0〜1 | 太さのばらつき |
| `RayCurve` | -1〜1 | うねり・渦巻き（マイナスで逆回り） |
| `RayFineLayer` | 0〜1 | 細い線の層の濃さ |
| `RayRangeAngle` | 0〜360（度） | 見える範囲の向き。0 = 右、90 = 下、270 = 上。右目は自動で鏡像 |
| `RayRangeWidth` | 0〜360（度） | 見える範囲の広さ。360 で全体（既定）、180 で半分 |
| `RayRangeFeather` | 0〜90（度） | 見える範囲の端のぼかし（既定 30） |

### 外輪（虹彩のふちの濃い輪）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `LimbalRingThickness` | 0〜0.22 | 太さ（0 でなし） |
| `LimbalRingColor` | 色（グラデ可） | 外周色より暗い色が基本 |
| `LimbalRingOpacity` | 0〜1 | |
| `LimbalRingSoftness` | 0〜0.80 | 内側の境界のぼかし |

### ラメ（粒子）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `ParticleCount` | 0〜250 | 0 でなし |
| `ParticleColor` | 色（グラデ可） | |
| `ParticleMinSize` | 0.2〜ParticleMaxSize | 最小の粒の大きさ |
| `ParticleMaxSize` | 0.5〜5.0 | 最大の粒の大きさ |
| `ParticleOpacity` | 0〜1 | |
| `ParticleRadiusRatio` | 0.30〜0.95 | ラメが散らばる範囲（外周までの比率） |
| `ParticleGlow` | 0〜1 | 粒の光のにじみ |
| `ParticleFlareRatio` | 0〜1 | 十字に光る粒の割合 |
| `ParticleFlareLength` | 1〜8 | 十字の光の長さ |
| `GlitterBlend` | 重ね方 | |
| `ParticleRangeAngle` | 0〜360（度） | 見える範囲の向き。0 = 右、90 = 下、270 = 上。右目は自動で鏡像 |
| `ParticleRangeWidth` | 0〜360（度） | 見える範囲の広さ。360 で全体（既定）、180 で半分 |
| `ParticleRangeFeather` | 0〜90（度） | 見える範囲の端のぼかし（既定 30） |

### レンズ風パターン（同心円の屈折リング）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `LensPatternEnabled` | true / false | |
| `LensRingCount` | 2〜20 | |
| `LensRingColor` | 色（グラデ可） | |
| `LensRingIntensity` | 0〜1 | |
| `LensRingWidth` | 0.3〜3.0 | |
| `LensRefractionGlow` | 0〜1 | 中心の屈折光 |
| `LensRangeAngle` | 0〜360（度） | リングの見える範囲の向き。0 = 右、90 = 下、270 = 上。右目は自動で鏡像 |
| `LensRangeWidth` | 0〜360（度） | 見える範囲の広さ。360 で全体（既定）、180 で半分 |
| `LensRangeFeather` | 0〜90（度） | 見える範囲の端のぼかし（既定 30） |

### 装飾記号 `Symbols`（配列。0 個でもよい。最大 64 個）

各要素の項目:

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `Type` | 0 なし, 1 4芒星, 2 5芒星, 3 ハート, 4 三日月, 5 クロス, 6 ダイヤ, 7 桜の花びら, 8 正円, 9 猫目スリット, 10 蝶々 | ハイライトと同じ形の一覧（番号はハイライトの `Shape` と違う） |
| `Count` | 1〜12 | 2 以上は位置を中心に円形に並ぶ（半径はサイズ × 1.4） |
| `Size` | 0.05〜0.40 | |
| `PositionX`, `PositionY` | -0.85〜0.85 | |
| `Color` | 色（グラデ可） | |
| `Opacity` | 0〜1 | |
| `Rotation` | -180〜180（度） | |
| `CornerRounding` | 0〜1 | |
| `StarCoreScale` | 0.5〜2.0（省略時 1.0） | 星の芯の太さ。4芒星・5芒星の芯（くびれ）の太さを標準の何倍にするか。ほかの形では使わない |
| `StretchTop`, `StretchBottom`, `StretchLeft`, `StretchRight` | 0.5〜2.0（省略時 1.0） | 上下左右の長さ。図形の中心から上・下・左・右の半分ずつを伸び縮みさせる（1.0 でそのまま）。右目の反転が鏡像なら、右目では左右が入れ替わる。例: 4芒星で上 0.7・下 1.3・左右 0.6 にすると、下の長いとがった十字になる |
| `Enabled` | true / false | `false` にすると描かずに設定だけ残します（画面の「表示する」のチェックを外したのと同じ） |
| `Blend` | 重ね方 | |
| `RightEyeFlip` | 0 鏡像, 1 位置だけ反転, 2 反転しない（省略時 0） | 右目での描き方。0: 位置も形の向きも左右反転。1: 位置は反転、形の向き（回転・三日月・左右の長さ・写しの並び）はそのまま。2: 両目で同じ |

### ハイライト `Highlights`（配列。1〜3 個が一般的。最大 64 個）

各要素の項目:

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `Shape` | 0 正円, 2 4芒星（キラキラ）, 3 ダイヤ（菱形）, 4 5芒星, 5 ハート, 6 三日月, 7 クロス, 8 桜の花びら, 9 猫目スリット, 10 蝶々 | 装飾記号と同じ形の一覧（番号は記号の `Type` と違う）。1 は使わない（以前の楕円。正円として読み込まれる）。楕円は正円の上下左右の長さで作る |
| `PositionX`, `PositionY` | -0.85〜0.85 | 左上が自然 |
| `Size` | 0.05〜0.45 | |
| `Count` | 1〜12（省略時 1） | 2 以上は位置を中心に円形に並ぶ（半径はサイズ × 1.4）。装飾記号と同じ |
| `StretchTop`, `StretchBottom`, `StretchLeft`, `StretchRight` | 0.5〜2.0（省略時 1.0） | 上下左右の長さ。装飾記号と同じ。正円で左右を 1.35 にすると横長の楕円、上下を 1.35 にすると縦長の楕円。右目の反転が鏡像のときだけ、右目で左右が入れ替わる |
| `Rotation` | -180〜180 | |
| `CornerRounding` | 0〜1（省略時 0） | 星の先・ダイヤの角などの丸み。正円には影響しない |
| `StarCoreScale` | 0.5〜2.0（省略時 1.0） | 星の芯の太さ（装飾記号と同じ） |
| `Color` | 色（グラデ可） | 通常は白 |
| `Opacity` | 0〜1 | |
| `Softness` | 0〜1（省略時 0） | 輪郭のぼかし。上げるほど輪郭がぼけて、まわりになじむ |
| `Glow` | 0〜1（省略時 0） | 光のにじみ。まわりに光が広がり、自分で光っているように見える |
| `Enabled` | true / false | `false` にすると描かずに設定だけ残します（画面の「表示する」のチェックを外したのと同じ） |
| `Blend` | 重ね方 | |
| `RightEyeFlip` | 0 鏡像, 1 位置だけ反転, 2 反転しない（省略時 0） | 右目での描き方。0: 位置も形の向きも左右反転。1: 位置は反転、形の向き（回転・三日月・左右の長さ・写しの並び）はそのまま。2: 両目で同じ |

右目のハイライトを反転しない（光源が固定された自然な見え方）ときは、各ハイライトの `RightEyeFlip` を 2 にし、`LightRightEyeFlip` も 2 にします（以前のデザイン全体の項目 `MirrorHighlightsWithIris` はなくなりました）。

### 宝石カット面（虹彩全体のカット面の明暗）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `IrisFacetIntensity` | 0〜1 | 強さ。0 でオフ（既定） |
| `IrisFacetCount` | 6〜16 | 面の数（宝石の対称の数。既定 8） |
| `IrisFacetTableSize` | 0.2〜0.7 | 中心の平らな面の大きさ（既定 0.45） |
| `IrisFacetShadeAngle` | 0〜360（度） | 暗くなる側の向き。270 = 上（既定）。右目は自動で鏡像 |
| `IrisFacetShadeBias` | 0〜1 | 0 で面ごとに交互の明暗だけ。上げるほど向きによる明暗が強い（既定 0.6） |
| `IrisFacetBlend` | 重ね方 | 既定 3（オーバーレイ）。色を残したまま明暗を付ける |
| `IrisFacetRangeAngle` | 0〜360（度） | 見える範囲の向き（既定 90） |
| `IrisFacetRangeWidth` | 0〜360（度） | 見える範囲の広さ。360 で全体（既定） |
| `IrisFacetRangeFeather` | 0〜90（度） | 見える範囲の端のぼかし（既定 30） |

### オッドアイ（左右で色を変える）

| 項目 | 範囲・値 | 説明 |
|---|---|---|
| `IsOddEye` | true / false | |
| `OddEyeBaseColor`, `OddEyeInnerColor`, `OddEyeOuterColor`, `OddEyeBottomHighlightColor` | 色（単色のみ） | 右目の色 |
| `OddEyeRayColor` | 色（グラデ可） | 右目の放射ラインの色 |
| `OddEyeSeedOffset` | 整数 | 右目の乱れ方を左目とずらす量（1000 のままでよい） |

左目の色は通常の `BaseColor` などを使います。瞳孔・外輪・ラメ・記号・ハイライトは両目共通です。

---

## 5. テンプレート

全項目がそろったデザイン 1 つの例です。これを書き換えて使えます。

コマンドライン版があれば、`DollIrisCli template` でもひな形を出せます（アプリの新しいデザインと同じ値。`--preset サクラ` のようにプリセットも当てはめられます）。

```json
{
  "Name": "サファイアブルー",
  "SizeMm": 10.0,
  "Shape": 0,
  "AspectRatio": 1.0,
  "TiltAngle": 0.0,
  "OutlineTaper": 0.0,
  "OutlineTopFlat": 0.0,
  "OutlineBottomFlat": 0.0,
  "OutlineSquareness": 0.0,
  "Seed": 12345,
  "BaseColor": "#2979FF",
  "InnerColor": "#80D8FF",
  "OuterColor": "#0D47A1",
  "BottomHighlightColor": "#B3E5FC",
  "InnerGradientRadius": 0.55,
  "OuterGradientRadius": 0.95,
  "BottomHighlightIntensity": 0.4,
  "LightAngle": 270.0,
  "LightRightEyeFlip": 0,
  "LightStrength": 0.0,
  "AuroraColor": "conic-gradient(from 270deg, #FF80AB 0%, #80D8FF 30%, #B388FF 55%, #A7FFEB 80%, #FF80AB 100%)",
  "AuroraOpacity": 0.0,
  "AuroraBlend": 0,
  "NebulaColor": "radial-gradient(#E040FB 0%, #7C4DFF 50%, #40C4FF 100%)",
  "NebulaOpacity": 0.0,
  "NebulaScale": 1.2,
  "NebulaDensity": 0.5,
  "NebulaSwirl": 0.4,
  "FiberNoise": 0.0,
  "FiberNoiseScale": 1.0,
  "ToneBandIntensity": 0.0,
  "ToneBandCount": 3,
  "ToneBandInnerColor": "#FFFFFF",
  "ToneBandOuterColor": "#000000",
  "ToneBandSoftness": 0.1,
  "ToneBandBlend": 4,
  "PupilShape": 0,
  "PupilSizeRatio": 0.32,
  "PupilColor": "#060A18",
  "PupilOpacity": 1.0,
  "PupilSoftness": 0.08,
  "PupilCornerRounding": 0.0,
  "PupilStarCoreScale": 1.0,
  "PupilStretchTop": 1.0,
  "PupilStretchBottom": 1.0,
  "PupilStretchLeft": 1.0,
  "PupilStretchRight": 1.0,
  "PupilRightEyeFlip": 0,
  "PupilFacetIntensity": 0.0,
  "PupilFacetCount": 10,
  "CollaretteIntensity": 0.0,
  "CollaretteColor": "#FFD54F",
  "CollaretteWidth": 0.2,
  "CollaretteJagged": 0.5,
  "CollaretteSpikeCount": 20,
  "CollaretteSoftness": 0.4,
  "CollaretteBlend": 0,
  "RayCount": 48,
  "RayColor": "#B3E5FC",
  "RayThickness": 1.2,
  "RayLengthRatio": 0.65,
  "RayInnerRadiusRatio": 0.32,
  "RayIntensity": 0.65,
  "RayRandomness": 0.45,
  "RayTaper": 0.7,
  "RayWidthVariation": 0.5,
  "RayCurve": 0.2,
  "RayFineLayer": 0.6,
  "RayRangeAngle": 90.0,
  "RayRangeWidth": 360.0,
  "RayRangeFeather": 30.0,
  "LimbalRingThickness": 0.08,
  "LimbalRingColor": "#051636",
  "LimbalRingOpacity": 0.95,
  "LimbalRingSoftness": 0.2,
  "ParticleCount": 50,
  "ParticleColor": "#FFFFFF",
  "ParticleMinSize": 1.0,
  "ParticleMaxSize": 2.4,
  "ParticleOpacity": 0.85,
  "ParticleRadiusRatio": 0.75,
  "ParticleGlow": 0.7,
  "ParticleFlareRatio": 0.15,
  "ParticleFlareLength": 4.0,
  "GlitterBlend": 0,
  "ParticleRangeAngle": 90.0,
  "ParticleRangeWidth": 360.0,
  "ParticleRangeFeather": 30.0,
  "LensPatternEnabled": false,
  "LensRingCount": 6,
  "LensRingColor": "#FFFFFF",
  "LensRingIntensity": 0.5,
  "LensRingWidth": 0.8,
  "LensRefractionGlow": 0.35,
  "LensRangeAngle": 90.0,
  "LensRangeWidth": 360.0,
  "LensRangeFeather": 30.0,
  "IrisFacetIntensity": 0.0,
  "IrisFacetCount": 8,
  "IrisFacetTableSize": 0.45,
  "IrisFacetShadeAngle": 270.0,
  "IrisFacetShadeBias": 0.6,
  "IrisFacetBlend": 3,
  "IrisFacetRangeAngle": 90.0,
  "IrisFacetRangeWidth": 360.0,
  "IrisFacetRangeFeather": 30.0,
  "Symbols": [
    {
      "Type": 1,
      "Count": 1,
      "Size": 0.18,
      "PositionX": 0.25,
      "PositionY": -0.15,
      "Color": "#FFFFFF",
      "Opacity": 0.85,
      "Rotation": 0.0,
      "CornerRounding": 0.0,
      "StarCoreScale": 1.0,
      "StretchTop": 1.0,
      "StretchBottom": 1.0,
      "StretchLeft": 1.0,
      "StretchRight": 1.0,
      "Enabled": true,
      "Blend": 0,
      "RightEyeFlip": 0
    }
  ],
  "Highlights": [
    {
      "Shape": 0,
      "PositionX": -0.32,
      "PositionY": -0.32,
      "Size": 0.26,
      "Count": 1,
      "StretchTop": 1.0,
      "StretchBottom": 1.0,
      "StretchLeft": 1.0,
      "StretchRight": 1.0,
      "Rotation": 0.0,
      "CornerRounding": 0.0,
      "StarCoreScale": 1.0,
      "Color": "#FFFFFF",
      "Opacity": 0.95,
      "Softness": 0.0,
      "Glow": 0.0,
      "Enabled": true,
      "Blend": 0,
      "RightEyeFlip": 0
    }
  ],
  "IsOddEye": false,
  "OddEyeBaseColor": "#FFB300",
  "OddEyeInnerColor": "#FFE082",
  "OddEyeOuterColor": "#E65100",
  "OddEyeRayColor": "#FFF9C4",
  "OddEyeBottomHighlightColor": "#FFFDE7",
  "OddEyeSeedOffset": 1000
}
```

### きれいに見せるコツ

- 地の色は `InnerColor`（明）→ `BaseColor`（中）→ `OuterColor`（暗）の順に明るさを下げ、`LimbalRingColor` をさらに暗くすると立体感が出ます。
- `RayColor` は地の色より明るい同系色にすると透明感が出ます。`RayIntensity` は 0.4〜0.7 が目安です。
- 盛り込みすぎないこと。星雲・オーロラ・レンズ・記号を全部強くすると濁るので、主役を 1〜2 つに絞り、他は 0 か弱めにします。
- 印刷すると画面より暗く見えやすいので、暗い色ばかりにしないようにします。
- 小さい記号（`Size` 0.1 未満）は印刷で潰れやすくなります。

---

## 6. 作り方の例

### 手で書く・直す

[5. テンプレート](#5-テンプレート) をメモ帳などに貼り付け、値を書き換えて保存します。たとえば次のような作業は、画面で 1 つずつ操作するより速く済みます。

- 記号を何個も並べる（`Symbols` の要素をコピーして位置だけ変える）
- 色やサイズだけ違うバリエーションを作る（ファイルを複製して一部の値だけ変える）

### スクリプトで量産する

JSON を書き出せるプログラムなら何でも使えます。`Seed` や色を少しずつ変えたデザインを並べて `〇〇.irisfav.json` にまとめれば、一度に読み込んで見比べられます。

コマンドライン版 `DollIrisCli` を使うと、作ったファイルをアプリを開かずに確かめたり、SVG・PNG・A4 PDF にまとめて書き出したりできます（[USER_MANUAL.md](../USER_MANUAL.md) の 5 章）。

```text
DollIrisCli validate 〇〇.irisfav.json
DollIrisCli svg 〇〇.irisfav.json -o svg
DollIrisCli pdf 〇〇.irisfav.json
```

### AI チャットに書いてもらう

ChatGPT・Claude・Gemini などの AI チャットに、このページを渡して JSON を書いてもらうこともできます。AI が決めるのは数値と色だけで、絵を描くのはアプリです。

> **注意**: AI チャットに書いた内容と、渡したファイル・デザインは **その AI サービスに送られます**。

1. このページ（`DESIGN_JSON_FORMAT.md`）の全文をコピーして貼り付けるか、ファイルとして添付します。
2. 続けて、次の依頼文と作りたい目のイメージを送ります。

   ```text
   上の説明は「Doll Iris Atelier」（ドールアイの虹彩を数式で描くアプリ）のデザインファイルの形式です。
   この形式に従って、これから伝えるイメージのデザインを JSON で作ってください。
   - 出力は JSON だけをコードブロック 1 つで返し、説明は JSON の外に短く書く。
   - テンプレートの全項目を、同じ項目名で出力する。
   - 「Name」に日本語で短いデザイン名を入れる。
   - 1 つだけのときはデザインの JSON をそのまま、複数のときは「複数のデザイン」の形にする。

   作りたい目: 夜空みたいな深い青。星雲を入れて、ラメ多め。瞳孔は星型。10mm
   ```

   イメージの例:
   - 「ルビーとサファイアのオッドアイ。猫目スリット、左上に大きめの丸いハイライト」
   - 「パステルピンクの桜モチーフ。桜の花びらを 5 枚、瞳孔の周りに円形に」
3. 返ってきた JSON を保存して [読み込み](#1-読み込み方) ます。プレビューを見て、気になるところを AI に伝えて直してもらうか、スライダーで仕上げます。

---

## 7. うまくいかないとき

`DollIrisCli validate 〇〇.json` を使うと、下のような誤りが項目の場所つきで一覧になります。

| 症状 | 対処 |
|---|---|
| 「読み込めませんでした」と出る | JSON 以外の文章やコメント（`//`）、末尾の余計な `,` が入っていないか確認する |
| 一部の設定が反映されない | 項目名の大文字・小文字が [5. テンプレート](#5-テンプレート) と同じか確認する（`raycount` などは無視される） |
| 色が既定色になる | 色名（`"blue"` など）は使えない。`#RRGGBB` の形にする |
| 印刷サイズが大きすぎる | `SizeMm` は虹彩（色の部分）の直径で、アイ全体の直径ではない（20mm のアイなら虹彩は約 10mm） |
| AI に頼むと何度も同じ雰囲気になる | 「`Seed` を変えて」「ハイライトの数と形を変えて」など、変えてほしい項目を具体的に伝える |

---

## 8. 項目を更新するとき（開発者向け）

`IrisParameters` に項目を足したり範囲を変えたりしたときは、このページの「項目一覧」と「テンプレート」も合わせて直します。テンプレートがそのまま読み込めて全項目がそろっていることは `DesignJsonFormatTests` が確かめます。
