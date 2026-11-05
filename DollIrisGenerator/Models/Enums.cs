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
