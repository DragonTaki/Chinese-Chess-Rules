/* ----- ----- ----- ----- */
// Elephant.cs
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
    /// Represents the <b>Elephant (相/象)</b> piece in Chinese Chess.
    /// The Elephant moves exactly 2 squares diagonally and cannot cross the river (except a revealed 揭棋 piece).
    /// Its move can be blocked if the "elephant's eye" (the midpoint of its path) is occupied.
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class Elephant : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Elephant"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Elephant(PieceInfo info)
            : base(info) { }
    }
}
