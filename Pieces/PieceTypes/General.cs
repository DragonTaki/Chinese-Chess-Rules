/* ----- ----- ----- ----- */
// General.cs
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
    /// Represents the <b>General (帥/將)</b> piece in Chinese Chess.
    /// The General moves exactly 1 square horizontally or vertically and must stay within the palace (九宮格)
    /// unless <see cref="Rules.CanGeneralLeavePalace"/> is on.
    /// It cannot move diagonally and cannot capture own pieces (unless <see cref="Rules.CanCaptureOwnPiece"/>).
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class General : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="General"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public General(PieceInfo info)
            : base(info) { }
    }
}
