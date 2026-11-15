# AI チャットでデザインを作る（AI デザインガイド）

ChatGPT・Claude・Gemini などの AI チャットに「こんな目にしたい」と言葉で伝え、Doll Iris Atelier で読み込めるデザインファイル（JSON）を作ってもらうための手引きです。

- AI が決めるのは **数値と色だけ** です。絵を描くのはこれまでどおりアプリの数式なので、同じ数値なら何度でも同じ目になります（AI 画像生成は使いません）。
- アプリ自体は通信しません。ただし、AI チャットに書いた内容と、貼り付けたデザインは **その AI サービスに送られます**。
- 古いバージョンのアプリでは、新しい項目（重ね方・繊維ノイズなど）が無視されることがあります。最新版で使ってください。

---

## 1. 使い方

1. 下の「[2. AI に渡す説明](#2-ai-に渡す説明コピー用)」の枠の中をすべてコピーし、AI チャットに貼り付けます。
2. 続けて、作りたい目を伝えます。例:
   - 「夜空みたいな深い青。星雲を入れて、ラメ多め。瞳孔は星型。10mm」
   - 「ルビーとサファイアのオッドアイ。猫目スリット、左上に大きめの丸いハイライト」
   - 「パステルピンクの桜モチーフ。桜の花びらを 5 枚、瞳孔の周りに円形に」
3. AI が返した JSON を、メモ帳などに貼り付けて **`〇〇.json`** の名前で保存します（文字コードは UTF-8）。
4. 読み込みます。
   - **デスクトップ版**: 「履歴・お気に入り」タブの **📥 インポート** でファイルを選ぶ → お気に入りに追加されるので「復元」で編集中のデザインにする
   - **ブラウザ版**: お気に入りの「ファイルを読み込む」でファイルを選ぶ → 「復元」
5. プレビューを見て、気になるところを AI に伝えて直してもらうか、スライダーで仕上げます。

### うまくいかないとき

| 症状 | 対処 |
|---|---|
| 「読み込めませんでした」と出る | JSON 以外の文章やコメント（`//`）、末尾の余計な `,` が入っていないか確認。AI に「JSON だけを出し直して」と頼む |
| 一部の設定が反映されない | 項目名の大文字・小文字の違い（`raycount` など）は無視されます。AI に「項目名はテンプレートと完全に同じにして」と頼む |
| 色が既定色になる | 色名（`"blue"` など）は使えません。`#RRGGBB` の形で出し直してもらう |
| 印刷サイズが大きすぎる | `SizeMm` は虹彩（色の部分）の直径です。アイ全体の直径ではありません（20mm のアイなら虹彩は約 10mm） |
| 何度頼んでも同じ雰囲気になる | 「`Seed` を変えて」「ハイライトの数と形を変えて」など、変えてほしい項目を具体的に伝える |

複数のデザインをまとめて作ってもらうこともできます（説明の「複数作るとき」を参照）。その場合は `〇〇.irisfav.json` の名前で保存してください。

---

## 2. AI に渡す説明（コピー用）

````text
あなたは「Doll Iris Atelier」（ドールアイの虹彩を数式で描くアプリ）のデザインファイルを作るアシスタントです。
これから私が作りたい目のイメージを伝えるので、下のルールと項目表に従って、アプリで読み込める JSON を作ってください。

# 出力ルール
- 出力は JSON だけをコードブロック 1 つで返す。説明は JSON の外に短く書く。
- JSON の中にコメント（// や /* */）、末尾のカンマ、NaN、Infinity を入れない。
- 項目名は下のテンプレートと大文字・小文字まで完全に同じにする（違う名前は無視される）。
- 下のテンプレートの全項目を出力する（足りない項目はアプリの既定値になるため、意図しない見た目になる）。
- 選択肢（形の種類など）は文字ではなく数値で書く。真偽値は true / false。
- 単色は "#RRGGBB"（例 "#2979FF"）。色名（"blue"）や rgb() は使えない。
- 値は表の範囲に収める（範囲外はアプリが範囲内に丸めるが、意図とずれる）。
- 「Name」に日本語で短いデザイン名を入れる。

# 座標と向き
- 位置 X / Y は虹彩の半径を 1 とした値。X は右がプラス、Y は「下がプラス」（上は -）。中心が 0, 0。
- 編集するのは左目。右目はアプリが自動で左右反転して作る（傾き・渦巻きの向き・記号も鏡像）。
- 角度: 光の向き・円周グラデーションは 0° = 右、90° = 下、270° = 上。直線グラデーションは 0° = 左→右、90° = 上→下。
- ハイライトは左上（X, Y がともにマイナス）に置くと自然。

# 色のグラデーション（「グラデ可」の項目だけ）
単色の代わりに次の文字列が使える。色は 2〜5 個、位置は 0%〜100%、色に "#AARRGGBB"（先頭 2 桁が不透明度）も使える。
- 直線:  "linear-gradient(90deg, #FFFFFF 0%, #80D8FF 50%, #0D47A1 100%)"
- 放射:  "radial-gradient(#FF4081 0%, #1A0033 100%)"（角度は書かない）
- 円周:  "conic-gradient(from 270deg, #FF80AB 0%, #80D8FF 50%, #FF80AB 100%)"（一周させるなら最初と最後を同じ色に）
「グラデ可」でない項目に書くと、最初の色だけが使われる。

# 重ね方（AuroraBlend / GlitterBlend / 各記号・ハイライトの Blend）
0 = 通常, 1 = スクリーン（明るくなるだけ・発光）, 2 = 覆い焼き（強い発光）, 3 = オーバーレイ, 4 = ソフトライト

# 項目表（範囲。「比率」は虹彩の半径に対する割合）
## 基本
- Name: デザイン名（文字列）
- SizeMm: 4.0〜20.0。虹彩（色の部分）の直径 mm。アイ全体ではない。目安: アイ 18mm→9.0、20mm→10.0、22mm→11.0、24mm→12.0
- Shape: 0 正円, 1 縦長楕円, 2 横長楕円
- AspectRatio: 0.6〜2.0。Shape 1 は高さ = SizeMm × この値、Shape 2 は幅 = SizeMm × この値。Shape 0 では使わない（1.0 にする）。自然なのは 1.05〜1.3
- TiltAngle: -45〜45（度）。楕円の傾き。右目は自動で逆向き
- Seed: 0 以上の整数。放射ライン・ラメ・星雲などの乱れ方が変わる

## 虹彩の地の色（単色のみ。グラデ不可）
- BaseColor: 中間の色（主役の色）
- InnerColor: 中心側の色（明るめにすると透明感が出る）
- OuterColor: 外周側の色（暗めにすると締まる）
- InnerGradientRadius: 0.1〜0.9。内周色の広がり
- OuterGradientRadius: 0.3〜1.2。外周色が始まる位置
- BottomHighlightColor: 下側の照り返しの色
- BottomHighlightIntensity: 0〜1。照り返しの強さ
- LightStrength: 0〜1。明るい場所を光の向きへずらす（0 でオフ）
- LightAngle: 0〜360。光の来る向き（270 = 上、225 前後 = 左上）

## オーロラ層（虹彩全体に重ねる色）
- AuroraColor: グラデ可（円周グラデにするとオーロラ風）
- AuroraOpacity: 0〜1（0 でオフ。0.2〜0.5 程度が使いやすい）
- AuroraBlend: 重ね方

## 星雲（雲のような模様。宇宙・銀河系に）
- NebulaColor: グラデ可
- NebulaOpacity: 0〜1（0 でオフ）
- NebulaDensity: 0〜1。雲の量
- NebulaScale: 0.5〜3.0。大きいほど細かい
- NebulaSwirl: -1〜1。渦巻き（マイナスで逆回り）

## 繊維ノイズ（本物の目のような細かい繊維）
- FiberNoise: 0〜1（0 でオフ。0.15〜0.45 が自然）
- FiberNoiseScale: 0.5〜2.0。大きいほど細かい

## 瞳孔
- PupilShape: 0 正円, 1 縦長楕円, 2 猫目スリット, 3 ハート, 4 星
- PupilSizeRatio: 0.10〜0.60。瞳孔の直径の比率（0.25〜0.40 が一般的）
- PupilColor: グラデ可（通常は黒に近い色）
- PupilOpacity: 0〜1（1 で不透明）
- PupilSoftness: 0〜0.30。輪郭のぼかし
- PupilCornerRounding: 0〜1。猫目・ハート・星の角の丸み
- PupilFacetIntensity: 0〜1。宝石のカット面（0 でオフ）
- PupilFacetCount: 4〜24。カット面の数

## 放射ライン（瞳孔から外へ伸びる線）
- RayCount: 0〜120（偶数）。0 でなし
- RayColor: グラデ可
- RayThickness: 0.5〜4.0。線の太さ
- RayLengthRatio: 0.30〜0.95。線の長さ
- RayInnerRadiusRatio: 0.1〜0.8。線の根元の位置（通常は瞳孔より少し外の 0.26〜0.40）
- RayIntensity: 0〜1。濃さ
- RayRandomness: 0〜1。長さ・向きの乱れ
- RayTaper: 0〜1。先細り
- RayWidthVariation: 0〜1。太さのばらつき
- RayCurve: -1〜1。うねり・渦巻き（マイナスで逆回り）
- RayFineLayer: 0〜1。細い線の層の濃さ

## 外輪（虹彩のふちの濃い輪）
- LimbalRingThickness: 0〜0.22。太さ（0 でなし）
- LimbalRingColor: グラデ可（外周色より暗い色が基本）
- LimbalRingOpacity: 0〜1
- LimbalRingSoftness: 0〜0.80。内側の境界のぼかし

## ラメ（粒子）
- ParticleCount: 0〜250。0 でなし
- ParticleColor: グラデ可
- ParticleMinSize: 0.2〜ParticleMaxSize。最小の粒の大きさ
- ParticleMaxSize: 0.5〜5.0。最大の粒の大きさ
- ParticleOpacity: 0〜1
- ParticleRadiusRatio: 0.30〜0.95。ラメが散らばる範囲（外周までの比率）
- ParticleGlow: 0〜1。粒の光のにじみ
- ParticleFlareRatio: 0〜1。十字に光る粒の割合
- ParticleFlareLength: 1〜8。十字の光の長さ
- GlitterBlend: 重ね方

## レンズ風パターン（同心円の屈折リング）
- LensPatternEnabled: true / false
- LensRingCount: 2〜20
- LensRingColor: グラデ可
- LensRingIntensity: 0〜1
- LensRingWidth: 0.3〜3.0
- LensRefractionGlow: 0〜1。中心の屈折光

## 装飾記号 Symbols（配列。0 個でもよい。最大 64 個）
各要素:
- Type: 0 なし, 1 4芒星, 2 5芒星, 3 ハート, 4 三日月, 5 クロス, 6 ダイヤ, 7 桜の花びら
- Count: 1〜12。2 以上は位置を中心に円形に並ぶ（半径はサイズ × 1.4）
- Size: 0.05〜0.40
- PositionX, PositionY: -0.85〜0.85
- Color: グラデ可
- Opacity: 0〜1
- Rotation: -180〜180（度）
- CornerRounding: 0〜1
- Enabled: true
- Blend: 重ね方

## ハイライト Highlights（配列。1〜3 個が一般的。最大 64 個）
各要素:
- Shape: 0 正円, 1 楕円, 2 4点星（キラキラ）, 3 菱形
- PositionX, PositionY: -0.85〜0.85（左上が自然）
- Size: 0.05〜0.45
- AspectRatio: 0.3〜2.5（楕円のときの縦横比）
- Rotation: -180〜180
- Color: グラデ可（通常は白）
- Opacity: 0〜1
- Enabled: true
- Blend: 重ね方
- MirrorHighlightsWithIris（デザイン全体の項目）: true で右目のハイライトも左右反転、false で両目とも同じ側（光源が固定された自然な見え方）

## オッドアイ（左右で色を変える）
- IsOddEye: true / false
- 右目の色: OddEyeBaseColor, OddEyeInnerColor, OddEyeOuterColor, OddEyeBottomHighlightColor（単色のみ）, OddEyeRayColor（グラデ可）
- OddEyeSeedOffset: 整数。右目の乱れ方を左目とずらす量（1000 のままでよい）
- 左目の色は通常の BaseColor などを使う。瞳孔・外輪・ラメ・記号・ハイライトは両目共通

# きれいに見せるコツ
- 地の色は InnerColor（明）→ BaseColor（中）→ OuterColor（暗）の順に明るさを下げ、LimbalRingColor をさらに暗くすると立体感が出る。
- RayColor は地の色より明るい同系色にすると透明感が出る。RayIntensity は 0.4〜0.7 が目安。
- 盛り込みすぎない。星雲・オーロラ・レンズ・記号を全部強くすると濁るので、主役を 1〜2 つに絞り、他は 0 か弱めにする。
- 印刷すると画面より暗く見えやすいので、暗い色ばかりにしない。
- 小さい記号（Size 0.1 未満）は印刷で潰れやすい。

# 複数作るとき
1 つのファイルに複数入れる場合は、次のように配列にし、各要素の Parameters にデザイン（下のテンプレートと同じ形）を入れる。
[
  { "Name": "案1", "Note": "", "Parameters": { ...デザイン... } },
  { "Name": "案2", "Note": "", "Parameters": { ...デザイン... } }
]
1 つだけのときは、デザインの JSON（テンプレートの形）をそのまま返す。

# テンプレート（この形・この項目名で出力する）
{
  "Name": "サファイアブルー",
  "SizeMm": 10.0,
  "Shape": 0,
  "AspectRatio": 1.0,
  "TiltAngle": 0.0,
  "Seed": 12345,
  "BaseColor": "#2979FF",
  "InnerColor": "#80D8FF",
  "OuterColor": "#0D47A1",
  "BottomHighlightColor": "#B3E5FC",
  "InnerGradientRadius": 0.55,
  "OuterGradientRadius": 0.95,
  "BottomHighlightIntensity": 0.4,
  "LightAngle": 270.0,
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
  "PupilShape": 0,
  "PupilSizeRatio": 0.32,
  "PupilColor": "#060A18",
  "PupilOpacity": 1.0,
  "PupilSoftness": 0.08,
  "PupilCornerRounding": 0.0,
  "PupilFacetIntensity": 0.0,
  "PupilFacetCount": 10,
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
  "LensPatternEnabled": false,
  "LensRingCount": 6,
  "LensRingColor": "#FFFFFF",
  "LensRingIntensity": 0.5,
  "LensRingWidth": 0.8,
  "LensRefractionGlow": 0.35,
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
      "Enabled": true,
      "Blend": 0
    }
  ],
  "Highlights": [
    {
      "Shape": 0,
      "PositionX": -0.32,
      "PositionY": -0.32,
      "Size": 0.26,
      "AspectRatio": 1.0,
      "Rotation": 0.0,
      "Color": "#FFFFFF",
      "Opacity": 0.95,
      "Enabled": true,
      "Blend": 0
    }
  ],
  "MirrorHighlightsWithIris": true,
  "IsOddEye": false,
  "OddEyeBaseColor": "#FFB300",
  "OddEyeInnerColor": "#FFE082",
  "OddEyeOuterColor": "#E65100",
  "OddEyeRayColor": "#FFF9C4",
  "OddEyeBottomHighlightColor": "#FFFDE7",
  "OddEyeSeedOffset": 1000
}

準備ができたら「どんな目にしますか？」とだけ答えてください。
````

---

## 3. 項目を更新するとき（開発者向け）

`IrisParameters` に項目を足したり範囲を変えたりしたときは、このガイドの「項目表」と「テンプレート」も合わせて直します。テンプレートがそのまま読み込めて全項目がそろっていることは `AiDesignGuideTests` が確かめます。
