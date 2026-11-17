using System;

namespace DollIrisGenerator.Models
{
    public enum IrisShape
    {
        Circle,         // 正円
        VerticalOval,   // 縦楕円
        HorizontalOval  // 横楕円
    }

    public enum PupilShape
    {
        Round,          // 正円
        VerticalOval,   // 縦長楕円
        CatSlit,        // 猫目スリット
        Heart,          // ハート型
        Star            // 星型
    }

    /// <summary>
    /// The shapes a decorative symbol and a highlight can take (one list for both, drawn by
    /// <c>ShapeOutline.ForShape</c>). <see cref="SymbolType"/> and <see cref="HighlightShape"/> keep the numbers their
    /// design files have always stored and map onto this list.
    /// </summary>
    public enum ShapeKind
    {
        Circle,         // 正円
        Star4,          // 4芒星
        Star5,          // 5芒星
        Heart,          // ハート
        Moon,           // 三日月
        Cross,          // クロス (十字架)
        Diamond,        // ダイヤ (菱形)
        SakuraPetal,    // 桜の花びら
        CatSlit         // 猫目スリット
    }

    /// <summary>Decorative symbol types. The numbers are what design files store: new shapes are added at the end.</summary>
    public enum SymbolType
    {
        None = 0,           // なし
        Star4 = 1,          // 4芒星
        Star5 = 2,          // 5芒星
        Heart = 3,          // ハート
        Moon = 4,           // 三日月
        Cross = 5,          // 十字架
        Diamond = 6,        // 菱形 / ダイヤ
        SakuraPetal = 7,    // 桜の花びら (Cherry Blossom Petal)
        Circle = 8,         // 正円
        CatSlit = 9         // 猫目スリット
    }

    /// <summary>
    /// How a layer is laid over what is under it (CSS / SVG mix-blend-mode). The numbers are saved in design files.
    /// </summary>
    public enum LayerBlend
    {
        Normal = 0,     // 通常
        Screen = 1,     // スクリーン (明るくなるだけ。発光)
        ColorDodge = 2, // 覆い焼き (強い発光)
        Overlay = 3,    // オーバーレイ (明るいところをより明るく、暗いところをより暗く)
        SoftLight = 4   // ソフトライト (オーバーレイの弱いもの)
    }

    /// <summary>
    /// Highlight shapes: the same list as the decorative symbols (<see cref="ShapeKind"/>). The numbers are what
    /// design files store, so they differ from <see cref="SymbolType"/>'s; 1 was the oval, which is now a circle with
    /// 上下左右の長さ (HighlightItem.StretchLeft / StretchRight), so it is left out and reads as a circle.
    /// </summary>
    public enum HighlightShape
    {
        Circle = 0,         // 正円
        Star4 = 2,          // 4芒星 / キラキラ (the highlight's is thinner than the symbol's)
        Diamond = 3,        // ダイヤ / 菱形 (the highlight's is a little narrower than the symbol's)
        Star5 = 4,          // 5芒星
        Heart = 5,          // ハート
        Moon = 6,           // 三日月
        Cross = 7,          // クロス
        SakuraPetal = 8,    // 桜の花びら
        CatSlit = 9         // 猫目スリット
    }

    public static class ShapeKinds
    {
        /// <summary>The shape a symbol type draws; null for <see cref="SymbolType.None"/>.</summary>
        public static ShapeKind? Of(SymbolType type) => type switch
        {
            SymbolType.Star4 => ShapeKind.Star4,
            SymbolType.Star5 => ShapeKind.Star5,
            SymbolType.Heart => ShapeKind.Heart,
            SymbolType.Moon => ShapeKind.Moon,
            SymbolType.Cross => ShapeKind.Cross,
            SymbolType.Diamond => ShapeKind.Diamond,
            SymbolType.SakuraPetal => ShapeKind.SakuraPetal,
            SymbolType.Circle => ShapeKind.Circle,
            SymbolType.CatSlit => ShapeKind.CatSlit,
            _ => null
        };

        /// <summary>The shape a highlight draws (unknown numbers: the circle).</summary>
        public static ShapeKind Of(HighlightShape shape) => shape switch
        {
            HighlightShape.Star4 => ShapeKind.Star4,
            HighlightShape.Diamond => ShapeKind.Diamond,
            HighlightShape.Star5 => ShapeKind.Star5,
            HighlightShape.Heart => ShapeKind.Heart,
            HighlightShape.Moon => ShapeKind.Moon,
            HighlightShape.Cross => ShapeKind.Cross,
            HighlightShape.SakuraPetal => ShapeKind.SakuraPetal,
            HighlightShape.CatSlit => ShapeKind.CatSlit,
            _ => ShapeKind.Circle
        };

        /// <summary>True for the stars, whose core thickness (星の芯の太さ) can be changed.</summary>
        public static bool IsStar(ShapeKind kind) => kind is ShapeKind.Star4 or ShapeKind.Star5;

        /// <summary>Shapes that are not the same mirrored (the moon): the right eye mirrors the whole outline.</summary>
        public static bool IsAsymmetric(ShapeKind kind) => kind == ShapeKind.Moon;
    }

    public enum EyeSide
    {
        Left,
        Right
    }
}
