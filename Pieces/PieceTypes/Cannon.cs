/* ----- ----- ----- ----- */
// Cannon.cs
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
    /// Represents the <b>Cannon (炮/包)</b> piece in Chinese Chess.
    /// The Cannon moves like the Rook — any number of empty squares horizontally or vertically —
    /// but captures differently: it must have exactly one piece between itself and its target when capturing.
    /// That is the Full board; on the dark-chess HalfCenter board it steps one square instead
    /// (see <see cref="IsValidMoveHalfCenter"/>).
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class Cannon : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cannon"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Cannon(PieceInfo info)
            : base(info) { }
    }
}
