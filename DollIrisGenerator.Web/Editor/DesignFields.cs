using DollIrisGenerator.Models;

namespace DollIrisGenerator.Web.Editor;

/// <summary>
/// The settings tabs, in the same groups, labels and ranges as the desktop app (MainWindow.xaml), so a design
/// made in either app is edited the same way.
/// </summary>
public static class DesignFields
{
    private static int Int(double v) => (int)Math.Round(v);

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
    };

    public static readonly IReadOnlyList<Field<IrisParameters>> Colors = new Field<IrisParameters>[]
    {
        new HeadingField<IrisParameters>("虹彩カラー"),
        new ColorField<IrisParameters>("ベース色 (中間色)", p => p.BaseColor, (p, v) => p.BaseColor = v),
        new ColorField<IrisParameters>("内周色 (中心グラデ)", p => p.InnerColor, (p, v) => p.InnerColor = v),
        new SliderField<IrisParameters>("内周グラデ範囲", p => p.InnerGradientRadius, (p, v) => p.InnerGradientRadius = v, 0.1, 0.9, 0.01, Fmt.Percent),
        new ColorField<IrisParameters>("外周色 (外郭グラデ)", p => p.OuterColor, (p, v) => p.OuterColor = v),
        new SliderField<IrisParameters>("外周グラデ範囲", p => p.OuterGradientRadius, (p, v) => p.OuterGradientRadius = v, 0.3, 1.2, 0.01, Fmt.Percent),
        new ColorField<IrisParameters>("下部反射光 (照り返し)", p => p.BottomHighlightColor, (p, v) => p.BottomHighlightColor = v),
        new SliderField<IrisParameters>("反射光の強さ", p => p.BottomHighlightIntensity, (p, v) => p.BottomHighlightIntensity = v, 0, 1, 0.01, Fmt.Percent),
        new HeadingField<IrisParameters>("オーロラ層", "虹彩全体に重ねる色。「円周」のグラデーションにすると、中心の周りで色が移り変わるオーロラ風になります"),
        new ColorField<IrisParameters>("オーロラ層の色", p => p.AuroraColor, (p, v) => p.AuroraColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("オーロラ層の強さ", p => p.AuroraOpacity, (p, v) => p.AuroraOpacity = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new HeadingField<IrisParameters>("星雲", "虹彩に重ねる雲のような模様。グラデーションにすると、場所ごとに雲の色が変わります (銀河・星雲風)"),
        new ColorField<IrisParameters>("星雲の色", p => p.NebulaColor, (p, v) => p.NebulaColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("星雲の強さ", p => p.NebulaOpacity, (p, v) => p.NebulaOpacity = v, 0, 1, 0.01, Fmt.Percent, "0 でオフ"),
        new SliderField<IrisParameters>("星雲の量", p => p.NebulaDensity, (p, v) => p.NebulaDensity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("星雲の細かさ", p => p.NebulaScale, (p, v) => p.NebulaScale = v,
            IrisParameters.MinNebulaScale, IrisParameters.MaxNebulaScale, 0.01, Fmt.F2),
        new SliderField<IrisParameters>("星雲の渦巻き", p => p.NebulaSwirl, (p, v) => p.NebulaSwirl = v, -1, 1, 0.01, Fmt.Percent,
            "マイナスで逆回り (右目は自動で鏡像)"),
    };

    public static readonly IReadOnlyList<Field<IrisParameters>> PupilAndRays = new Field<IrisParameters>[]
    {
        new HeadingField<IrisParameters>("瞳孔模様"),
        new ChoiceField<IrisParameters>("瞳孔形状", p => p.PupilShape, (p, v) => p.PupilShape = (PupilShape)v, new (string, object)[]
        {
            ("正円", PupilShape.Round), ("縦長楕円", PupilShape.VerticalOval), ("猫目スリット", PupilShape.CatSlit),
            ("ハート型", PupilShape.Heart), ("星型", PupilShape.Star)
        }),
        new SliderField<IrisParameters>("瞳孔サイズ (直径比率)", p => p.PupilSizeRatio, (p, v) => p.PupilSizeRatio = v, 0.10, 0.60, 0.01, Fmt.Percent),
        new ColorField<IrisParameters>("瞳孔色", p => p.PupilColor, (p, v) => p.PupilColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("透明度", p => p.PupilOpacity, (p, v) => p.PupilOpacity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("輪郭の柔らかさ (ぼかし)", p => p.PupilSoftness, (p, v) => p.PupilSoftness = v, 0, 0.30, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("角の丸み (猫目・ハート・星)", p => p.PupilCornerRounding, (p, v) => p.PupilCornerRounding = v, 0, 1, 0.01, Fmt.Percent,
            "とがった角を丸めます。正円・縦長楕円には影響しません"),
        new SliderField<IrisParameters>("宝石カット", p => p.PupilFacetIntensity, (p, v) => p.PupilFacetIntensity = v, 0, 1, 0.01, Fmt.Percent,
            "瞳孔の中に宝石のカット面のような明暗の面を入れます。0% でオフ"),
        new SliderField<IrisParameters>("カット面の数", p => p.PupilFacetCount, (p, v) => p.PupilFacetCount = Int(v),
            IrisParameters.MinPupilFacetCount, IrisParameters.MaxPupilFacetCount, 1, v => Fmt.Count(v, "面")),
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
    };

    public static readonly IReadOnlyList<Field<IrisParameters>> RingAndGlitter = new Field<IrisParameters>[]
    {
        new HeadingField<IrisParameters>("外輪"),
        new SliderField<IrisParameters>("リング幅 (太さ)", p => p.LimbalRingThickness, (p, v) => p.LimbalRingThickness = v, 0, 0.22, 0.005, Fmt.Percent),
        new ColorField<IrisParameters>("リング色", p => p.LimbalRingColor, (p, v) => p.LimbalRingColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("透明度", p => p.LimbalRingOpacity, (p, v) => p.LimbalRingOpacity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("境界の柔らかさ (ぼかし)", p => p.LimbalRingSoftness, (p, v) => p.LimbalRingSoftness = v, 0, 0.80, 0.01, Fmt.Percent),
        new HeadingField<IrisParameters>("粒子模様・ラメ"),
        new SliderField<IrisParameters>("粒子数量 (ラメ数)", p => p.ParticleCount, (p, v) => p.ParticleCount = Int(v), 0, 250, 5, v => Fmt.Count(v, "個")),
        new ColorField<IrisParameters>("ラメ色", p => p.ParticleColor, (p, v) => p.ParticleColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("粒径 (最大サイズ)", p => p.ParticleMaxSize, (p, v) => p.ParticleMaxSize = v, 0.5, 5.0, 0.1, Fmt.F1),
        new SliderField<IrisParameters>("透明度", p => p.ParticleOpacity, (p, v) => p.ParticleOpacity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("分布範囲 (外周比率)", p => p.ParticleRadiusRatio, (p, v) => p.ParticleRadiusRatio = v, 0.30, 0.95, 0.01, Fmt.Percent),
        new HeadingField<IrisParameters>("光るラメ (0 でにじみ・フレアなし)"),
        new SliderField<IrisParameters>("光のにじみ", p => p.ParticleGlow, (p, v) => p.ParticleGlow = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("十字フレアの割合", p => p.ParticleFlareRatio, (p, v) => p.ParticleFlareRatio = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("フレアの長さ", p => p.ParticleFlareLength, (p, v) => p.ParticleFlareLength = v, 1, 8, 0.1, Fmt.F1),
    };

    public static readonly IReadOnlyList<Field<IrisParameters>> Lens = new Field<IrisParameters>[]
    {
        new HeadingField<IrisParameters>("レンズ風パターン"),
        new ToggleField<IrisParameters>("光学レンズ・フレネル屈折リングを有効化", p => p.LensPatternEnabled, (p, v) => p.LensPatternEnabled = v),
        new SliderField<IrisParameters>("屈折リング数", p => p.LensRingCount, (p, v) => p.LensRingCount = Int(v), 2, 20, 1, v => Fmt.Count(v, "本")),
        new ColorField<IrisParameters>("リング線色", p => p.LensRingColor, (p, v) => p.LensRingColor = v, AllowGradient: true),
        new SliderField<IrisParameters>("リング強度 (濃度)", p => p.LensRingIntensity, (p, v) => p.LensRingIntensity = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<IrisParameters>("リング線幅", p => p.LensRingWidth, (p, v) => p.LensRingWidth = v, 0.3, 3.0, 0.1, Fmt.F1),
        new SliderField<IrisParameters>("中心屈折光 (コースティクス)", p => p.LensRefractionGlow, (p, v) => p.LensRefractionGlow = v, 0, 1, 0.01, Fmt.Percent),
    };

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

    public static readonly IReadOnlyList<Field<IrisParameters>> Symmetry = new Field<IrisParameters>[]
    {
        new HeadingField<IrisParameters>("左右対称設定"),
        new ToggleField<IrisParameters>("ハイライトも左右反転する (完全鏡像)", p => p.MirrorHighlightsWithIris, (p, v) => p.MirrorHighlightsWithIris = v,
            "オフにすると、光源位置が固定された自然なライティングになります"),
    };

    public static readonly IReadOnlyList<Field<DecorativeSymbolItem>> Symbol = new Field<DecorativeSymbolItem>[]
    {
        new ToggleField<DecorativeSymbolItem>("装飾記号を表示する", s => s.Enabled, (s, v) => s.Enabled = v),
        new ChoiceField<DecorativeSymbolItem>("図形種類", s => s.Type, (s, v) => s.Type = (SymbolType)v, new (string, object)[]
        {
            ("なし", SymbolType.None), ("4芒星", SymbolType.Star4), ("5芒星", SymbolType.Star5), ("ハート", SymbolType.Heart),
            ("三日月", SymbolType.Moon), ("クロス", SymbolType.Cross), ("ダイヤ", SymbolType.Diamond), ("桜の花びら", SymbolType.SakuraPetal)
        }),
        new ColorField<DecorativeSymbolItem>("記号色", s => s.Color, (s, v) => s.Color = v, AllowGradient: true),
        new SliderField<DecorativeSymbolItem>("サイズ", s => s.Size, (s, v) => s.Size = v, 0.05, 0.40, 0.01, Fmt.Percent),
        new SliderField<DecorativeSymbolItem>("繰り返し数", s => s.Count, (s, v) => s.Count = Int(v), 1, 12, 1, v => Fmt.Count(v, "個")),
        new SliderField<DecorativeSymbolItem>("位置 X (横)", s => s.PositionX, (s, v) => s.PositionX = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<DecorativeSymbolItem>("位置 Y (縦)", s => s.PositionY, (s, v) => s.PositionY = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<DecorativeSymbolItem>("回転角度", s => s.Rotation, (s, v) => s.Rotation = v, -180, 180, 1, Fmt.Deg),
        new SliderField<DecorativeSymbolItem>("角の丸み", s => s.CornerRounding, (s, v) => s.CornerRounding = v, 0, 1, 0.01, Fmt.Percent),
        new SliderField<DecorativeSymbolItem>("透明度", s => s.Opacity, (s, v) => s.Opacity = v, 0, 1, 0.01, Fmt.Percent),
    };

    public static readonly IReadOnlyList<Field<HighlightItem>> Highlight = new Field<HighlightItem>[]
    {
        new ToggleField<HighlightItem>("有効", h => h.Enabled, (h, v) => h.Enabled = v),
        new ChoiceField<HighlightItem>("形状", h => h.Shape, (h, v) => h.Shape = (HighlightShape)v, new (string, object)[]
        {
            ("正円", HighlightShape.Circle), ("楕円", HighlightShape.Oval), ("4点星", HighlightShape.Star4), ("菱形", HighlightShape.Rhombus)
        }),
        new ColorField<HighlightItem>("色", h => h.Color, (h, v) => h.Color = v, AllowGradient: true),
        new SliderField<HighlightItem>("サイズ", h => h.Size, (h, v) => h.Size = v, 0.05, 0.45, 0.01, Fmt.Percent),
        new SliderField<HighlightItem>("位置 X (横)", h => h.PositionX, (h, v) => h.PositionX = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<HighlightItem>("位置 Y (縦)", h => h.PositionY, (h, v) => h.PositionY = v, -0.85, 0.85, 0.01, Fmt.F2),
        new SliderField<HighlightItem>("楕円 縦横比", h => h.AspectRatio, (h, v) => h.AspectRatio = v, 0.3, 2.5, 0.01, Fmt.F2),
        new SliderField<HighlightItem>("回転角度", h => h.Rotation, (h, v) => h.Rotation = v, -180, 180, 1, Fmt.Deg),
        new SliderField<HighlightItem>("透明度", h => h.Opacity, (h, v) => h.Opacity = v, 0, 1, 0.01, Fmt.Percent),
    };

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
