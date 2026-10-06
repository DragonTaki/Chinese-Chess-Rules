/* ----- ----- ----- ----- */
// Advisor.cs
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
    /// Represents the <b>Advisor (仕/士)</b> piece in Chinese Chess.
    /// The Advisor protects the General and can only move diagonally by one step.
    /// It remains within the 3×3 palace area of its own side (unless <see cref="Rules.CanAdvisorLeavePalace"/>,
    /// or it is a revealed 揭棋 piece).
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class Advisor : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Advisor"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Advisor(PieceInfo info)
            : base(info) { }
    }
}
