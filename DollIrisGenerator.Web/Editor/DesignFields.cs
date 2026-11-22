using DollIrisGenerator.Models;
using DollIrisGenerator.Services;

namespace DollIrisGenerator.Web.Editor;

/// <summary>
/// The settings tabs, in the same groups, labels and ranges as the desktop app (MainWindow.xaml), so a design
/// made in either app is edited the same way.
/// </summary>
public static class DesignFields
{
    private static int Int(double v) => (int)Math.Round(v);

    /// <summary>
    /// 「透明度」スライダー。デザインが持つ値は不透明度 (1 で不透明) なので、スライダーには 1 − 不透明度 を出し、
    /// % を上げるほど透ける (0% で不透明、100% で見えない) ようにする。
    /// </summary>
    private static SliderField<T> Transparency<T>(Func<T, double> getOpacity, Action<T, double> setOpacity) =>
        new("透明度", t => 1.0 - getOpacity(t), (t, v) => setOpacity(t, Math.Round(1.0 - v, 4)), 0, 1, 0.01, Fmt.Percent,
            "上げるほど透けます (0% で不透明、100% で見えない)");

    /// <summary>
    /// The shared shapes of the decorative symbols and highlights, in the same order and with the same names as the
    /// desktop app (each item's own enum, whose numbers are what the files store).
    /// </summary>
    private static (string, object)[] Shapes<TEnum>(Func<ShapeKind, TEnum> of) where TEnum : notnull => new (string, ShapeKind)[]
    {
        ("正円", ShapeKind.Circle), ("4芒星", ShapeKind.Star4), ("5芒星", ShapeKind.Star5), ("ハート", ShapeKind.Heart),
        ("三日月", ShapeKind.Moon), ("クロス", ShapeKind.Cross), ("ダイヤ", ShapeKind.Diamond), ("桜の花びら", ShapeKind.SakuraPetal),
        ("猫目スリット", ShapeKind.CatSlit), ("蝶々", ShapeKind.Butterfly)
    }.Select(x => (x.Item1, (object)of(x.Item2))).ToArray();

    private static SymbolType SymbolOf(ShapeKind kind) =>
        Enum.GetValues<SymbolType>().Single(t => ShapeKinds.Of(t) == kind);

    private static PupilShape PupilOf(ShapeKind kind) =>
        Enum.GetValues<PupilShape>().Single(x => ShapeKinds.Of(x) == kind);

    /// <summary>One of the pupil's 上下左右の長さ (the same range as the desktop app's sliders).</summary>
    private static SliderField<IrisParameters> PupilStretch(string label, Func<IrisParameters, double> get, Action<IrisParameters, double> set) =>
        new(label, get, set, ShapeStretch.MinSlider, ShapeStretch.MaxSlider, 0.01, Fmt.Percent,
            "瞳孔の中心から上・下・左・右の半分ずつを伸び縮みさせます (100% でそのまま)。正円の上下を伸ばすと縦長の楕円になります。右目は鏡像なので、左右の長さが入れ替わります");

    private static HighlightShape HighlightOf(ShapeKind kind) =>
        Enum.GetValues<HighlightShape>().Single(h => ShapeKinds.Of(h) == kind);

    /// <summary>右目の反転 of a decorative symbol or highlight, and of the pupil / light (which sit at the center).</summary>
    private static readonly (string, object)[] ItemFlips =
        { ("鏡像", RightEyeFlip.Mirror), ("位置だけ反転", RightEyeFlip.PositionOnly), ("反転しない", RightEyeFlip.None) };
    private static readonly (string, object)[] CenteredFlips = { ("鏡像", RightEyeFlip.Mirror), ("反転しない", RightEyeFlip.None) };

    /// <summary>星の芯の太さ (the same range as the desktop app's slider).</summary>
    private static SliderField<T> StarCoreField<T>(Func<T, double> get, Action<T, double> set) =>
        new("星の芯の太さ", get, set, StarCore.MinSlider, StarCore.MaxSlider, 0.01, Fmt.Percent,
            "4芒星・5芒星の芯 (くびれ) の太さです。100% で標準、下げると細くとがったきらめき、上げると太い星になります。ほかの形には影響しません");

    /// <summary>One of a symbol's or highlight's 上下左右の長さ (the same range as the desktop app's sliders).</summary>
    private static SliderField<T> Stretch<T>(string label, Func<T, double> get, Action<T, double> set) =>
        new(label, get, set, ShapeStretch.MinSlider, ShapeStretch.MaxSlider, 0.01, Fmt.Percent,
            "図形の中心から上・下・左・右の半分ずつを伸び縮みさせます (100% でそのまま)。右目を鏡像にするときは、左右の長さも入れ替わります");

    public static readonly IReadOnlyList<Field<IrisParameters>> Basic = new Field<IrisParameters>[]
    {
        new TextField<IrisParameters>("デザイン名", p => p.Name, (p, v) => p.Name = v),
        new SliderField<IrisParameters>("虹彩サイズ (直径)", p => p.SizeMm, (p, v) => p.SizeMm = Math.Round(v, 1),
            IrisParameters.MinSizeMm, IrisParameters.MaxSizeMm, 0.1, Fmt.Mm,
            "虹彩（黒目の色の部分）の直径です。アイ全体の直径ではありません。目安: 20mm のアイなら虹彩は約 10mm"),
        new ChoiceField<IrisParameters>("虹彩形状", p => p.Shape, (p, v) => p.Shape = (IrisShape)v, new (string, object)[]
        {
            ("正円", IrisShape.Circle), ("縦長楕円", IrisShape.VerticalOval), ("横長楕円", IrisShape.HorizontalOval)
        }),
        new SliderField<IrisParameters>("楕円の縦横比率", p => p.AspectRatio, (p, v) => p.AspectRatio = v, 0.6, 2.0, 0.01, Fmt.F2),
        new SliderField<IrisParameters>("楕円の傾き角度 (チルト)", p => p.TiltAngle, (p, v) => p.TiltAngle = v, -45, 45, 1, Fmt.Deg),
        new HeadingField<IrisParameters>("虹彩の輪郭", "上を平らにする・卵形や盾形にするなど、楕円からの変え方です。すべて 0 で楕円。カットラインも輪郭に沿います"),
        new ChoiceField<IrisParameters>("簡単選択", p => IrisOutline.Match(p)?.Name ?? "",
            (p, v) => { var s = IrisOutline.Styles.FirstOrDefault(x => x.Name == (string)v); if (s != null) IrisOutline.Apply(p, s); },
            IrisOutline.Styles.Select(s => (s.Name, (object)s.Name)).ToArray()),
        new SliderField<IrisParameters>("上下の幅の差", p => p.OutlineTaper, (p, v) => p.OutlineTaper = v, -1, 1, 0.01, Fmt.Percent,
            "プラスで上が広く下がすぼむ (卵形・盾形)、マイナスで下が広くなります"),
        new SliderField<IrisParameters>("上の平ら", p => p.OutlineTopFlat, (p, v) => p.OutlineTopFlat = v, 0, 1, 0.01, Fmt.Percent,
            "上の端を平らにします (角は丸いまま)"),
        new SliderField<IrisParameters>("下の平ら", p => p.OutlineBottomFlat, (p, v) => p.OutlineBottomFlat = v, 0, 1, 0.01, Fmt.Percent,
            "下の端を平らにします (角は丸いまま)"),
        new SliderField<IrisParameters>("四角さ", p => p.OutlineSquareness, (p, v) => p.OutlineSquareness = v, 0, 1, 0.01, Fmt.Percent,
            "楕円から角の丸い四角に近づけます"),
    };

