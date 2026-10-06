/* ----- ----- ----- ----- */
// Horse.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/06
// Version: v3.0
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Pieces.PieceTypes
{
    /// <summary>
    /// Represents the <b>Horse (傌/馬)</b> piece in Chinese Chess.
    /// The Horse moves in an L-shape (two squares in one direction, one square in perpendicular direction) and
    /// cannot jump over a piece directly adjacent in the primary direction ("horse leg" rule).
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class Horse : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Horse"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Horse(PieceInfo info)
            : base(info) { }
    }
}
