/* ----- ----- ----- ----- */
// PieceConstants.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/05/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    public static class PieceConstants
    {
        private static readonly Dictionary<PieceType, string[]> _pieceTextMap = new()
        {
            { PieceType.None   ,  new[] { "？", "？", "？" } },
            { PieceType.General,  new[] { "帥", "將", "王" } },
            { PieceType.Advisor,  new[] { "仕", "士", "士" } },
            { PieceType.Elephant, new[] { "相", "象", "向" } },
            { PieceType.Chariot,  new[] { "俥", "車", "車" } },
            { PieceType.Horse,    new[] { "傌", "馬", "馬" } },
            { PieceType.Cannon,   new[] { "炮", "包", "礮" } },
            { PieceType.Soldier,  new[] { "兵", "卒", "勇" } },
        };

        /// <summary>
        /// 取得棋子文字表示，依 PieceColor 選對應文字
        /// 索引對應：
        /// Red = 0, Black = 1, None/Yellow = 2
        /// </summary>
        public static string GetPieceText(PieceType type, PieceColor color)
        {
            if (!_pieceTextMap.TryGetValue(type, out var texts))
                return _pieceTextMap[PieceType.None][2];

            return color switch
            {
                PieceColor.Red   => texts[0],
                PieceColor.Black => texts[1],
                _ => texts[2],  // None / Yellow / 未定義顏色
            };
        }

        // Piece initial locations
        private static readonly (PieceType type, int x, int y, PieceColor color, PlayerSide side)[] ClassicPieceData = new[]
        {
            (PieceType.General,  4, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Advisor,  3, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Advisor,  5, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Elephant, 2, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Elephant, 6, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Horse,    1, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Horse,    7, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Chariot,  0, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Chariot,  8, 0, PieceColor.Black, PlayerSide.Black),
            (PieceType.Cannon,   1, 2, PieceColor.Black, PlayerSide.Black),
            (PieceType.Cannon,   7, 2, PieceColor.Black, PlayerSide.Black),
            (PieceType.Soldier,  0, 3, PieceColor.Black, PlayerSide.Black),
            (PieceType.Soldier,  2, 3, PieceColor.Black, PlayerSide.Black),
            (PieceType.Soldier,  4, 3, PieceColor.Black, PlayerSide.Black),
            (PieceType.Soldier,  6, 3, PieceColor.Black, PlayerSide.Black),
            (PieceType.Soldier,  8, 3, PieceColor.Black, PlayerSide.Black),

            (PieceType.General,  4, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Advisor,  3, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Advisor,  5, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Elephant, 2, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Elephant, 6, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Horse,    1, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Horse,    7, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Chariot,  0, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Chariot,  8, 9, PieceColor.Red, PlayerSide.Red),
            (PieceType.Cannon,   1, 7, PieceColor.Red, PlayerSide.Red),
            (PieceType.Cannon,   7, 7, PieceColor.Red, PlayerSide.Red),
            (PieceType.Soldier,  0, 6, PieceColor.Red, PlayerSide.Red),
            (PieceType.Soldier,  2, 6, PieceColor.Red, PlayerSide.Red),
            (PieceType.Soldier,  4, 6, PieceColor.Red, PlayerSide.Red),
            (PieceType.Soldier,  6, 6, PieceColor.Red, PlayerSide.Red),
            (PieceType.Soldier,  8, 6, PieceColor.Red, PlayerSide.Red),
        };

        public static List<PieceInfo> InitialClassicPieces = ClassicPieceData
            .Select(p => new PieceInfo(
                p.type,
                p.x,
                p.y,
                p.color,
                p.side,
                isFaceUp: true,
                isDead: false,
                turnIndex: 0))
            .ToList();
    }
}
