/* ----- ----- ----- ----- */
// ThreeKingdomsMoves.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Movements;
using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// How each piece moves in 三國暗棋 (9×5 points, DARK-CHESS-RULES §1.2; the author's decisions
    /// win over the wiki): no palace, river, horse leg or elephant eye. General and Chariot slide
    /// any distance orthogonally; Advisor one step diagonally; Elephant two steps diagonally (may
    /// jump, author 2026-10-02); Horse an L (may jump); Soldier one step orthogonally; Cannon moves
    /// like a Chariot and captures the first piece behind one screen (any distance). Captures do not
    /// follow rank and only take face-up pieces of another faction.
    /// </summary>
    internal static class ThreeKingdomsMoves
    {
        private static readonly (int dx, int dy)[] DiagonalTwoStep = { (2, 2), (2, -2), (-2, 2), (-2, -2) };

        /// <summary>Whether <paramref name="piece"/> may move to (targetX, targetY) by its own movement rules.</summary>
        public static bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if (dx == 0 && dy == 0)
                return false;

            switch (piece.Type)
            {
                case PieceType.General:
                case PieceType.Chariot:
                    return (dx == 0 || dy == 0)
                        && MoveLines.CountPiecesBetween(board, piece.X, piece.Y, targetX, targetY) == 0
                        && CanCapture(board, piece, targetX, targetY);
                case PieceType.Advisor:
                    return Math.Abs(dx) == 1 && Math.Abs(dy) == 1 && CanCapture(board, piece, targetX, targetY);
                case PieceType.Elephant:
                    return Math.Abs(dx) == 2 && Math.Abs(dy) == 2 && CanCapture(board, piece, targetX, targetY);
                case PieceType.Horse:
                    return ((Math.Abs(dx) == 1 && Math.Abs(dy) == 2) || (Math.Abs(dx) == 2 && Math.Abs(dy) == 1))
                        && CanCapture(board, piece, targetX, targetY);
                case PieceType.Soldier:
                    return Math.Abs(dx) + Math.Abs(dy) == 1 && CanCapture(board, piece, targetX, targetY);
                case PieceType.Cannon:
                    if (dx != 0 && dy != 0)
                        return false;
                    int between = MoveLines.CountPiecesBetween(board, piece.X, piece.Y, targetX, targetY);
                    // A quiet move needs a clear line; a capture exactly one screen.
                    if (board.Grid[targetX, targetY] == null)
                        return between == 0;
                    return between == 1 && CanCapture(board, piece, targetX, targetY);
                default:
                    throw new ArgumentException($"No Three Kingdoms movement for {piece.Type}", nameof(piece));
            }
        }

        /// <summary>Every square <paramref name="piece"/> may move to by its own movement rules.</summary>
        public static List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece)
        {
            var moves = new List<(int x, int y)>();
            switch (piece.Type)
            {
                case PieceType.General:
                case PieceType.Chariot:
                    foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
                        Slide(board, piece, dx, dy, moves);
                    break;
                case PieceType.Advisor:
                    Steps(board, piece, MoveDirections.DiagonalOneStep, moves);
                    break;
                case PieceType.Elephant:
                    Steps(board, piece, DiagonalTwoStep, moves);
                    break;
                case PieceType.Horse:
                    Steps(board, piece, MoveDirections.DiagonalLShape, moves);
                    break;
                case PieceType.Soldier:
                    Steps(board, piece, MoveDirections.OrthogonalOneStep, moves);
                    break;
                case PieceType.Cannon:
                    foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
                        CannonLine(board, piece, dx, dy, moves);
                    break;
                default:
                    throw new ArgumentException($"No Three Kingdoms movement for {piece.Type}", nameof(piece));
            }
            return moves;
        }

        /// <summary>
        /// Whether moving onto (targetX, targetY) is allowed as far as capturing goes: an empty point,
        /// or a face-up piece of another faction (any rank; a piece of a faction nobody has claimed yet
        /// is everyone's enemy). Face-down pieces and own pieces are never taken.
        /// </summary>
        public static bool CanCapture(Board board, Piece piece, int targetX, int targetY)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null)
                return true;
            if (target == piece || !target.CurrentInfo.IsFaceUp)
                return false;
            return target.Side != piece.Side;
        }

        private static void Steps(Board board, Piece piece, (int dx, int dy)[] offsets, List<(int x, int y)> moves)
        {
            foreach (var (dx, dy) in offsets)
            {
                int x = piece.X + dx;
                int y = piece.Y + dy;
                if (board.IsInBoard(x, y) && CanCapture(board, piece, x, y))
                    moves.Add((x, y));
            }
        }

        private static void Slide(Board board, Piece piece, int dx, int dy, List<(int x, int y)> moves)
        {
            int x = piece.X + dx;
            int y = piece.Y + dy;
            while (board.IsInBoard(x, y))
            {
                if (board.Grid[x, y] != null)
                {
                    if (CanCapture(board, piece, x, y))
                        moves.Add((x, y));
                    return;
                }
                moves.Add((x, y));
                x += dx;
                y += dy;
            }
        }

        private static void CannonLine(Board board, Piece piece, int dx, int dy, List<(int x, int y)> moves)
        {
            bool jumped = false;
            int x = piece.X + dx;
            int y = piece.Y + dy;
            while (board.IsInBoard(x, y))
            {
                bool occupied = board.Grid[x, y] != null;
                if (!jumped)
                {
                    if (occupied)
                        jumped = true;   // the screen
                    else
                        moves.Add((x, y));
                }
                else if (occupied)
                {
                    // The first piece behind the screen is the only possible target.
                    if (CanCapture(board, piece, x, y))
                        moves.Add((x, y));
                    return;
                }
                x += dx;
                y += dy;
            }
        }
    }
}
