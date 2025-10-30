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

        protected abstract List<(int x, int y)> GetLegalMovesFull(Board board);
        protected abstract List<(int x, int y)> GetLegalMovesHalfCenter(Board board);
        protected abstract List<(int x, int y)> GetLegalMovesHalfCross(Board board);

        public T PieceFunc<T>(PieceFuncType funcType, BoardType boardType, Board board, int x = -1, int y = -1)
        {
            switch (boardType)
            {
                case BoardType.Full:
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
