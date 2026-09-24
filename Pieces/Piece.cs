/* ----- ----- ----- ----- */
// Piece.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/30
// Version: v2.1
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
        /* ----- 基本屬性 ----- */

        /// <summary>
        /// Gets the specific type of this piece (e.g., General, Soldier, Chariot).
        /// </summary>
        public PieceType Type => CurrentInfo.Type;

        /// <summary>
        /// Gets the player side to which this piece belongs (Red or Black).
        /// </summary>
        public PlayerSide Side => CurrentInfo.Side;

        /// <summary>
        /// The visible color of this piece (usually Red or Black, but decoupled from ownership).
        /// </summary>
        public PieceColor Color => CurrentInfo.Color;

        /* ----- 狀態屬性 ----- */

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

        /* ----- 建構與狀態更新 ----- */

        /// <summary>
        /// Initializes a new instance of the <see cref="Piece"/> class with specified properties.
        /// </summary>
        /// <param name="type">The type of this piece.</param>
        /// <param name="x">The initial X-coordinate position.</param>
        /// <param name="y">The initial Y-coordinate position.</param>
        /// <param name="side">The side (Red or Black) this piece belongs to.</param>
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
        public static Piece Create(PieceInfo info) => info.Type switch
        {
            PieceType.General  => new General(info),
            PieceType.Advisor  => new Advisor(info),
            PieceType.Elephant => new Elephant(info),
            PieceType.Horse    => new Horse(info),
            PieceType.Chariot  => new Chariot(info),
            PieceType.Cannon   => new Cannon(info),
            PieceType.Soldier  => new Soldier(info),
            _ => throw new Exception("Unknown piece type"),
        };

        /// <summary>
        /// 更新棋子狀態，只修改指定欄位，其餘保持原值。
        /// turnIndex 必須提供，用以紀錄該回合的行為。
        /// </summary>
        /// <param name="turnIndex">回合索引，必須提供</param>
        /// <param name="x">新的 X 座標（不修改時傳 null）</param>
        /// <param name="y">新的 Y 座標（不修改時傳 null）</param>
        /// <param name="faceUp">是否翻開（不修改時傳 null）</param>
        /// <param name="isDead">是否死亡（不修改時傳 null）</param>
        public void UpdateState(
            int turnIndex,
            int? x = null,
            int? y = null,
            bool? isFaceUp = null,
            bool? isDead = null
        ) {
            var newInfo = new PieceInfo(
                CurrentInfo.Type,
                x ?? CurrentInfo.X,
                y ?? CurrentInfo.Y,
                CurrentInfo.Color,
                CurrentInfo.Side,
                isFaceUp ?? CurrentInfo.IsFaceUp,
                isDead ?? CurrentInfo.IsDead,
                turnIndex
            );

            CurrentInfo = newInfo;
            History.Add(newInfo.Clone());
        }

        /* ----- 遊戲邏輯 ----- */

        // Only check destination location
        protected virtual bool IsDestinationLegalFull(Board board, int targetX, int targetY) => board.IsInBoard(targetX, targetY);
        protected virtual bool IsDestinationLegalHalfCenter(Board board, int targetX, int targetY) => board.IsInBoard(targetX, targetY);
        protected virtual bool IsDestinationLegalHalfCross(Board board, int targetX, int targetY) => board.IsInBoard(targetX, targetY);

        // Check chessboard circumstance if destination legal
        protected virtual bool IsValidMoveFull(Board board, int x, int targetY) => true;
        protected virtual bool IsValidMoveHalfCenter(Board board, int x, int targetY) => true;
        protected virtual bool IsValidMoveHalfCross(Board board, int x, int targetY) => true;

        /// <summary>
        /// HalfCenter (8×4, 明棋／暗棋半盤) capture-eligibility check for a
        /// target square already confirmed on-board and reachable by the
        /// piece's own movement shape — centralized here since every piece
        /// type on this board moves differently but is captured-from the
        /// same way, governed by <see cref="Rules.PieceRankings"/> and the
        /// hidden-piece flags rather than by piece type.
        /// </summary>
        /// <remarks>
        /// This only answers whether *attempting* the move is legal. If the
        /// target is hidden and <see cref="Rules.CanCaptureHiddenPiece"/> is
        /// enabled, whether the attacker also dies alongside the target once
        /// its rank is revealed (<see cref="Rules.IsCaptureHiddenPieceStrongerSuiside"/>)
        /// is a state change the caller (not yet implemented — see
        /// docs/STATUS.md) applies after the move, not a legality question.
        /// </remarks>
        protected bool CanCaptureInDarkChess(Board board, int targetX, int targetY)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null)
                return true;

            // Capturing an allied piece is never allowed on this board type.
            // CanCaptureOwnPiece is declared under Rules.cs's "Full Board
            // Rules" region, so it's scoped to the Full board only.
            if (target.Side == Side)
                return false;

            var rules = board.GameRules;

            if (!target.CurrentInfo.IsFaceUp)
                return rules.CanCaptureHiddenPiece;

            int myRank = Array.IndexOf(rules.PieceRankings, Type);
            int targetRank = Array.IndexOf(rules.PieceRankings, target.Type);

            // PieceRankings is ordered strongest-first, so a lower index
            // means a stronger piece; capturing requires being the same
            // rank or stronger.
            return myRank <= targetRank;
        }

        /// <summary>
        /// Movement check shared by HalfCenter and HalfCross — both are the
        /// same dark-chess mechanic (see <see cref="CanCaptureInDarkChess"/>),
        /// just on differently-shaped boards, and neither has a palace,
        /// river, or any other board-shape restriction beyond "on the
        /// board" — so this checks bounds directly via
        /// <see cref="Board.IsInBoard"/> rather than going through either
        /// board type's (identical, unoverridden) IsDestinationLegal*.
        /// Five of the seven piece types move exactly one square
        /// orthogonally by default this way (General, Advisor, Elephant,
        /// Soldier always; Chariot/Horse only when their respective
        /// "special movement" rule flag is off).
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
        /// 揭棋 (Jieqi/FlipChess — see <see cref="Rules.IsJieqi"/>): a piece
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
                    if (board.GameRules.IsJieqi && !CurrentInfo.IsFaceUp)
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

        // 基底統一呼叫

        public bool IsDestinationLegal(Board board, int targetX, int targetY) =>
            PieceFunc<bool>(PieceFuncType.IsDestinationLegal, board.Type, board, targetX, targetY);

        public bool IsValidMove(Board board, int targetX, int targetY) =>
            PieceFunc<bool>(PieceFuncType.IsValidMove, board.Type, board, targetX, targetY);

        public bool CanMoveTo(Board board, int targetX, int targetY) =>
            IsValidMove(board, targetX, targetY);

        public List<(int x, int y)> GetLegalMoves(Board board) =>
            PieceFunc<List<(int x, int y)>>(PieceFuncType.GetLegalMoves, board.Type, board);
    }

    public enum PieceFuncType
    {
        IsDestinationLegal,
        IsValidMove,
        GetLegalMoves,
    }
}
