/* ----- ----- ----- ----- */
// PieceInfo.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/14
// Update Date: 2026/10/02
// Version: v1.3
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

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
    /// Colour helpers of the two-colour games (the Full board, dark chess), where a player's
    /// colour is a per-game attribute (<c>GameManager.ColorOf</c>): Player1 always moves first
    /// and plays whichever colour moves first in the start position.
    /// </summary>
    public static class PieceColors
    {
        /// <summary>The other colour of a two-colour game: Red for Black and Black for Red; None for any other colour.</summary>
        public static PieceColor Opposite(PieceColor color) => color switch
        {
            PieceColor.Red => PieceColor.Black,
            PieceColor.Black => PieceColor.Red,
            _ => PieceColor.None,
        };

        /// <summary>
        /// Copies of <paramref name="pieces"/> owned by colour, for a Full-board position:
        /// pieces of <paramref name="firstColor"/> (the colour that moves first) belong to
        /// <see cref="PlayerSide.Player1"/>, the other colour's to <see cref="PlayerSide.Player2"/>.
        /// Every other field is kept.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="firstColor"/> is not Red/Black,
        /// or a piece is neither Red nor Black.</exception>
        public static List<PieceInfo> AssignOwners(IEnumerable<PieceInfo> pieces, PieceColor firstColor)
        {
            ArgumentNullException.ThrowIfNull(pieces);
            if (firstColor != PieceColor.Red && firstColor != PieceColor.Black)
                throw new ArgumentException($"The first colour must be Red or Black, not {firstColor}", nameof(firstColor));

            var owned = new List<PieceInfo>();
            foreach (var p in pieces)
            {
                if (p.Color != PieceColor.Red && p.Color != PieceColor.Black)
                    throw new ArgumentException($"{p.Type} at ({p.X},{p.Y}) is {p.Color}; the Full board only has Red and Black", nameof(pieces));
                owned.Add(p.WithSide(p.Color == firstColor ? PlayerSide.Player1 : PlayerSide.Player2));
            }
            return owned;
        }
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

        /// <summary>Visual color of the piece (Red / Black, or Yellow / None for the remaining variants)</summary>
        public PieceColor Color { get; }

        /// <summary>
        /// Owning player's side or faction (e.g., Player1, Player2, Player3, Neutral). None for a
        /// dark-chess piece whose owner is not decided yet (before the first flip, see
        /// <c>Board.AssignFactions</c>).
        /// </summary>
        public PlayerSide Side { get; }

        /// <summary>Whether the piece is currently face-up (for variants like blind chess)</summary>
        public bool IsFaceUp { get; set; }

        /// <summary>Whether the piece is captured or removed from the board</summary>
        public bool IsDead { get; set; }

        /// <summary>Board turn counter (<c>Board.Turn</c>) when this piece was last updated (used in step replay or undo)</summary>
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

        /// <summary>A copy of the current piece state owned by <paramref name="side"/>.</summary>
        public PieceInfo WithSide(PlayerSide side)
        {
            return new PieceInfo(Type, X, Y, Color, side, IsFaceUp, IsDead, TurnIndex);
        }
    }
}
