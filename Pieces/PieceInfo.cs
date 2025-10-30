/* ----- ----- ----- ----- */
// PieceInfo.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/14
// Update Date: 2025/10/29
// Version: v1.1
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    /// <summary>
    /// Defines the visible color of a piece.  
    /// This can differ from its owning player's side in multi-faction variants (e.g., Three Kingdoms Chess).
    /// </summary>
    public enum PieceColor
    {
        None,
        Red,
        Black,
        Yellow,
    }

    /// <summary>
    /// Stores runtime state of a single chess piece.  
    /// Used for both in-game logic and state snapshots (e.g., replay, undo).
    /// </summary>
    public class PieceInfo
    {
        /// <summary>Type of the piece (e.g., 車, 馬, 相, 士, 帥, etc.)</summary>
        public PieceType Type { get; }

        /// <summary>Current X position on the board (column index)</summary>
        public int X { get; set; }

        /// <summary>Current Y position on the board (row index)</summary>
        public int Y { get; set; }

        /// <summary>Visual color of the piece (Red / Black)</summary>
        public PieceColor Color { get; }

        /// <summary>Owning player's side or faction (e.g., Red, Black, Neutral)</summary>
        public PlayerSide Side { get; }

        /// <summary>Whether the piece is currently face-up (for variants like blind chess)</summary>
        public bool IsFaceUp { get; set; }

        /// <summary>Whether the piece is captured or removed from the board</summary>
        public bool IsDead { get; set; }

        /// <summary>Round number when this piece was last updated (used in step replay or undo)</summary>
        public int TurnIndex { get; set; }

        public PieceInfo(
            PieceType type,
            int x,
            int y,
            PieceColor color,
            PlayerSide side,
            bool isFaceUp = true,
            bool isDead = false,
            int turnIndex = 0)
        {
            Type = type;
            X = x;
            Y = y;
            Color = color;
            Side = side;
            IsFaceUp = isFaceUp;
            IsDead = isDead;
            TurnIndex = turnIndex;
        }

        /// <summary>
        /// Creates a shallow copy of the current piece state for snapshot or replay purposes.
        /// </summary>
        public PieceInfo Clone()
        {
            return new PieceInfo(Type, X, Y, Color, Side, IsFaceUp, IsDead, TurnIndex);
        }
    }
}
