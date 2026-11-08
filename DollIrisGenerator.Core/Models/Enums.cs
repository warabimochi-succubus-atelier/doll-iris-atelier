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

    public enum SymbolType
    {
        None,           // なし
        Star4,          // 4芒星
        Star5,          // 5芒星
        Heart,          // ハート
        Moon,           // 三日月
        Cross,          // 十字架
        Diamond,        // 菱形 / ダイヤ
        SakuraPetal     // 桜の花びら (Cherry Blossom Petal)
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

    public enum HighlightShape
    {
        Circle,         // 正円
        Oval,           // 楕円
        Star4,          // 4芒星 / キラキラ
        Rhombus         // 菱形
    }

    public enum EyeSide
    {
        Left,
        Right
    }
}