    public static readonly IReadOnlyList<Field<IrisParameters>> Colors =
    [
        new HeadingField<IrisParameters>("虹彩カラー"),
        new ColorField<IrisParameters>("ベース色 (中間色)", p => p.BaseColor, (p, v) => p.BaseColor = v),
        new ColorField<IrisParameters>("内周色 (中心グラデ)", p => p.InnerColor, (p, v) => p.InnerColor = v),
        new SliderField<IrisParameters>("内周グラデ範囲", p => p.InnerGradientRadius, (p, v) => p.InnerGradientRadius = v, 0.1, 0.9, 0.01, Fmt.Percent),
        new ColorField<IrisParameters>("外周色 (外郭グラデ)", p => p.OuterColor, (p, v) => p.OuterColor = v),
        new SliderField<IrisParameters>("外周グラデ範囲", p => p.OuterGradientRadius, (p, v) => p.OuterGradientRadius = v, 0.3, 1.2, 0.01, Fmt.Percent),
        new ColorField<IrisParameters>("下部反射光 (照り返し)", p => p.BottomHighlightColor, (p, v) => p.BottomHighlightColor = v),
        new SliderField<IrisParameters>("反射光の強さ", p => p.BottomHighlightIntensity, (p, v) => p.BottomHighlightIntensity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("反射光の始まる高さ", p => p.BottomHighlightStart, (p, v) => p.BottomHighlightStart = v,
            0, IrisParameters.MaxBottomHighlightStart, 0.01, Fmt.Percent,
            "照り返しが始まるところ。虹彩の高さに対する、上端からの割合 (最初は 45%)。小さいほど上まで広がり、大きいほど下端だけの細い照り返しになります"),
        new SliderField<IrisParameters>("まぶたの影の強さ", p => p.UpperShadowIntensity, (p, v) => p.UpperShadowIntensity = v, 0, 1, 0.01, Fmt.Percent,
            "虹彩の上側に落ちる影の濃さ。0% で影なし (最初は 65%)"),
        new SliderField<IrisParameters>("まぶたの影の深さ", p => p.UpperShadowDepth, (p, v) => p.UpperShadowDepth = v,
            IrisParameters.MinUpperShadowDepth, IrisParameters.MaxUpperShadowDepth, 0.01, Fmt.Percent, "影が消えるところ。虹彩の高さに対する、上端からの割合 (最初は 65%)"),
        new ColorField<IrisParameters>("まぶたの影の色", p => p.UpperShadowColor, (p, v) => p.UpperShadowColor = v),
        new SliderField<IrisParameters>("光の強さ", p => p.LightStrength, (p, v) => p.LightStrength = v, 0, 1, 0.01, Fmt.Percent,
            "虹彩のいちばん明るいところを、光の来る向きへずらします。0 でオフ"),
        new SliderField<IrisParameters>("光の向き", p => p.LightAngle, (p, v) => p.LightAngle = v, 0, 360, 1, Fmt.Deg,
            "0° = 右、90° = 下、270° = 上"),
        new ChoiceField<IrisParameters>("光の向きの右目の反転", p => p.LightRightEyeFlip, (p, v) => p.LightRightEyeFlip = (RightEyeFlip)v, CenteredFlips,
            "右目での光の向き。鏡像: 左右反転した向きから光が入る。反転しない: 両目で同じ向きから光が入る (ハイライトも「反転しない」にすると、光源が固定された自然な光になります)"),
        new HeadingField<IrisParameters>("色の段", "瞳孔の縁から外周までを同じ幅の同心の帯に分け、帯ごとに一段ずつ違う色を重ねます (アニメ塗りの段階的な色分け)"),
        new SliderField<IrisParameters>("色の段の強さ", p => p.ToneBandIntensity, (p, v) => p.ToneBandIntensity = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new SliderField<IrisParameters>("段の数", p => p.ToneBandCount, (p, v) => p.ToneBandCount = Int(v),
            IrisParameters.MinToneBandCount, IrisParameters.MaxToneBandCount, 1, v => Fmt.Count(v, "段")),
        new ColorField<IrisParameters>("内側の色", p => p.ToneBandInnerColor, (p, v) => p.ToneBandInnerColor = v),
        new ColorField<IrisParameters>("外側の色", p => p.ToneBandOuterColor, (p, v) => p.ToneBandOuterColor = v),
        new SliderField<IrisParameters>("境目のぼかし", p => p.ToneBandSoftness, (p, v) => p.ToneBandSoftness = v, 0, 1, 0.01, Fmt.Percent,
            "0% でくっきりした段。上げるほど境目がなめらかになります"),
        new ChoiceField<IrisParameters>("色の段の重ね方", p => p.ToneBandBlend, (p, v) => p.ToneBandBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) },
            "ソフトライト (最初の値): 白 → 黒なら虹彩の色を残したまま内側を明るく、外側を暗くします。通常: 帯の色で塗ります"),
        new HeadingField<IrisParameters>("二色分け", "弧の境目で虹彩を 2 つに分け、片側 (最初は下側) の色を変えます。放射ライン・外輪・瞳孔はその上に見えます"),
        new SliderField<IrisParameters>("二色分けの強さ", p => p.TwoToneIntensity, (p, v) => p.TwoToneIntensity = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new ColorField<IrisParameters>("色を変える部分の色", p => p.TwoToneColor, (p, v) => p.TwoToneColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("境目の高さ", p => p.TwoToneHeight, (p, v) => p.TwoToneHeight = v,
            IrisParameters.MinTwoToneHeight, IrisParameters.MaxTwoToneHeight, 0.01, Fmt.Percent, "境目の真ん中が、外周から虹彩の直径のどれだけまで来るか (50% で中心)"),
        new SliderField<IrisParameters>("境目の丸み", p => p.TwoToneCurve, (p, v) => p.TwoToneCurve = v,
            -IrisParameters.MaxTwoToneCurve, IrisParameters.MaxTwoToneCurve, 0.01, Fmt.F2, "プラスで中心側へ膨らむ弧、0 でまっすぐ、マイナスで外周側へたわむ弧"),
        new SliderField<IrisParameters>("境目のぼかし", p => p.TwoToneSoftness, (p, v) => p.TwoToneSoftness = v, 0, 1, 0.01, Fmt.Percent, "0% でくっきり"),
        new SliderField<IrisParameters>("二色分けの向き", p => p.TwoToneAngle, (p, v) => p.TwoToneAngle = v, 0, 360, 1, Fmt.Deg,
            "色を変える側。0° = 右、90° = 下 (最初の値)、180° = 左、270° = 上 (右目は自動で鏡像)"),
        new ChoiceField<IrisParameters>("二色分けの重ね方", p => p.TwoToneBlend, (p, v) => p.TwoToneBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) }),
    ];

