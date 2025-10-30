/* ----- ----- ----- ----- */
// PieceSettings.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/05/07
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Drawing;

using Engine.Styles;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    public static class PieceSettings
    {
        // Size
        public const int Radius = 35;
        public const int RedOutlineWidth = 3;
        public const int BlackOutlineWidth = 2;
        public const int OuterMargin = 6;
        public const int GlowMargin = 6;

        // Font
        public static readonly Font Font = StyleHelper.GetFont("NotoSerif", 30, FontStyle.Bold);

        // Red piece color
        public static readonly Brush RedTextBrush = StyleHelper.GetBrush("#E83015");  // #E83015
        public static readonly Brush RedBackgroundBrush = StyleHelper.GetBrush("#FCFAF2");  // #FCFAF2
        public static readonly Color RedOutlineColor = StyleHelper.GetColor("#E83015");  // #E83015
        // Black piece color
        public static readonly Brush BlackTextBrush = StyleHelper.GetBrush("#FCFAF2");  // #FCFAF2
        public static readonly Brush BlackBackgroundBrush = StyleHelper.GetBrush("#1C1C1C");  // #1C1C1C
        public static readonly Color BlackOutlineColor = StyleHelper.GetColor("#FCFAF2");  // #FCFAF2
        // Glow color
        public static readonly Color GlowColor = StyleHelper.GetColor("#FFB11B", 0.75f);  // #FFB11B
    }
}