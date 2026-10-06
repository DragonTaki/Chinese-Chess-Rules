/* ----- ----- ----- ----- */
// Piece.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/06
// Version: v3.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces.PieceTypes;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    /// <summary>
    /// Represents an abstract base class for all chess pieces in the Chinese Chess game: identity,
    /// state and history. How a piece moves is not here but in the rules family of the board it
    /// stands on (<see cref="Board.Family"/>: Full-board xiangqi, half-board dark chess, Three Kingdoms).
    /// </summary>
    public abstract class Piece
    {
        /* ----- Basic properties ----- */

        /// <summary>
        /// Gets the specific type of this piece (e.g., General, Soldier, Chariot).
        /// </summary>
        public PieceType Type => CurrentInfo.Type;

        /// <summary>
        /// Gets the player side to which this piece belongs (Player1 or Player2).
        /// <c>PlayerSide.None</c> on the dark-chess board until the first flip decides which
        /// player owns which colour (see <see cref="Board.AssignFactions"/>).
        /// </summary>
        public PlayerSide Side => CurrentInfo.Side;

        /// <summary>
        /// The visible color of this piece (usually Red or Black, but decoupled from ownership).
        /// </summary>
        public PieceColor Color => CurrentInfo.Color;

        /* ----- State properties ----- */

        /// <summary>
        /// The current runtime information of this piece (position, state, etc.).
        /// </summary>
        public PieceInfo CurrentInfo { get; private set; }

        /// <summary>
        /// The historical snapshots of this piece for replay or undo.
        /// </summary>
        public List<PieceInfo> History { get; } = new List<PieceInfo>();

        /// <summary>
        /// Shortcut for the current X position.
        /// </summary>
        public int X => CurrentInfo.X;

        /// <summary>
        /// Shortcut for the current Y position.
        /// </summary>
        public int Y => CurrentInfo.Y;

        /// <summary>
        /// Returns the current position of the piece as a <see cref="GridPoint"/>.
        /// </summary>
        public GridPoint Position => new GridPoint(X, Y);

        /* ----- Construction and state updates ----- */

        /// <summary>
        /// Initializes a new instance of the <see cref="Piece"/> class with specified properties.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side, ...); copied, not kept.</param>
        protected Piece(PieceInfo info)
        {
            CurrentInfo = info.Clone();
            History.Add(info.Clone());
        }

        /// <summary>
        /// Creates the concrete <see cref="Piece"/> subclass matching
        /// <paramref name="info"/>'s type. Single source of truth for the
        /// type↔class mapping — <see cref="Board"/>'s own piece placement
        /// calls this instead of duplicating the switch.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="info"/>'s type has no piece class (<c>None</c>, <c>Shadow</c>).</exception>
        public static Piece Create(PieceInfo info) => info.Type switch
        {
            PieceType.General  => new General(info),
            PieceType.Advisor  => new Advisor(info),
            PieceType.Elephant => new Elephant(info),
            PieceType.Horse    => new Horse(info),
            PieceType.Chariot  => new Chariot(info),
            PieceType.Cannon   => new Cannon(info),
            PieceType.Soldier  => new Soldier(info),
            _ => throw new ArgumentException($"No piece class for type {info.Type}", nameof(info)),
        };

        /// <summary>
        /// Updates the piece's state: only the given fields change, the others keep their values.
        /// <paramref name="turnIndex"/> is required; it records the turn the change happened in
        /// (the new state is also appended to <see cref="History"/>).
        /// </summary>
        /// <param name="turnIndex">Turn index of this change (required)</param>
        /// <param name="x">New X coordinate (null to keep it)</param>
        /// <param name="y">New Y coordinate (null to keep it)</param>
        /// <param name="isFaceUp">Whether the piece is face up (null to keep it)</param>
        /// <param name="isDead">Whether the piece is dead (null to keep it)</param>
        /// <param name="side">
        /// The owning side (null to keep it). Only changes when the dark chess's first flip
        /// decides which player owns which colour (see <see cref="Board.AssignFactions"/>).
        /// </param>
        public void UpdateState(
            int turnIndex,
            int? x = null,
            int? y = null,
            bool? isFaceUp = null,
            bool? isDead = null,
            PlayerSide? side = null
        ) {
            var newInfo = new PieceInfo(
                CurrentInfo.Type,
                x ?? CurrentInfo.X,
                y ?? CurrentInfo.Y,
                CurrentInfo.Color,
                side ?? CurrentInfo.Side,
                isFaceUp ?? CurrentInfo.IsFaceUp,
                isDead ?? CurrentInfo.IsDead,
                turnIndex
            );

            CurrentInfo = newInfo;
            History.Add(newInfo.Clone());
        }

        /// <summary>
        /// Takes back the last <see cref="UpdateState"/>: drops the newest
        /// <see cref="History"/> snapshot and makes the one before it current again. Used by
        /// <see cref="Board.UnmakeMove"/> to undo a move (the mover's move, the captured
        /// piece's death).
        /// </summary>
        /// <exception cref="InvalidOperationException">Only the initial snapshot is left.</exception>
        internal void RevertLastState()
        {
            if (History.Count < 2)
                throw new InvalidOperationException("No state change to revert");

            History.RemoveAt(History.Count - 1);
            CurrentInfo = History[History.Count - 1].Clone();
        }

        /* ----- Game logic ----- */

        /// <summary>
        /// Whether <paramref name="other"/> belongs to the same player as this piece (an own
        /// piece, never a capture target). Normally their <see cref="Side"/>s decide. On the
        /// dark-chess board before the factions are decided (both <c>PlayerSide.None</c> —
        /// 明棋半盤's face-up start, before the first move) the colour decides instead: whoever
        /// moves a piece gets its colour, so a same-coloured piece will be the mover's own and an
        /// other-coloured one the opponent's. Callers must not ask this about a face-down piece's
        /// identity (hidden information).
        /// </summary>
        /// <param name="other">The other piece; null is never the same faction.</param>
        public bool IsSameFaction(Piece other)
        {
            if (other == null)
                return false;
            if (Side == PlayerSide.None && other.Side == PlayerSide.None)
                return Color == other.Color;
            return Side == other.Side;
        }

        /// <summary>
        /// Whether moving to (targetX, targetY) is legal: the piece's own movement rules
        /// (<see cref="IsPseudoLegalMove"/>) and, on boards that use check rules (see
        /// <see cref="Board.UsesCheckRules"/>), the move must not leave the mover's own
        /// General attacked or the two Generals facing each other.
        /// </summary>
        public bool IsValidMove(Board board, int targetX, int targetY)
        {
            if (!IsPseudoLegalMove(board, targetX, targetY))
                return false;

            return !board.UsesCheckRules || !board.WouldMoveExposeOwnGeneral(this, targetX, targetY);
        }

        public bool CanMoveTo(Board board, int targetX, int targetY) =>
            IsValidMove(board, targetX, targetY);

        /// <summary>
        /// All legal destinations: <see cref="GetPseudoLegalMoves"/> filtered the same way
        /// as <see cref="IsValidMove"/>, so the two always agree.
        /// </summary>
        public List<(int x, int y)> GetLegalMoves(Board board)
        {
            var moves = GetPseudoLegalMoves(board);
            if (!board.UsesCheckRules)
                return moves;

            moves.RemoveAll(m => board.WouldMoveExposeOwnGeneral(this, m.x, m.y));
            return moves;
        }

        /// <summary>
        /// The piece's own movement/capture rules only, ignoring whether the move leaves
        /// its own General in check. This is also "does this piece attack that square"
        /// for check detection (see <see cref="Board.IsSideInCheck"/>). How each piece moves
        /// depends on the board's rules family (<see cref="Board.Family"/>).
        /// </summary>
        public bool IsPseudoLegalMove(Board board, int targetX, int targetY) =>
            board.Family.IsPseudoLegalMove(board, this, targetX, targetY);

        /// <summary>See <see cref="IsPseudoLegalMove"/>.</summary>
        public List<(int x, int y)> GetPseudoLegalMoves(Board board) =>
            board.Family.GetPseudoLegalMoves(board, this);

        /// <summary>
        /// Whether moving onto the piece at (targetX, targetY) is a 自殺 (<see cref="Rules.CanSuicide"/>:
        /// the mover dies and the target stays; only the dark-chess family has it). Does not check
        /// the move itself; ask <see cref="IsPseudoLegalMove"/> for that.
        /// </summary>
        public bool IsSuicideMove(Board board, int targetX, int targetY) =>
            board.Family.IsSuicideMove(board, this, targetX, targetY);

        /// <summary>
        /// Replaces <see cref="CurrentInfo"/> without recording a history snapshot. Only for
        /// <see cref="Board"/>'s temporary move simulation, which restores the original
        /// info afterwards.
        /// </summary>
        internal void SetInfoWithoutHistory(PieceInfo info)
        {
            CurrentInfo = info;
        }
    }
}
