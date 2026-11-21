using DollIrisGenerator.Models;

namespace DollIrisGenerator.ViewModels
{
    /// <summary>
    /// Names a hand edit for the 生成履歴 list (「パラメータ調整: 瞳孔サイズ」), using the labels of the settings tabs,
    /// so the timeline says what was changed instead of a row of identical 「パラメータ調整」 entries.
    /// </summary>
    public static class ManualEditDescriber
    {
        public const string GenericAction = "パラメータ調整";

        private static readonly Dictionary<string, string> Labels = new()
        {
            [nameof(IrisParameters.Name)] = "デザイン名",
            [nameof(IrisParameters.SizeMm)] = "虹彩サイズ",
            [nameof(IrisParameters.Shape)] = "虹彩形状",
            [nameof(IrisParameters.AspectRatio)] = "楕円の縦横比率",
            [nameof(IrisParameters.TiltAngle)] = "楕円の傾き角度",
            [nameof(IrisParameters.OutlineTaper)] = "輪郭の上下の幅の差",
            [nameof(IrisParameters.OutlineTopFlat)] = "輪郭の上の平ら",
            [nameof(IrisParameters.OutlineBottomFlat)] = "輪郭の下の平ら",
            [nameof(IrisParameters.OutlineSquareness)] = "輪郭の四角さ",
            [nameof(IrisParameters.Seed)] = "乱数シード",
            [nameof(IrisParameters.BaseColor)] = "ベース色",
            [nameof(IrisParameters.InnerColor)] = "内周色",
            [nameof(IrisParameters.OuterColor)] = "外周色",
            [nameof(IrisParameters.BottomHighlightColor)] = "下部反射光",
            [nameof(IrisParameters.InnerGradientRadius)] = "内周グラデ範囲",
            [nameof(IrisParameters.OuterGradientRadius)] = "外周グラデ範囲",
            [nameof(IrisParameters.BottomHighlightIntensity)] = "反射光の強さ",
            [nameof(IrisParameters.AuroraColor)] = "オーロラ層の色",
            [nameof(IrisParameters.AuroraOpacity)] = "オーロラ層の強さ",
            [nameof(IrisParameters.NebulaColor)] = "星雲の色",
            [nameof(IrisParameters.NebulaOpacity)] = "星雲の強さ",
            [nameof(IrisParameters.NebulaDensity)] = "星雲の量",
            [nameof(IrisParameters.NebulaScale)] = "星雲の細かさ",
            [nameof(IrisParameters.NebulaSwirl)] = "星雲の渦巻き",
            [nameof(IrisParameters.GlitterBlend)] = "ラメの重ね方",
            [nameof(IrisParameters.AuroraBlend)] = "オーロラ層の重ね方",
            [nameof(IrisParameters.FiberNoiseScale)] = "繊維ノイズの細かさ",
            [nameof(IrisParameters.FiberNoise)] = "繊維ノイズ",
            [nameof(IrisParameters.LightStrength)] = "光の強さ",
            [nameof(IrisParameters.LightAngle)] = "光の向き",
            [nameof(IrisParameters.LimbalRingThickness)] = "外輪の幅",
            [nameof(IrisParameters.LimbalRingColor)] = "外輪の色",
            [nameof(IrisParameters.LimbalRingOpacity)] = "外輪の透明度",
            [nameof(IrisParameters.LimbalRingSoftness)] = "外輪のぼかし",
            [nameof(IrisParameters.PupilShape)] = "瞳孔形状",
            [nameof(IrisParameters.PupilSizeRatio)] = "瞳孔サイズ",
            [nameof(IrisParameters.PupilColor)] = "瞳孔色",
            [nameof(IrisParameters.PupilOpacity)] = "瞳孔の透明度",
            [nameof(IrisParameters.PupilSoftness)] = "瞳孔のぼかし",
            [nameof(IrisParameters.PupilCornerRounding)] = "瞳孔の角の丸み",
            [nameof(IrisParameters.PupilStarCoreScale)] = "瞳孔の星の芯の太さ",
            [nameof(IrisParameters.PupilRightEyeFlip)] = "瞳孔の右目の反転",
            [nameof(IrisParameters.PupilStretchTop)] = "瞳孔の上の長さ",
            [nameof(IrisParameters.PupilStretchBottom)] = "瞳孔の下の長さ",
            [nameof(IrisParameters.PupilStretchLeft)] = "瞳孔の左の長さ",
            [nameof(IrisParameters.PupilStretchRight)] = "瞳孔の右の長さ",
            [nameof(IrisParameters.PupilFacetIntensity)] = "宝石カット",
            [nameof(IrisParameters.PupilFacetCount)] = "カット面の数",
            [nameof(IrisParameters.CollaretteIntensity)] = "瞳孔まわりの輪の強さ",
            [nameof(IrisParameters.CollaretteColor)] = "瞳孔まわりの輪の色",
            [nameof(IrisParameters.CollaretteWidth)] = "瞳孔まわりの輪の幅",
            [nameof(IrisParameters.CollaretteJagged)] = "瞳孔まわりの輪のギザギザ",
            [nameof(IrisParameters.CollaretteSpikeCount)] = "瞳孔まわりの輪の山の数",
            [nameof(IrisParameters.CollaretteSoftness)] = "瞳孔まわりの輪の柔らかさ",
            [nameof(IrisParameters.CollaretteBlend)] = "瞳孔まわりの輪の重ね方",
            [nameof(IrisParameters.RayCount)] = "ライン本数",
            [nameof(IrisParameters.RayColor)] = "ライン色",
            [nameof(IrisParameters.RayThickness)] = "ラインの線幅",
            [nameof(IrisParameters.RayLengthRatio)] = "ラインの長さ",
            [nameof(IrisParameters.RayIntensity)] = "ラインの強度",
            [nameof(IrisParameters.RayRandomness)] = "ラインのランダム性",
            [nameof(IrisParameters.RayInnerRadiusRatio)] = "ラインの内側半径",
            [nameof(IrisParameters.RayTaper)] = "ラインの先細り",
            [nameof(IrisParameters.RayWidthVariation)] = "ラインの太さのばらつき",
            [nameof(IrisParameters.RayCurve)] = "ラインのうねり",
            [nameof(IrisParameters.RayFineLayer)] = "細線レイヤー",
            [nameof(IrisParameters.ParticleCount)] = "ラメの数",
            [nameof(IrisParameters.ParticleColor)] = "ラメ色",
            [nameof(IrisParameters.ParticleMinSize)] = "ラメの粒径",
            [nameof(IrisParameters.ParticleMaxSize)] = "ラメの粒径",
            [nameof(IrisParameters.ParticleOpacity)] = "ラメの透明度",
            [nameof(IrisParameters.ParticleRadiusRatio)] = "ラメの分布範囲",
            [nameof(IrisParameters.ParticleGlow)] = "ラメの光のにじみ",
            [nameof(IrisParameters.ParticleFlareRatio)] = "十字フレアの割合",
            [nameof(IrisParameters.ParticleFlareLength)] = "フレアの長さ",
            [nameof(IrisParameters.LensPatternEnabled)] = "レンズ風パターン",
            [nameof(IrisParameters.LensRingCount)] = "屈折リング数",
            [nameof(IrisParameters.LensRingColor)] = "リング線色",
            [nameof(IrisParameters.LensRingIntensity)] = "リング強度",
            [nameof(IrisParameters.LensRingWidth)] = "リング線幅",
            [nameof(IrisParameters.LensRefractionGlow)] = "中心屈折光",
            [nameof(IrisParameters.RayRangeAngle)] = "放射ラインの範囲の向き",
            [nameof(IrisParameters.RayRangeWidth)] = "放射ラインの範囲の広さ",
            [nameof(IrisParameters.RayRangeFeather)] = "放射ラインの範囲の端のぼかし",
            [nameof(IrisParameters.ParticleRangeAngle)] = "ラメの範囲の向き",
            [nameof(IrisParameters.ParticleRangeWidth)] = "ラメの範囲の広さ",
            [nameof(IrisParameters.ParticleRangeFeather)] = "ラメの範囲の端のぼかし",
            [nameof(IrisParameters.LensRangeAngle)] = "レンズ風リングの範囲の向き",
            [nameof(IrisParameters.LensRangeWidth)] = "レンズ風リングの範囲の広さ",
            [nameof(IrisParameters.LensRangeFeather)] = "レンズ風リングの範囲の端のぼかし",
            [nameof(IrisParameters.IrisFacetIntensity)] = "宝石カット面の強さ",
            [nameof(IrisParameters.IrisFacetCount)] = "宝石カット面の面の数",
            [nameof(IrisParameters.IrisFacetTableSize)] = "宝石カット面のテーブルの大きさ",
            [nameof(IrisParameters.IrisFacetShadeAngle)] = "宝石カット面の明暗の向き",
            [nameof(IrisParameters.IrisFacetShadeBias)] = "宝石カット面の明暗の偏り",
            [nameof(IrisParameters.IrisFacetBlend)] = "宝石カット面の重ね方",
            [nameof(IrisParameters.IrisFacetRangeAngle)] = "宝石カット面の範囲の向き",
            [nameof(IrisParameters.IrisFacetRangeWidth)] = "宝石カット面の範囲の広さ",
            [nameof(IrisParameters.IrisFacetRangeFeather)] = "宝石カット面の範囲の端のぼかし",
            [nameof(IrisParameters.LightRightEyeFlip)] = "光の向きの右目の反転",
            [nameof(IrisParameters.IsOddEye)] = "オッドアイ",
            [nameof(IrisParameters.OddEyeBaseColor)] = "右目ベース色",
            [nameof(IrisParameters.OddEyeInnerColor)] = "右目内周色",
            [nameof(IrisParameters.OddEyeOuterColor)] = "右目外周色",
            [nameof(IrisParameters.OddEyeRayColor)] = "右目ライン色",
            [nameof(IrisParameters.OddEyeBottomHighlightColor)] = "右目反射光色",
            [nameof(IrisParameters.OddEyeSeedOffset)] = "右目の模様のずらし量",
            [nameof(IrisParameters.Highlights)] = "ハイライト・装飾記号",
            [nameof(IrisParameters.Symbols)] = "ハイライト・装飾記号",
            [IrisParameters.NestedItemsChangedName] = "ハイライト・装飾記号",
        };

        /// <summary>
        /// 「パラメータ調整: 瞳孔サイズ」, 「パラメータ調整: 瞳孔サイズ ほか 2 項目」, or just 「パラメータ調整」
        /// when nothing recognisable changed. <paramref name="propertyNames"/> are in the order they were edited.
        /// </summary>
        public static string Describe(IEnumerable<string?> propertyNames)
        {
            var labels = propertyNames
                .Select(name => name != null && Labels.TryGetValue(name, out var label) ? label : null)
                .OfType<string>()
                .Distinct()
                .ToList();

            return labels.Count switch
            {
                0 => GenericAction,
                1 => $"{GenericAction}: {labels[0]}",
                _ => $"{GenericAction}: {labels[0]} ほか {labels.Count - 1} 項目",
            };
        }
    }
}
