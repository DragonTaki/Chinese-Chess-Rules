/* ----- ----- ----- ----- */
// Piece.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/01
// Version: v2.3
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Drawing;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Movements;
using Chinese_Chess_v3.Game.Core.Pieces.PieceTypes;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    /// <summary>
    /// Represents an abstract base class for all chess pieces in the Chinese Chess game.
    /// Each derived piece defines its own movement rules.
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
        /// Returns the current position of the piece as a Point.
        /// </summary>
        public Point Position => new Point(X, Y);

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
        /// calls this instead of duplicating the switch, and so does 揭棋's
        /// hidden-piece movement delegation (see
        /// <see cref="IsValidMoveAsOriginalPosition"/>), which needs a
        /// throwaway instance of a *different* type at the same position.
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
        /// Full board: whether the piece on (targetX, targetY) is one this piece may not move
        /// onto because it is its own (吃己棋). An own piece always blocks unless
        /// <see cref="Rules.CanCaptureOwnPiece"/> is on, and the own General always blocks (the
        /// Full board's check rules need both Generals; author decision 2026-10-02).
        /// </summary>
        protected bool IsBlockedByOwnPieceFull(Board board, int targetX, int targetY)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null || target.Side != Side)
                return false;
            // Its own square is never a destination.
            if (target == this)
                return true;
            return !board.GameRules.CanCaptureOwnPiece || target.Type == PieceType.General;
        }

        // Only check destination location
        protected virtual bool IsDestinationLegalFull(Board board, int targetX, int targetY) => board.IsInBoard(targetX, targetY);
        protected virtual bool IsDestinationLegalHalfCenter(Board board, int targetX, int targetY) => board.IsInBoard(targetX, targetY);
        protected virtual bool IsDestinationLegalHalfCross(Board board, int targetX, int targetY) => board.IsInBoard(targetX, targetY);

        // Check chessboard circumstance if destination legal
        protected virtual bool IsValidMoveFull(Board board, int targetX, int targetY) => true;
        protected virtual bool IsValidMoveHalfCenter(Board board, int targetX, int targetY) => true;
        protected virtual bool IsValidMoveHalfCross(Board board, int targetX, int targetY) => true;

        /// <summary>
        /// Full-board flying-General check (王見王) for a non-General piece moving from its
        /// current square to (targetX, targetY): true if the move would leave the Generals
        /// facing each other and <see cref="Rules.CanGeneralSeeGeneral"/> is off.
        /// </summary>
        protected bool WouldExposeGeneralsFull(Board board, int targetX, int targetY) =>
            !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(X, Y, targetX, targetY);

        /// <summary>
        /// HalfCenter (8×4, 明棋／暗棋半盤) capture-eligibility check for a
        /// target square already confirmed on-board and reachable by the
        /// piece's own movement shape — centralized here since every piece
        /// type on this board moves differently but is captured-from the
        /// same way, governed by <see cref="Rules.PieceRankings"/> and the
        /// hidden-piece flags rather than by piece type — except the one fixed
        /// Soldier/General pair (a Soldier can capture a General, a General cannot
        /// capture a Soldier). Cannon screens are the Cannon's own concern.
        /// </summary>
        /// <remarks>
        /// This only answers whether *attempting* the move is legal. If the
        /// target is hidden and <see cref="Rules.CanCaptureHiddenPiece"/> is
        /// enabled, whether the attacker also dies alongside the target once
        /// its rank is revealed (<see cref="Rules.IsCaptureHiddenPieceStrongerSuicide"/>)
        /// is a state change <c>GameManager</c> applies when the move is made (it reveals
        /// the target and re-checks this with the target face up), not a legality question.
        /// The same goes for a hidden target that turns out to be one's own piece.
        /// With <see cref="Rules.CanSuicide"/> on, a ranked capture of a stronger face-up
        /// enemy piece is a legal move too: the mover dies (<see cref="IsSuicideMove"/>).
        /// With <see cref="Rules.CanCaptureOwnPiece"/> on, a face-up own piece is a target
        /// like an enemy one, rank included (author decision 2026-10-02), but never a suicide.
        /// </remarks>
        /// <param name="board">The board the move is made on.</param>
        /// <param name="targetX">Target square X.</param>
        /// <param name="targetY">Target square Y.</param>
        /// <param name="ignoreRank">
        /// true for the Cannon's jump capture (炮隔子打), which takes an enemy piece of any
        /// rank, General included: the rank order and the Soldier/General pair are skipped,
        /// everything else (hidden pieces, own pieces) still applies.
        /// </param>
        /// <returns>Whether moving onto the target square is allowed as far as capturing goes.</returns>
        protected bool CanCaptureInDarkChess(Board board, int targetX, int targetY, bool ignoreRank = false)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null)
                return true;
            if (target == this)
                return false;

            var rules = board.GameRules;

            // A face-down target's identity (side and rank) is hidden information: it must
            // never decide legality, or the legal-move hints would reveal which face-down
            // pieces are one's own. With hidden capture (暗吃) on, every face-down target is
            // a legal attempt — what happens once it is revealed is the caller's concern
            // (see remarks); with it off, no face-down piece is ever a target.
            if (!target.CurrentInfo.IsFaceUp)
                return rules.CanCaptureHiddenPiece;

            // An own piece is a target only with 吃己棋 on (then by the same rules as an enemy
            // piece, rank included). Before the factions are decided (明棋半盤's first move)
            // the colours tell them apart.
            bool own = IsSameFaction(target);
            if (own && !rules.CanCaptureOwnPiece)
                return false;

            if (ignoreRank || OutranksForCapture(target, rules))
                return true;

            // Too weak for a ranked capture: with 自殺 on, moving onto a stronger enemy piece is
            // still a legal move, in which the mover dies (IsSuicideMove). Never onto an own piece.
            return rules.CanSuicide && !own;
        }

        /// <summary>
        /// The dark-chess rank order for a ranked capture (吃子看大小): whether this piece is
        /// strong enough to capture <paramref name="target"/>. A Soldier can capture a General
        /// and a General cannot capture a Soldier (the standard exception); every other pair
        /// follows <see cref="Rules.PieceRankings"/> (same rank or stronger).
        /// </summary>
        private bool OutranksForCapture(Piece target, Rules rules)
        {
            if (Type == PieceType.Soldier && target.Type == PieceType.General)
                return true;
            if (Type == PieceType.General && target.Type == PieceType.Soldier)
                return false;

            int myRank = Array.IndexOf(rules.PieceRankings, Type);
            int targetRank = Array.IndexOf(rules.PieceRankings, target.Type);

            // PieceRankings is ordered strongest-first, so a lower index
            // means a stronger piece; capturing requires being the same
            // rank or stronger.
            return myRank <= targetRank;
        }

        /// <summary>
        /// HalfCenter 自殺 (<see cref="Rules.CanSuicide"/>): whether moving this piece onto the
        /// face-up piece at (<paramref name="targetX"/>, <paramref name="targetY"/>) is a suicide
        /// — the target is an enemy piece this piece may not capture by rank, so this piece dies
        /// and the target stays. Only a one-square orthogonal capture follows rank (author
        /// decision 2026-10-02: 車衝 over more than one square, the Cannon's jump and the 馬斜
        /// diagonal ignore rank), so only such a move can be a suicide. Does not check the move
        /// itself; ask <see cref="IsPseudoLegalMove"/> for that.
        /// </summary>
        public bool IsSuicideMove(Board board, int targetX, int targetY)
        {
            var target = board.GetPiece(targetX, targetY);
            if (board.Type != BoardType.HalfCenter || target == null || target == this || !target.CurrentInfo.IsFaceUp
                || IsSameFaction(target))
                return false;
            if (Math.Abs(targetX - X) + Math.Abs(targetY - Y) != 1)
                return false;
            return !OutranksForCapture(target, board.GameRules);
        }

        /// <summary>
        /// Movement check shared by HalfCenter and HalfCross — both are the
        /// same dark-chess mechanic (see <see cref="CanCaptureInDarkChess"/>),
        /// just on differently-shaped boards, and neither has a palace,
        /// river, or any other board-shape restriction beyond "on the
        /// board" — so this checks bounds directly via
        /// <see cref="Board.IsInBoard"/> rather than going through either
        /// board type's (identical, unoverridden) IsDestinationLegal*.
        /// Every piece type moves exactly one square orthogonally this way
        /// (General, Advisor, Elephant, Soldier always; Chariot/Horse only
        /// when their respective "special movement" rule flag is off). The
        /// Cannon makes its non-capturing moves this way too, and also its
        /// captures when <see cref="Rules.IsCannonMustJumpToCapture"/> is off;
        /// with it on, its captures are jumps (see <c>Cannon</c>).
        /// </summary>
        protected bool IsValidOrthogonalOneStepDarkChess(Board board, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            bool matched = false;
            foreach (var (dirX, dirY) in MoveDirections.OrthogonalOneStep)
            {
                if (dx == dirX && dy == dirY)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;

            return CanCaptureInDarkChess(board, targetX, targetY);
        }

        /// <summary>See <see cref="IsValidOrthogonalOneStepDarkChess"/>.</summary>
        protected List<(int x, int y)> GetOrthogonalOneStepMovesDarkChess(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
            {
                int newX = X + dx;
                int newY = Y + dy;

                if (!board.IsInBoard(newX, newY))
                    continue;

                if (!CanCaptureInDarkChess(board, newX, newY))
                    continue;

                legalMoves.Add((newX, newY));
            }

            return legalMoves;
        }

        /// <summary>
        /// 揭棋 (Jieqi/FlipChess — see <see cref="Board.IsJieqi"/>): a piece
        /// that hasn't moved yet is still face-down, and its first move
        /// must follow the movement rules of whichever piece type
        /// canonically starts at this square in the classic layout — not
        /// its own true identity, which stays secret until it moves. Since
        /// each of the seven <c>PieceTypes</c> classes bakes its movement
        /// logic into instance methods rather than static/stateless
        /// functions, the least invasive way to "borrow" another type's
        /// logic without duplicating or refactoring all seven is to
        /// construct a throwaway instance of that type at the same
        /// position/side and delegate to its own (already correct,
        /// unmodified) <c>IsValidMoveFull</c>.
        /// </summary>
        protected bool IsValidMoveAsOriginalPosition(Board board, int targetX, int targetY)
        {
            var originalType = PieceConstants.GetClassicPieceTypeAt(X, Y);
            if (originalType == Type)
                return IsValidMoveFull(board, targetX, targetY);

            var standIn = Create(new PieceInfo(originalType, X, Y, Color, Side, CurrentInfo.IsFaceUp, CurrentInfo.IsDead, CurrentInfo.TurnIndex));
            return standIn.IsValidMoveFull(board, targetX, targetY);
        }

        /// <summary>See <see cref="IsValidMoveAsOriginalPosition"/>.</summary>
        protected List<(int x, int y)> GetLegalMovesAsOriginalPosition(Board board)
        {
            var originalType = PieceConstants.GetClassicPieceTypeAt(X, Y);
            if (originalType == Type)
                return GetLegalMovesFull(board);

            var standIn = Create(new PieceInfo(originalType, X, Y, Color, Side, CurrentInfo.IsFaceUp, CurrentInfo.IsDead, CurrentInfo.TurnIndex));
            return standIn.GetLegalMovesFull(board);
        }

        protected abstract List<(int x, int y)> GetLegalMovesFull(Board board);
        protected abstract List<(int x, int y)> GetLegalMovesHalfCenter(Board board);
        protected abstract List<(int x, int y)> GetLegalMovesHalfCross(Board board);

        public T PieceFunc<T>(PieceFuncType funcType, BoardType boardType, Board board, int x = -1, int y = -1)
        {
            switch (boardType)
            {
                case BoardType.Full:
                    // 揭棋 (Jieqi/FlipChess): a still-hidden piece moves as
                    // whatever canonically starts at its square, not as
                    // itself — see IsValidMoveAsOriginalPosition. Once
                    // revealed (IsFaceUp), it's back to moving as itself,
                    // same as a normal Full-board game.
                    if (board.IsJieqi && !CurrentInfo.IsFaceUp)
                    {
                        return funcType switch
                        {
                            PieceFuncType.IsDestinationLegal => (T)(object)IsDestinationLegalFull(board, x, y),
                            PieceFuncType.IsValidMove => (T)(object)IsValidMoveAsOriginalPosition(board, x, y),
                            PieceFuncType.GetLegalMoves => (T)(object)GetLegalMovesAsOriginalPosition(board),
                            _ => throw new NotImplementedException()
                        };
                    }
                    return funcType switch
                    {
                        PieceFuncType.IsDestinationLegal => (T)(object)IsDestinationLegalFull(board, x, y),
                        PieceFuncType.IsValidMove => (T)(object)IsValidMoveFull(board, x, y),
                        PieceFuncType.GetLegalMoves => (T)(object)GetLegalMovesFull(board),
                        _ => throw new NotImplementedException()
                    };
                case BoardType.HalfCenter:
                    return funcType switch
                    {
                        PieceFuncType.IsDestinationLegal => (T)(object)IsDestinationLegalHalfCenter(board, x, y),
                        PieceFuncType.IsValidMove => (T)(object)IsValidMoveHalfCenter(board, x, y),
                        PieceFuncType.GetLegalMoves => (T)(object)GetLegalMovesHalfCenter(board),
                        _ => throw new NotImplementedException()
                    };
                case BoardType.HalfCross:
                    return funcType switch
                    {
                        PieceFuncType.IsDestinationLegal => (T)(object)IsDestinationLegalHalfCross(board, x, y),
                        PieceFuncType.IsValidMove => (T)(object)IsValidMoveHalfCross(board, x, y),
                        PieceFuncType.GetLegalMoves => (T)(object)GetLegalMovesHalfCross(board),
                        _ => throw new NotImplementedException()
                    };
                default:
                    throw new NotImplementedException();
            }
        }

        // Unified entry points on the base class (dispatch by board type through PieceFunc)

        public bool IsDestinationLegal(Board board, int targetX, int targetY) =>
            PieceFunc<bool>(PieceFuncType.IsDestinationLegal, board.Type, board, targetX, targetY);

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
        /// for check detection (see <see cref="Board.IsSideInCheck"/>).
        /// </summary>
        public bool IsPseudoLegalMove(Board board, int targetX, int targetY) =>
            PieceFunc<bool>(PieceFuncType.IsValidMove, board.Type, board, targetX, targetY);

        /// <summary>See <see cref="IsPseudoLegalMove"/>.</summary>
        public List<(int x, int y)> GetPseudoLegalMoves(Board board) =>
            PieceFunc<List<(int x, int y)>>(PieceFuncType.GetLegalMoves, board.Type, board);

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

    public enum PieceFuncType
    {
        IsDestinationLegal,
        IsValidMove,
        GetLegalMoves,
    }
}