    /// <summary>The 質感 tab: layers over the iris colors (the same 「すべての色」 lock as 色).</summary>
    public static readonly IReadOnlyList<Field<IrisParameters>> Texture =
    [
        new HeadingField<IrisParameters>("オーロラ層", "虹彩全体に重ねる色。「円周」のグラデーションにすると、中心の周りで色が移り変わるオーロラ風になります"),
        new ColorField<IrisParameters>("オーロラ層の色", p => p.AuroraColor, (p, v) => p.AuroraColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("オーロラ層の強さ", p => p.AuroraOpacity, (p, v) => p.AuroraOpacity = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new ChoiceField<IrisParameters>("オーロラ層の重ね方", p => p.AuroraBlend, (p, v) => p.AuroraBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) }, "スクリーン: 明るくなるだけ (発光)。覆い焼き: 強い発光。オーバーレイ: 明るいところをより明るく、暗いところをより暗く。ソフトライト: オーバーレイの弱いもの"),
        new HeadingField<IrisParameters>("星雲", "虹彩に重ねる雲のような模様。グラデーションにすると、場所ごとに雲の色が変わります (銀河・星雲風)"),
        new ColorField<IrisParameters>("星雲の色", p => p.NebulaColor, (p, v) => p.NebulaColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("星雲の強さ", p => p.NebulaOpacity, (p, v) => p.NebulaOpacity = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new SliderField<IrisParameters>("星雲の量", p => p.NebulaDensity, (p, v) => p.NebulaDensity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("星雲の細かさ", p => p.NebulaScale, (p, v) => p.NebulaScale = v,
            IrisParameters.MinNebulaScale, IrisParameters.MaxNebulaScale, 0.01, Fmt.F2),
        new SliderField<IrisParameters>("星雲の渦巻き", p => p.NebulaSwirl, (p, v) => p.NebulaSwirl = v, -1, 1, 0.01, Fmt.Percent,
            "マイナスで逆回り (右目は自動で鏡像)"),
        new HeadingField<IrisParameters>("繊維ノイズ", "瞳孔から外へ伸びる細かい繊維のゆらぎ (明るい繊維と暗い繊維) を重ねます"),
        new SliderField<IrisParameters>("繊維ノイズ", p => p.FiberNoise, (p, v) => p.FiberNoise = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new SliderField<IrisParameters>("繊維の細かさ", p => p.FiberNoiseScale, (p, v) => p.FiberNoiseScale = v, 0.5, 2, 0.01, Fmt.F2,
            "上げるほど繊維が細かく、密になります"),
        new HeadingField<IrisParameters>("偏光の虹色", "ホログラムシートや偏光フィルムのような、細かい虹色の縞の照りです。縞は「光の向き」に合わせて並びます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("偏光の虹色の強さ", p => p.HoloIntensity, (p, v) => p.HoloIntensity = v, 0, 1, 0.01, Fmt.Percent, "虹色の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("偏光の虹色の細かさ", p => p.HoloScale, (p, v) => p.HoloScale = v,
            IrisParameters.MinHoloScale, IrisParameters.MaxHoloScale, 0.1, Fmt.F1, "虹彩の端から端までに、虹がいくつ並ぶか"),
        new ChoiceField<IrisParameters>("偏光の虹色の縞の形", p => p.HoloShape, (p, v) => p.HoloShape = (HoloShape)v,
            new (string, object)[] { ("直線", HoloShape.Linear), ("同心円", HoloShape.Concentric) },
            "直線: 光の向きに直角な縞。同心円: 光の側に寄った中心のまわりの輪"),
        new ChoiceField<IrisParameters>("偏光の虹色の重ね方", p => p.HoloBlend, (p, v) => p.HoloBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) },
            "スクリーン (最初の値) は明るくなるだけです。ソフトライト・オーバーレイは虹彩の色を残して色づけます"),
        .. RangeFields(PatternLayer.Holo, "偏光の虹色の見える範囲"),
    ];

    public static readonly IReadOnlyList<Field<IrisParameters>> Pupil =
    [
        new HeadingField<IrisParameters>("瞳孔模様"),
        new ChoiceField<IrisParameters>("形状", p => p.PupilShape, (p, v) => p.PupilShape = (PupilShape)v, Shapes(PupilOf)),
        new ColorField<IrisParameters>("色", p => p.PupilColor, (p, v) => p.PupilColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("サイズ", p => p.PupilSizeRatio, (p, v) => p.PupilSizeRatio = v, 0.10, 0.60, 0.01, Fmt.Percent, "虹彩の直径に対する、瞳孔の直径の比率です"),
        new SliderField<IrisParameters>("角の丸み", p => p.PupilCornerRounding, (p, v) => p.PupilCornerRounding = v, 0, 1, 0.01, Fmt.Percent,
            "猫目スリット・ハート・星などのとがった角を丸めます。0% で従来どおりのとがった形です。正円には影響しません"),
        new SliderField<IrisParameters>("星の芯の太さ", p => p.PupilStarCoreScale, (p, v) => p.PupilStarCoreScale = v, StarCore.MinSlider, StarCore.MaxSlider, 0.01, Fmt.Percent,
            "5芒星・4芒星の瞳孔の芯 (くびれ) の太さです。100% で標準。ほかの形には影響しません"),
        PupilStretch("上の長さ", p => p.PupilStretchTop, (p, v) => p.PupilStretchTop = v),
        PupilStretch("下の長さ", p => p.PupilStretchBottom, (p, v) => p.PupilStretchBottom = v),
        PupilStretch("左の長さ", p => p.PupilStretchLeft, (p, v) => p.PupilStretchLeft = v),
        PupilStretch("右の長さ", p => p.PupilStretchRight, (p, v) => p.PupilStretchRight = v),
        Transparency<IrisParameters>(p => p.PupilOpacity, (p, v) => p.PupilOpacity = v),
        new ChoiceField<IrisParameters>("右目の反転", p => p.PupilRightEyeFlip, (p, v) => p.PupilRightEyeFlip = (RightEyeFlip)v, CenteredFlips,
            "右目での瞳孔の描き方。鏡像: 左右反転 (三日月や左右の長さ・宝石カットも反転)。反転しない: 両目でまったく同じ"),
        new SliderField<IrisParameters>("輪郭の柔らかさ (ぼかし)", p => p.PupilSoftness, (p, v) => p.PupilSoftness = v, 0, 0.30, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("瞳孔の宝石カット", p => p.PupilFacetIntensity, (p, v) => p.PupilFacetIntensity = v, 0, 1, 0.01, Fmt.Percent,
            "瞳孔の中に宝石のカット面のような明暗の面を入れます。0% でオフ"),
        new SliderField<IrisParameters>("カット面の数", p => p.PupilFacetCount, (p, v) => p.PupilFacetCount = Int(v),
            IrisParameters.MinPupilFacetCount, IrisParameters.MaxPupilFacetCount, 1, v => Fmt.Count(v, "面")),
        new HeadingField<IrisParameters>("瞳孔の縁取り線", "瞳孔の輪郭に沿って細い線を引きます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("縁取り線の強さ", p => p.PupilOutlineIntensity, (p, v) => p.PupilOutlineIntensity = v, 0, 1, 0.01, Fmt.Percent, "線の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("縁取り線の太さ", p => p.PupilOutlineWidth, (p, v) => p.PupilOutlineWidth = v,
            IrisParameters.MinPupilOutlineWidth, IrisParameters.MaxPupilOutlineWidth, 0.001, Fmt.Percent1, "虹彩の半径に対する割合"),
        new ColorField<IrisParameters>("縁取り線の色", p => p.PupilOutlineColor, (p, v) => p.PupilOutlineColor = v),
        new HeadingField<IrisParameters>("瞳孔の中の輪", "瞳孔の中に、瞳孔と同じ形を小さくした細い輪を 3 本まで引きます。輪ごとに強さ・大きさ・太さ・色を決めます。縁取り線と同時に使えます (強さ 0% でその輪はオフ)"),
        new SliderField<IrisParameters>("中の輪 1 本目の強さ", p => p.PupilInnerRingIntensity, (p, v) => p.PupilInnerRingIntensity = v, 0, 1, 0.01, Fmt.Percent, "輪の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("中の輪 1 本目の大きさ", p => p.PupilInnerRingSize, (p, v) => p.PupilInnerRingSize = v,
            IrisParameters.MinPupilInnerRingSize, IrisParameters.MaxPupilInnerRingSize, 0.01, Fmt.Percent, "瞳孔に対する輪の大きさ (50% で瞳孔の半分)。間隔は自由です"),
        new SliderField<IrisParameters>("中の輪 1 本目の太さ", p => p.PupilInnerRingWidth, (p, v) => p.PupilInnerRingWidth = v,
            IrisParameters.MinPupilOutlineWidth, IrisParameters.MaxPupilOutlineWidth, 0.001, Fmt.Percent1, "虹彩の半径に対する割合 (輪の大きさを変えても太さは変わりません)"),
        new ColorField<IrisParameters>("中の輪 1 本目の色", p => p.PupilInnerRingColor, (p, v) => p.PupilInnerRingColor = v),
        new SliderField<IrisParameters>("中の輪 2 本目の強さ", p => p.PupilInnerRingIntensity2, (p, v) => p.PupilInnerRingIntensity2 = v, 0, 1, 0.01, Fmt.Percent, "輪の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("中の輪 2 本目の大きさ", p => p.PupilInnerRingSize2, (p, v) => p.PupilInnerRingSize2 = v,
            IrisParameters.MinPupilInnerRingSize, IrisParameters.MaxPupilInnerRingSize, 0.01, Fmt.Percent, "瞳孔に対する輪の大きさ (50% で瞳孔の半分)。間隔は自由です"),
        new SliderField<IrisParameters>("中の輪 2 本目の太さ", p => p.PupilInnerRingWidth2, (p, v) => p.PupilInnerRingWidth2 = v,
            IrisParameters.MinPupilOutlineWidth, IrisParameters.MaxPupilOutlineWidth, 0.001, Fmt.Percent1, "虹彩の半径に対する割合 (輪の大きさを変えても太さは変わりません)"),
        new ColorField<IrisParameters>("中の輪 2 本目の色", p => p.PupilInnerRingColor2, (p, v) => p.PupilInnerRingColor2 = v),
        new SliderField<IrisParameters>("中の輪 3 本目の強さ", p => p.PupilInnerRingIntensity3, (p, v) => p.PupilInnerRingIntensity3 = v, 0, 1, 0.01, Fmt.Percent, "輪の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("中の輪 3 本目の大きさ", p => p.PupilInnerRingSize3, (p, v) => p.PupilInnerRingSize3 = v,
            IrisParameters.MinPupilInnerRingSize, IrisParameters.MaxPupilInnerRingSize, 0.01, Fmt.Percent, "瞳孔に対する輪の大きさ (50% で瞳孔の半分)。間隔は自由です"),
        new SliderField<IrisParameters>("中の輪 3 本目の太さ", p => p.PupilInnerRingWidth3, (p, v) => p.PupilInnerRingWidth3 = v,
            IrisParameters.MinPupilOutlineWidth, IrisParameters.MaxPupilOutlineWidth, 0.001, Fmt.Percent1, "虹彩の半径に対する割合 (輪の大きさを変えても太さは変わりません)"),
        new ColorField<IrisParameters>("中の輪 3 本目の色", p => p.PupilInnerRingColor3, (p, v) => p.PupilInnerRingColor3 = v),
        new HeadingField<IrisParameters>("瞳孔まわりの輪", "瞳孔の縁から外へ広がる、外縁がギザギザの色の輪です (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("強さ", p => p.CollaretteIntensity, (p, v) => p.CollaretteIntensity = v, 0, 1, 0.01, Fmt.Percent, "輪の濃さ。0% でオフ"),
        new ColorField<IrisParameters>("輪の色", p => p.CollaretteColor, (p, v) => p.CollaretteColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("幅", p => p.CollaretteWidth, (p, v) => p.CollaretteWidth = v,
            IrisParameters.MinCollaretteWidth, IrisParameters.MaxCollaretteWidth, 0.01, Fmt.Percent, "瞳孔の縁から輪の外縁 (山の先) までの幅。虹彩の半径に対する割合"),
        new SliderField<IrisParameters>("ギザギザ", p => p.CollaretteJagged, (p, v) => p.CollaretteJagged = v, 0, 1, 0.01, Fmt.Percent, "外縁の谷の深さ。0% で円"),
        new SliderField<IrisParameters>("山の数", p => p.CollaretteSpikeCount, (p, v) => p.CollaretteSpikeCount = Int(v),
            IrisParameters.MinCollaretteSpikeCount, IrisParameters.MaxCollaretteSpikeCount, 1, v => Fmt.Count(v, "個")),
        new SliderField<IrisParameters>("柔らかさ", p => p.CollaretteSoftness, (p, v) => p.CollaretteSoftness = v, 0, 1, 0.01, Fmt.Percent, "外縁のぼかし。0% でくっきり"),
        new ChoiceField<IrisParameters>("重ね方", p => p.CollaretteBlend, (p, v) => p.CollaretteBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) },
            "スクリーンにすると光るような輪になります"),
    ];

    public static readonly IReadOnlyList<Field<IrisParameters>> Rays =
    [
        new HeadingField<IrisParameters>("放射模様"),
        new SliderField<IrisParameters>("ライン本数", p => p.RayCount, (p, v) => p.RayCount = Int(v), 0, 120, 2, v => Fmt.Count(v, "本")),
        new ColorField<IrisParameters>("ライン色", p => p.RayColor, (p, v) => p.RayColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("線幅 (太さ)", p => p.RayThickness, (p, v) => p.RayThickness = v, 0.5, 4.0, 0.1, Fmt.F1),
        new SliderField<IrisParameters>("長さ比率", p => p.RayLengthRatio, (p, v) => p.RayLengthRatio = v, 0.30, 0.95, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("強度 (濃さ)", p => p.RayIntensity, (p, v) => p.RayIntensity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("ランダム性 (変動量)", p => p.RayRandomness, (p, v) => p.RayRandomness = v, 0, 1, 0.01, Fmt.Percent),
        new HeadingField<IrisParameters>("繊維の質感 (すべて 0 で直線)"),
        new SliderField<IrisParameters>("先細り", p => p.RayTaper, (p, v) => p.RayTaper = v, 0, 1, 0.01, Fmt.Percent, "瞳孔側を太く、先端を細く尖らせます"),
        new SliderField<IrisParameters>("太さのばらつき", p => p.RayWidthVariation, (p, v) => p.RayWidthVariation = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("うねり (渦巻き)", p => p.RayCurve, (p, v) => p.RayCurve = v, -1, 1, 0.01, Fmt.Percent, "マイナスで逆回り (右目は自動で鏡像)"),
        new SliderField<IrisParameters>("細線レイヤー", p => p.RayFineLayer, (p, v) => p.RayFineLayer = v, 0, 1, 0.01, Fmt.Percent),
        .. RangeFields(PatternLayer.Rays),
        new HeadingField<IrisParameters>("外周からの線", "外周から内側へ伸びる先細りの線です。瞳孔側からの放射ラインとは別の組で、両方を同時に描けます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("外周からの線の強さ", p => p.RimRayIntensity, (p, v) => p.RimRayIntensity = v, 0, 1, 0.01, Fmt.Percent, "線の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("外周からの線の本数", p => p.RimRayCount, (p, v) => p.RimRayCount = Int(v),
            IrisParameters.MinRimRayCount, IrisParameters.MaxRimRayCount, 1, v => Fmt.Count(v, "本")),
        new ColorField<IrisParameters>("外周からの線の色", p => p.RimRayColor, (p, v) => p.RimRayColor = v),
        new SliderField<IrisParameters>("外周からの線の長さ", p => p.RimRayLength, (p, v) => p.RimRayLength = v,
            IrisParameters.MinRimRayLength, IrisParameters.MaxRimRayLength, 0.01, Fmt.Percent, "外周から内側の先まで。虹彩の半径に対する割合"),
        new SliderField<IrisParameters>("外周からの線の線幅", p => p.RimRayWidth, (p, v) => p.RimRayWidth = v,
            IrisParameters.MinRimRayWidth, IrisParameters.MaxRimRayWidth, 0.1, Fmt.F1, "外周での線の太さ (放射ラインの線幅と同じ単位)"),
        new SliderField<IrisParameters>("外周からの線のランダム性", p => p.RimRayRandomness, (p, v) => p.RimRayRandomness = v, 0, 1, 0.01, Fmt.Percent,
            "向き・長さ・濃さのばらつき"),
        .. RangeFields(PatternLayer.RimRays, "外周からの線の見える範囲"),
    ];

    /// <summary>
    /// A pattern layer's 見える範囲: quick-select buttons and the three sliders (the same as the desktop app).
    /// </summary>
    private static Field<IrisParameters>[] RangeFields(PatternLayer layer, string heading = "見える範囲")
    {
        Func<IrisParameters, (double A, double W, double F)> of = p => PatternRange.Of(p, layer);
        return new Field<IrisParameters>[]
        {
            new HeadingField<IrisParameters>(heading, "中心から見た扇形の中だけに模様を出します。向きは 0° = 右、90° = 下、270° = 上 (右目は自動で鏡像)。広さ 360° で全体"),
            new ChoiceField<IrisParameters>("簡単選択", p => PatternRange.Match(p, layer)?.Name ?? "",
                (p, v) => { var s = PatternRange.Styles.FirstOrDefault(x => x.Name == (string)v); if (s != null) PatternRange.Apply(p, layer, s); },
                PatternRange.Styles.Select(s => (s.Name, (object)s.Name)).ToArray()),
            new SliderField<IrisParameters>("範囲の向き", p => of(p).A, (p, v) => { var r = of(p); PatternRange.Set(p, layer, v, r.W, r.F); }, 0, 360, 1, Fmt.Deg,
                "扇形の中心の向き。0° = 右、90° = 下、180° = 左、270° = 上"),
            new SliderField<IrisParameters>("範囲の広さ", p => of(p).W, (p, v) => { var r = of(p); PatternRange.Set(p, layer, r.A, v, r.F); }, 0, 360, 1, Fmt.Deg,
                "360° で全体 (範囲の制限なし)、180° で半分"),
            new SliderField<IrisParameters>("端のぼかし", p => of(p).F, (p, v) => { var r = of(p); PatternRange.Set(p, layer, r.A, r.W, v); }, 0, 90, 1, Fmt.Deg,
                "扇形の両端で、だんだん消えていく角度の幅"),
        };
    }

    public static readonly IReadOnlyList<Field<IrisParameters>> RingAndGlitter =
    [
        new HeadingField<IrisParameters>("外輪"),
        new SliderField<IrisParameters>("リング幅 (太さ)", p => p.LimbalRingThickness, (p, v) => p.LimbalRingThickness = v, 0, 0.22, 0.005, Fmt.Percent),
        new ColorField<IrisParameters>("リング色", p => p.LimbalRingColor, (p, v) => p.LimbalRingColor = v, AllowGradient: true),
        Transparency<IrisParameters>(p => p.LimbalRingOpacity, (p, v) => p.LimbalRingOpacity = v),
        new SliderField<IrisParameters>("境界の柔らかさ (ぼかし)", p => p.LimbalRingSoftness, (p, v) => p.LimbalRingSoftness = v, 0, 0.80, 0.01, Fmt.Percent),
        new HeadingField<IrisParameters>("外輪の内側の輪", "外輪の内側に、細く明るい輪をもう 1 本引きます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("内側の輪の強さ", p => p.LimbalInnerRingIntensity, (p, v) => p.LimbalInnerRingIntensity = v, 0, 1, 0.01, Fmt.Percent, "輪の濃さ。0% でオフ"),
        new SliderField<IrisParameters>("内側の輪の太さ", p => p.LimbalInnerRingWidth, (p, v) => p.LimbalInnerRingWidth = v,
            IrisParameters.MinLimbalInnerRingWidth, IrisParameters.MaxLimbalInnerRingWidth, 0.001, Fmt.Percent1, "虹彩の半径に対する割合"),
        new SliderField<IrisParameters>("外輪との間隔", p => p.LimbalInnerRingGap, (p, v) => p.LimbalInnerRingGap = v,
            0, IrisParameters.MaxLimbalInnerRingGap, 0.001, Fmt.Percent1, "外輪の内側の縁から輪までの距離。虹彩の半径に対する割合"),
        new ColorField<IrisParameters>("内側の輪の色", p => p.LimbalInnerRingColor, (p, v) => p.LimbalInnerRingColor = v),
        new HeadingField<IrisParameters>("粒子模様・ラメ"),
        new SliderField<IrisParameters>("粒子数量 (ラメ数)", p => p.ParticleCount, (p, v) => p.ParticleCount = Int(v), 0, 250, 5, v => Fmt.Count(v, "個")),
        new ColorField<IrisParameters>("ラメ色", p => p.ParticleColor, (p, v) => p.ParticleColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("粒径 (最大サイズ)", p => p.ParticleMaxSize, (p, v) => p.ParticleMaxSize = v, 0.5, 5.0, 0.1, Fmt.F1),
        Transparency<IrisParameters>(p => p.ParticleOpacity, (p, v) => p.ParticleOpacity = v),
        new SliderField<IrisParameters>("分布範囲 (外周比率)", p => p.ParticleRadiusRatio, (p, v) => p.ParticleRadiusRatio = v, 0.30, 0.95, 0.01, Fmt.Percent),
        new ChoiceField<IrisParameters>("粒の形", p => p.ParticleShape, (p, v) => p.ParticleShape = (GlitterShape)v,
            new (string, object)[] { ("従来 (丸と 4芒星)", GlitterShape.Mixed), ("丸", GlitterShape.Circle), ("4芒星", GlitterShape.Star4), ("5芒星", GlitterShape.Star5), ("ハート", GlitterShape.Heart), ("ダイヤ", GlitterShape.Diamond), ("桜の花びら", GlitterShape.SakuraPetal) },
            "従来: 丸い粒で、5 粒に 1 粒が 4芒星。ほかの形は「形の粒の割合」の粒がその形になり、残りは丸になります"),
        new SliderField<IrisParameters>("形の粒の割合", p => p.ParticleShapeRatio, (p, v) => p.ParticleShapeRatio = v,
            IrisParameters.MinParticleShapeRatio, 1, 0.01, Fmt.Percent, "選んだ形にする粒の割合。残りは丸い粒になります (従来・丸では使いません)"),
        new HeadingField<IrisParameters>("光るラメ (0 でにじみ・フレアなし)"),
        new SliderField<IrisParameters>("光のにじみ", p => p.ParticleGlow, (p, v) => p.ParticleGlow = v, 0, 1, 0.01, Fmt.Percent),
        new ToggleField<IrisParameters>("にじみの色を変える", p => p.ParticleGlowCustomColor, (p, v) => p.ParticleGlowCustomColor = v),
        new ColorField<IrisParameters>("にじみの色", p => p.ParticleGlowColor, (p, v) => p.ParticleGlowColor = v),
        new SliderField<IrisParameters>("十字フレアの割合", p => p.ParticleFlareRatio, (p, v) => p.ParticleFlareRatio = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("フレアの長さ", p => p.ParticleFlareLength, (p, v) => p.ParticleFlareLength = v, 1, 8, 0.1, Fmt.F1),
        new ChoiceField<IrisParameters>("ラメの重ね方", p => p.GlitterBlend, (p, v) => p.GlitterBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) }, "スクリーン: 明るくなるだけ (発光)。覆い焼き: 強い発光。オーバーレイ: 明るいところをより明るく、暗いところをより暗く。ソフトライト: オーバーレイの弱いもの"),
        .. RangeFields(PatternLayer.Glitter),
    ];

    private static readonly (string, object)[] Blends =
        { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) };

    public static readonly IReadOnlyList<Field<IrisParameters>> Lens =
    [
        new HeadingField<IrisParameters>("中心の光", "虹彩の中心のぼんやりした光です (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("中心の光の強さ", p => p.CenterGlowIntensity, (p, v) => p.CenterGlowIntensity = v, 0, 1, 0.01, Fmt.Percent, "光の明るさ。0% でオフ"),
        new ColorField<IrisParameters>("中心の光の色", p => p.CenterGlowColor, (p, v) => p.CenterGlowColor = v),
        new HeadingField<IrisParameters>("集光", "透明なドームのレンズが集めた光です。「光の入る向き」と反対側に出ます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("集光の強さ", p => p.CausticIntensity, (p, v) => p.CausticIntensity = v, 0, 1, 0.01, Fmt.Percent, "集めた光の明るさ。0% でオフ"),
        new ColorField<IrisParameters>("集光の色", p => p.CausticColor, (p, v) => p.CausticColor = v),
        new ChoiceField<IrisParameters>("集光の形", p => p.CausticShape, (p, v) => p.CausticShape = (CausticShape)v,
            new (string, object)[] { ("三日月", CausticShape.Crescent), ("丸", CausticShape.Round) },
            "丸: ぼんやりした光のたまり。三日月: 虹彩のふちに沿った弧の照り返し"),
        new SliderField<IrisParameters>("集光の位置", p => p.CausticOffset, (p, v) => p.CausticOffset = v, 0, IrisParameters.MaxCausticOffset, 0.01, Fmt.Percent,
            "中心から、光と反対側へどれだけ寄せるか。虹彩の半径に対する割合"),
        new SliderField<IrisParameters>("集光の大きさ", p => p.CausticSize, (p, v) => p.CausticSize = v,
            IrisParameters.MinCausticSize, IrisParameters.MaxCausticSize, 0.01, Fmt.Percent, "光の半径。虹彩の半径に対する割合"),
        new ChoiceField<IrisParameters>("集光の重ね方", p => p.CausticBlend, (p, v) => p.CausticBlend = (LayerBlend)v, Blends,
            "スクリーン (最初の値) は明るくなるだけです。覆い焼きは強く光ります"),
        new HeadingField<IrisParameters>("縁の屈折", "外輪のすぐ内側に出る光の帯です。「光の入る向き」と反対側を強くできます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("縁の屈折の強さ", p => p.DomeEdgeIntensity, (p, v) => p.DomeEdgeIntensity = v, 0, 1, 0.01, Fmt.Percent, "帯の濃さ。0% でオフ"),
        new ColorField<IrisParameters>("縁の屈折の色", p => p.DomeEdgeColor, (p, v) => p.DomeEdgeColor = v),
        new SliderField<IrisParameters>("縁の屈折の幅", p => p.DomeEdgeWidth, (p, v) => p.DomeEdgeWidth = v,
            IrisParameters.MinDomeEdgeWidth, IrisParameters.MaxDomeEdgeWidth, 0.01, Fmt.Percent, "外輪の内側から中へ、だんだん消えていく幅。虹彩の半径に対する割合"),
        new SliderField<IrisParameters>("縁の屈折の偏り", p => p.DomeEdgeBias, (p, v) => p.DomeEdgeBias = v, 0, 1, 0.01, Fmt.Percent,
            "0% で一周同じ、100% で光と反対側だけ"),
        new HeadingField<IrisParameters>("宝石カット面", "虹彩全体に、カットした宝石を上から見たような明暗の面を重ねます (強さ 0% でオフ)"),
        new SliderField<IrisParameters>("強さ", p => p.IrisFacetIntensity, (p, v) => p.IrisFacetIntensity = v, 0, 1, 0.01, Fmt.Percent, "面の明暗の強さ。0% でオフ"),
        new SliderField<IrisParameters>("面の数", p => p.IrisFacetCount, (p, v) => p.IrisFacetCount = Int(v),
            IrisParameters.MinIrisFacetCount, IrisParameters.MaxIrisFacetCount, 1, v => Fmt.Count(v, "面"), "宝石の対称の数 (テーブル面の角の数)"),
        new SliderField<IrisParameters>("テーブルの大きさ", p => p.IrisFacetTableSize, (p, v) => p.IrisFacetTableSize = v,
            IrisParameters.MinIrisFacetTableSize, IrisParameters.MaxIrisFacetTableSize, 0.01, Fmt.Percent, "中心の平らな面 (テーブル面) の大きさ。虹彩の半径に対する割合"),
        new SliderField<IrisParameters>("明暗の向き", p => p.IrisFacetShadeAngle, (p, v) => p.IrisFacetShadeAngle = v, 0, 360, 1, Fmt.Deg,
            "暗くなる側の向き。0° = 右、90° = 下、180° = 左、270° = 上 (右目は自動で鏡像)"),
        new SliderField<IrisParameters>("明暗の偏り", p => p.IrisFacetShadeBias, (p, v) => p.IrisFacetShadeBias = v, 0, 1, 0.01, Fmt.Percent,
            "0% で面ごとに交互の明暗だけ。上げるほど、向きによる明暗が強くなります"),
        new ChoiceField<IrisParameters>("重ね方", p => p.IrisFacetBlend, (p, v) => p.IrisFacetBlend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) },
            "オーバーレイ (最初の値) とソフトライトは虹彩の色を残したまま明暗を付けます"),
        .. RangeFields(PatternLayer.IrisFacets),
    ];

    public static readonly IReadOnlyList<Field<IrisParameters>> OddEye = new Field<IrisParameters>[]
    {
        new HeadingField<IrisParameters>("オッドアイ"),
        new ToggleField<IrisParameters>("オッドアイ (左右異色瞳) を有効化", p => p.IsOddEye, (p, v) => p.IsOddEye = v,
            "左目と右目で異なる瞳色・シードを使います (サイズ・形状・位置は同じ)"),
        new ColorField<IrisParameters>("右目ベース色", p => p.OddEyeBaseColor, (p, v) => p.OddEyeBaseColor = v),
        new ColorField<IrisParameters>("右目内周色", p => p.OddEyeInnerColor, (p, v) => p.OddEyeInnerColor = v),
        new ColorField<IrisParameters>("右目外周色", p => p.OddEyeOuterColor, (p, v) => p.OddEyeOuterColor = v),
        new ColorField<IrisParameters>("右目ライン色", p => p.OddEyeRayColor, (p, v) => p.OddEyeRayColor = v, AllowGradient: true),
        new ColorField<IrisParameters>("右目反射光色", p => p.OddEyeBottomHighlightColor, (p, v) => p.OddEyeBottomHighlightColor = v),
    };

    public static readonly IReadOnlyList<Field<DecorativeSymbolItem>> Symbol = new Field<DecorativeSymbolItem>[]
    {
        new ToggleField<DecorativeSymbolItem>("表示する", s => s.Enabled, (s, v) => s.Enabled = v),
        new ChoiceField<DecorativeSymbolItem>("形状", s => s.Type, (s, v) => s.Type = (SymbolType)v,
            new (string, object)[] { ("なし", SymbolType.None) }.Concat(Shapes(SymbolOf)).ToArray()),
        new ColorField<DecorativeSymbolItem>("色", s => s.Color, (s, v) => s.Color = v, AllowGradient: true),
        new SliderField<DecorativeSymbolItem>("サイズ", s => s.Size, (s, v) => s.Size = v, 0.05, 0.40, 0.01, Fmt.Percent),
        new SliderField<DecorativeSymbolItem>("繰り返し数", s => s.Count, (s, v) => s.Count = Int(v), 1, 12, 1, v => Fmt.Count(v, "個")),
        new SliderField<DecorativeSymbolItem>("位置 X (横)", s => s.PositionX, (s, v) => s.PositionX = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<DecorativeSymbolItem>("位置 Y (縦)", s => s.PositionY, (s, v) => s.PositionY = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<DecorativeSymbolItem>("回転角度", s => s.Rotation, (s, v) => s.Rotation = v, -180, 180, 1, Fmt.Deg),
        new SliderField<DecorativeSymbolItem>("角の丸み", s => s.CornerRounding, (s, v) => s.CornerRounding = v, 0, 1, 0.01, Fmt.Percent),
        StarCoreField<DecorativeSymbolItem>(s => s.StarCoreScale, (s, v) => s.StarCoreScale = v),
        Stretch<DecorativeSymbolItem>("上の長さ", s => s.StretchTop, (s, v) => s.StretchTop = v),
        Stretch<DecorativeSymbolItem>("下の長さ", s => s.StretchBottom, (s, v) => s.StretchBottom = v),
        Stretch<DecorativeSymbolItem>("左の長さ", s => s.StretchLeft, (s, v) => s.StretchLeft = v),
        Stretch<DecorativeSymbolItem>("右の長さ", s => s.StretchRight, (s, v) => s.StretchRight = v),
        Transparency<DecorativeSymbolItem>(s => s.Opacity, (s, v) => s.Opacity = v),
        new ChoiceField<DecorativeSymbolItem>("重ね方", s => s.Blend, (s, v) => s.Blend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) },
            "この記号の重ね方。スクリーン: 明るくなるだけ (発光)。覆い焼き: 強い発光。オーバーレイ: 明るいところをより明るく、暗いところをより暗く。ソフトライト: オーバーレイの弱いもの"),
        new ChoiceField<DecorativeSymbolItem>("右目の反転", s => s.RightEyeFlip, (s, x) => s.RightEyeFlip = (RightEyeFlip)x, ItemFlips, "右目での描き方。鏡像: 位置も形の向きも左右反転 (左右対称)。位置だけ反転: 置く場所は左右対称で、形の向きはそのまま (三日月などが両目で同じ向き)。反転しない: 両目でまったく同じ (光源が固定された自然な光)"),
    };

    public static readonly IReadOnlyList<Field<HighlightItem>> Highlight = new Field<HighlightItem>[]
    {
        new ToggleField<HighlightItem>("表示する", h => h.Enabled, (h, v) => h.Enabled = v),
        new ChoiceField<HighlightItem>("形状", h => h.Shape, (h, v) => h.Shape = (HighlightShape)v,
            [.. Shapes(HighlightOf), ("弧", HighlightShape.Arc)]),
        new ColorField<HighlightItem>("色", h => h.Color, (h, v) => h.Color = v, AllowGradient: true),
        new SliderField<HighlightItem>("サイズ", h => h.Size, (h, v) => h.Size = v, 0.05, 0.45, 0.01, Fmt.Percent),
        new SliderField<HighlightItem>("繰り返し数", h => h.Count, (h, v) => h.Count = Int(v), 1, 12, 1, v => Fmt.Count(v, "個"),
            "同じハイライトをいくつ並べるか。2 以上にすると、位置のまわりに円を描くように並びます"),
        new SliderField<HighlightItem>("位置 X (横)", h => h.PositionX, (h, v) => h.PositionX = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<HighlightItem>("位置 Y (縦)", h => h.PositionY, (h, v) => h.PositionY = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<HighlightItem>("回転角度", h => h.Rotation, (h, v) => h.Rotation = v, -180, 180, 1, Fmt.Deg),
        new SliderField<HighlightItem>("角の丸み", h => h.CornerRounding, (h, v) => h.CornerRounding = v, 0, 1, 0.01, Fmt.Percent,
            "星の先・ダイヤの角などを丸めます。0% で従来どおりのとがった形です。正円には影響しません"),
        StarCoreField<HighlightItem>(h => h.StarCoreScale, (h, v) => h.StarCoreScale = v),
        new SliderField<HighlightItem>("弧の反り", h => h.ArcCurve, (h, v) => h.ArcCurve = v, 0, HighlightItem.MaxArcCurve, 0.01, Fmt.Percent,
            "弧の形だけに使います。真ん中が先よりどれだけ盛り上がるか。0% でまっすぐな光の筋になります"),
        new SliderField<HighlightItem>("弧の太さ", h => h.ArcThickness, (h, v) => h.ArcThickness = v,
            HighlightItem.MinArcThickness, HighlightItem.MaxArcThickness, 0.01, Fmt.Percent, "弧の形だけに使います。真ん中の太さで、先はとがります"),
        Stretch<HighlightItem>("上の長さ", h => h.StretchTop, (h, v) => h.StretchTop = v),
        Stretch<HighlightItem>("下の長さ", h => h.StretchBottom, (h, v) => h.StretchBottom = v),
        Stretch<HighlightItem>("左の長さ", h => h.StretchLeft, (h, v) => h.StretchLeft = v),
        Stretch<HighlightItem>("右の長さ", h => h.StretchRight, (h, v) => h.StretchRight = v),
        Transparency<HighlightItem>(h => h.Opacity, (h, v) => h.Opacity = v),
        new SliderField<HighlightItem>("輪郭のぼかし", h => h.Softness, (h, v) => h.Softness = v, 0, 1, 0.01, Fmt.Percent,
            "ハイライトの輪郭をぼかして、まわりになじませます。0% でくっきりした輪郭です"),
        new SliderField<HighlightItem>("光のにじみ", h => h.Glow, (h, v) => h.Glow = v, 0, 1, 0.01, Fmt.Percent,
            "ハイライトのまわりに光をにじませて、自分で光っているように見せます。重ね方をスクリーンにすると、より発光らしくなります"),
        new ChoiceField<HighlightItem>("重ね方", h => h.Blend, (h, v) => h.Blend = (LayerBlend)v,
            new (string, object)[] { ("通常", LayerBlend.Normal), ("スクリーン", LayerBlend.Screen), ("覆い焼き", LayerBlend.ColorDodge), ("オーバーレイ", LayerBlend.Overlay), ("ソフトライト", LayerBlend.SoftLight) },
            "このハイライトの重ね方。スクリーン: 明るくなるだけ (発光)。覆い焼き: 強い発光。オーバーレイ: 明るいところをより明るく、暗いところをより暗く。ソフトライト: オーバーレイの弱いもの"),
        new ChoiceField<HighlightItem>("右目の反転", h => h.RightEyeFlip, (h, x) => h.RightEyeFlip = (RightEyeFlip)x, ItemFlips, "右目での描き方。鏡像: 位置も形の向きも左右反転 (左右対称)。位置だけ反転: 置く場所は左右対称で、形の向きはそのまま (三日月などが両目で同じ向き)。反転しない: 両目でまったく同じ (光源が固定された自然な光)"),
    };

    private static bool IsPosition<T>(Field<T> f) => f.Label.StartsWith("位置 ", StringComparison.Ordinal);

    /// <summary>まとめて変更 of the ticked symbols: the symbol's settings without the position (and the 表示 check box, shown on its own).</summary>
    public static readonly IReadOnlyList<Field<DecorativeSymbolItem>> SymbolBulk = Symbol.Where(f => !IsPosition(f)).ToArray();

    /// <summary>まとめて変更 of the ticked highlights: the highlight's settings without the position.</summary>
    public static readonly IReadOnlyList<Field<HighlightItem>> HighlightBulk = Highlight.Where(f => !IsPosition(f)).ToArray();

    private static Field<T>[] Pick<T>(IEnumerable<Field<T>> fields, params string[] labels) =>
        labels.Select(l => fields.Single(f => f.Label == l)).ToArray();

    // 並べて追加 (the same labels and ranges as the desktop app's 「📐 並べて追加」)
    public static readonly IReadOnlyList<Field<ItemLayoutSettings>> Layout = new Field<ItemLayoutSettings>[]
    {
        new SliderField<ItemLayoutSettings>("個数", l => l.Count, (l, v) => l.Count = Int(v),
            ItemLayoutSettings.MinCount, ItemLayoutSettings.MaxCount, 1, v => Fmt.Count(v, "個")),
        new SliderField<ItemLayoutSettings>("範囲 内側 (中心からの距離)", l => l.InnerRadius, (l, v) => l.InnerRadius = v,
            0, ItemLayoutSettings.MaxRadius, 0.01, Fmt.F2, "虹彩の中心からこれより近くには置きません (瞳孔の上にのせないため)。1 で虹彩の縁"),
        new SliderField<ItemLayoutSettings>("範囲 外側 (中心からの距離)", l => l.OuterRadius, (l, v) => l.OuterRadius = v,
            0.05, ItemLayoutSettings.MaxRadius, 0.01, Fmt.F2, "これより遠く (縁の近く) には置きません。1 で虹彩の縁"),
        new ToggleField<ItemLayoutSettings>("向きをランダムにする", l => l.RandomRotation, (l, v) => l.RandomRotation = v,
            "オンで 1 つずつ回転角度をランダムにします。オフでは全部まっすぐ (0°) です"),
    };

    /// <summary>The look of the symbols 並べて追加 makes.</summary>
    public static readonly IReadOnlyList<Field<DecorativeSymbolItem>> SymbolTemplate =
        Pick(Symbol, "形状", "色", "サイズ", "角の丸み", "星の芯の太さ", "上の長さ", "下の長さ", "左の長さ", "右の長さ", "透明度", "重ね方", "右目の反転");

    /// <summary>The look of the highlights 並べて追加 makes.</summary>
    public static readonly IReadOnlyList<Field<HighlightItem>> HighlightTemplate =
        Pick(Highlight, "形状", "色", "サイズ", "角の丸み", "星の芯の太さ", "上の長さ", "下の長さ", "左の長さ", "右の長さ", "透明度", "重ね方", "右目の反転");

    /// <summary>A new decorative symbol, as the desktop app's 「➕ 装飾記号追加」 makes it.</summary>
    public static DecorativeSymbolItem NewSymbol() => new()
    {
        Type = SymbolType.Star4, Count = 1, Size = 0.18, PositionX = 0.25, PositionY = -0.15,
        Color = "#FFFFFF", Opacity = 0.85, Enabled = true
    };

    /// <summary>A new highlight, as the desktop app's 「➕ ハイライト追加」 makes it.</summary>
    public static HighlightItem NewHighlight() => new()
    {
        Shape = HighlightShape.Circle, PositionX = 0.25, PositionY = 0.25, Size = 0.15,
        Color = "#FFFFFF", Opacity = 0.85, Enabled = true
    };

    /// <summary>Applies an odd-eye template the way the desktop app does (turns odd-eye on, sets both eyes' colors).</summary>
    public static void ApplyOddEyeTemplate(IrisParameters p, OddEyeTemplate t)
    {
        p.IsOddEye = true;
        p.BaseColor = t.LeftBaseColor;
        p.InnerColor = t.LeftInnerColor;
        p.OuterColor = t.LeftOuterColor;
        p.RayColor = t.LeftRayColor;
        p.OddEyeBaseColor = t.RightBaseColor;
        p.OddEyeInnerColor = t.RightInnerColor;
        p.OddEyeOuterColor = t.RightOuterColor;
        p.OddEyeRayColor = t.RightRayColor;
    }
}
