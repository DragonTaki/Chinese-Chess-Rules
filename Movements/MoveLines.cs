/* ----- ----- ----- ----- */
// MoveLines.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

using Chinese_Chess_v3.Game.Core.Boards;

namespace Chinese_Chess_v3.Game.Core.Movements
{
    /// <summary>Straight-line helpers shared by every rules family (sliding pieces, cannon screens).</summary>
    public static class MoveLines
    {
        /// <summary>
        /// How many pieces stand strictly between (startX, startY) and (endX, endY), which must be
        /// on one row or column (the squares are walked one step at a time).
        /// </summary>
        public static int CountPiecesBetween(Board board, int startX, int startY, int endX, int endY)
        {
            int dx = Math.Sign(endX - startX);
            int dy = Math.Sign(endY - startY);
            int count = 0;
            int x = startX + dx;
            int y = startY + dy;
            while (x != endX || y != endY)
            {
                if (board.Grid[x, y] != null)
                    count++;
                x += dx;
                y += dy;
            }
            return count;
        }
    }
}
