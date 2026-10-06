/* ----- ----- ----- ----- */
// Chariot.cs
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
    /// Represents the <b>Chariot (俥/車)</b> piece in Chinese Chess.
    /// The Chariot moves any number of squares horizontally or vertically, like the Rook in Western chess.
    /// It cannot jump over other pieces and cannot capture own pieces (unless <see cref="Rules.CanCaptureOwnPiece"/>).
    /// How it moves depends on the board's rules family (<c>Families/</c>); this class is its identity only.
    /// </summary>
    public class Chariot : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Chariot"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Chariot(PieceInfo info)
            : base(info) { }
    }
}
