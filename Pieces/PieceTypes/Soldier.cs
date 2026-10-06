/* ----- ----- ----- ----- */
// Soldier.cs
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
    /// Represents the <b>Soldier (兵/卒)</b> piece in Chinese Chess.
    /// <para>
    /// Soldiers move 1 step forward before crossing the river and can move horizontally
    /// (left or right) after crossing the river. They cannot move backward.
    /// </para>
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class Soldier : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Soldier"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Soldier(PieceInfo info)
            : base(info) { }
    }
}
