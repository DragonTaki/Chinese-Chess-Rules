/* ----- ----- ----- ----- */
// GridPoint.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/05
// Update Date: 2026/10/05
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core.Boards
{
    /// <summary>
    /// A square on the board grid, in board coordinates (column <see cref="X"/>, row
    /// <see cref="Y"/>; the same space as <c>Piece.X</c>/<c>Piece.Y</c>). Core's own type so the
    /// rules layer needs no drawing-library types.
    /// </summary>
    /// <param name="X">Column index.</param>
    /// <param name="Y">Row index.</param>
    public readonly record struct GridPoint(int X, int Y);
}
