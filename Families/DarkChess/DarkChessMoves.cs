/* ----- ----- ----- ----- */
// DarkChessMoves.cs
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

namespace Chinese_Chess_v3.Game.Core.Families.DarkChess
{
    /// <summary>
    /// How each piece moves on the half board (台灣暗棋 8×4: 暗棋半盤 and 明棋半盤). Every piece
    /// steps one square orthogonally and captures by rank (<see cref="Rules.PieceRankings"/>, a
    /// Soldier may take a General, a General may not take a Soldier), except: the Cannon jumps
    /// one screen to capture regardless of rank (<see cref="Rules.IsCannonMustJumpToCapture"/>);
    /// with 車衝馬斜 (<see cref="Rules.IsChariotRushHorseDiagonal"/>) the Chariot slides any
    /// distance (a capture over more than one square ignores rank) and the Horse steps one square
    /// diagonally (its captures ignore rank). A face-down target's identity never decides
    /// legality (hidden information).
    /// </summary>
    internal static class DarkChessMoves
    {
        /// <summary>Whether <paramref name="piece"/> may move to (targetX, targetY) by its own movement rules.</summary>
        public static bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY) => piece.Type switch
        {
            PieceType.Chariot when board.GameRules.IsChariotRushHorseDiagonal => IsValidRushingChariot(board, piece, targetX, targetY),
            PieceType.Horse when board.GameRules.IsChariotRushHorseDiagonal => IsValidDiagonalHorse(board, piece, targetX, targetY),
            PieceType.Cannon when board.GameRules.IsCannonMustJumpToCapture => IsValidJumpingCannon(board, piece, targetX, targetY),
            PieceType.General or PieceType.Advisor or PieceType.Elephant or PieceType.Horse or PieceType.Chariot
                or PieceType.Cannon or PieceType.Soldier => IsValidOneStep(board, piece, targetX, targetY),
            _ => throw new ArgumentException($"No half-board movement for {piece.Type}", nameof(piece)),
        };

        /// <summary>Every square <paramref name="piece"/> may move to by its own movement rules.</summary>
        public static List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece) => piece.Type switch
        {
            PieceType.Chariot when board.GameRules.IsChariotRushHorseDiagonal => GetRushingChariotMoves(board, piece),
            PieceType.Horse when board.GameRules.IsChariotRushHorseDiagonal => GetDiagonalHorseMoves(board, piece),
            PieceType.Cannon when board.GameRules.IsCannonMustJumpToCapture => GetJumpingCannonMoves(board, piece),
            PieceType.General or PieceType.Advisor or PieceType.Elephant or PieceType.Horse or PieceType.Chariot
                or PieceType.Cannon or PieceType.Soldier => GetOneStepMoves(board, piece),
            _ => throw new ArgumentException($"No half-board movement for {piece.Type}", nameof(piece)),
        };

        #region Capturing

        /// <summary>
        /// Whether moving <paramref name="piece"/> onto (targetX, targetY) - a square already known
        /// to be on the board and reachable by its movement - is allowed as far as capturing goes.
        /// A face-down target is a legal attempt only with 暗吃 (<see cref="Rules.CanCaptureHiddenPiece"/>;
        /// what happens once it is revealed is the action's business). An own face-up piece is a
        /// target only with 吃己棋 (<see cref="Rules.CanCaptureOwnPiece"/>, rank included; before the
        /// factions are decided the colours tell own pieces apart). A ranked capture of a stronger
        /// enemy piece is legal only with 自殺 (<see cref="Rules.CanSuicide"/>: the mover dies).
        /// </summary>
        /// <param name="ignoreRank">true for captures that ignore rank (the Cannon's jump, a 車衝 over more than one square, the 馬斜 diagonal).</param>
        public static bool CanCapture(Board board, Piece piece, int targetX, int targetY, bool ignoreRank = false)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null)
                return true;
            if (target == piece)
                return false;

            var rules = board.GameRules;

            // A face-down target's identity (side and rank) is hidden information: it must never
            // decide legality, or the legal-move hints would reveal which face-down pieces are one's own.
            if (!target.CurrentInfo.IsFaceUp)
                return rules.CanCaptureHiddenPiece;

            bool own = piece.IsSameFaction(target);
            if (own && !rules.CanCaptureOwnPiece)
                return false;

            if (ignoreRank || Outranks(piece, target, rules))
                return true;

            // Too weak for a ranked capture: with 自殺 on, moving onto a stronger enemy piece is
            // still a legal move, in which the mover dies. Never onto an own piece.
            return rules.CanSuicide && !own;
        }

        /// <summary>
        /// The rank order of a ranked capture (吃子看大小): whether <paramref name="piece"/> may take
        /// <paramref name="target"/>. A Soldier can capture a General and a General cannot capture a
        /// Soldier; every other pair follows <see cref="Rules.PieceRankings"/> (same rank or stronger).
        /// </summary>
        public static bool Outranks(Piece piece, Piece target, Rules rules)
        {
            if (piece.Type == PieceType.Soldier && target.Type == PieceType.General)
                return true;
            if (piece.Type == PieceType.General && target.Type == PieceType.Soldier)
                return false;
            // Ordered strongest first: a lower index is stronger.
            return Array.IndexOf(rules.PieceRankings, piece.Type) <= Array.IndexOf(rules.PieceRankings, target.Type);
        }

        /// <summary>
        /// 自殺 (<see cref="Rules.CanSuicide"/>): whether moving <paramref name="piece"/> onto the face-up
        /// piece at (targetX, targetY) is a suicide - an enemy piece it may not capture by rank, so the
        /// mover dies and the target stays. Only a one-square orthogonal capture follows rank (author
        /// decision 2026-10-02: 車衝 over more than one square, the Cannon's jump and the 馬斜 diagonal
        /// ignore rank), so only such a move can be one. Does not check the move itself.
        /// </summary>
        public static bool IsSuicideMove(Board board, Piece piece, int targetX, int targetY)
        {
            var target = board.GetPiece(targetX, targetY);
            if (target == null || target == piece || !target.CurrentInfo.IsFaceUp || piece.IsSameFaction(target))
                return false;
            if (Math.Abs(targetX - piece.X) + Math.Abs(targetY - piece.Y) != 1)
                return false;
            return !Outranks(piece, target, board.GameRules);
        }

        #endregion

        #region One orthogonal step (every piece by default)

        private static bool IsValidOneStep(Board board, Piece piece, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;
            if (Math.Abs(targetX - piece.X) + Math.Abs(targetY - piece.Y) != 1)
                return false;
            return CanCapture(board, piece, targetX, targetY);
        }

        private static List<(int x, int y)> GetOneStepMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (board.IsInBoard(newX, newY) && CanCapture(board, piece, newX, newY))
                    legalMoves.Add((newX, newY));
            }
            return legalMoves;
        }

        #endregion

        #region 車衝 (Chariot rush)

        private static bool IsRushCapture(Piece piece, int targetX, int targetY) =>
            Math.Abs(targetX - piece.X) + Math.Abs(targetY - piece.Y) > 1;

        private static bool IsValidRushingChariot(Board board, Piece piece, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if ((dx != 0 && dy != 0) || (dx == 0 && dy == 0))
                return false;
            if (MoveLines.CountPiecesBetween(board, piece.X, piece.Y, targetX, targetY) != 0)
                return false;
            return CanCapture(board, piece, targetX, targetY, ignoreRank: IsRushCapture(piece, targetX, targetY));
        }

        private static List<(int x, int y)> GetRushingChariotMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                while (board.IsInBoard(newX, newY))
                {
                    if (board.Grid[newX, newY] == null)
                    {
                        legalMoves.Add((newX, newY));
                    }
                    else
                    {
                        if (CanCapture(board, piece, newX, newY, ignoreRank: IsRushCapture(piece, newX, newY)))
                            legalMoves.Add((newX, newY));
                        break;
                    }
                    newX += dx;
                    newY += dy;
                }
            }
            return legalMoves;
        }

        #endregion

        #region 馬斜 (Horse diagonal)

        private static bool IsValidDiagonalHorse(Board board, Piece piece, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;
            if (Math.Abs(targetX - piece.X) != 1 || Math.Abs(targetY - piece.Y) != 1)
                return false;
            return CanCapture(board, piece, targetX, targetY, ignoreRank: true);
        }

        private static List<(int x, int y)> GetDiagonalHorseMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MoveDirections.DiagonalOneStep)
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (board.IsInBoard(newX, newY) && CanCapture(board, piece, newX, newY, ignoreRank: true))
                    legalMoves.Add((newX, newY));
            }
            return legalMoves;
        }

        #endregion

        #region Cannon jump

        private static bool IsValidJumpingCannon(Board board, Piece piece, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;
            int dx = targetX - piece.X;
            int dy = targetY - piece.Y;
            if ((dx != 0 && dy != 0) || (dx == 0 && dy == 0))
                return false;
            // Non-capturing move: one step onto an empty square.
            if (board.Grid[targetX, targetY] == null)
                return Math.Abs(dx) + Math.Abs(dy) == 1;
            // Capture: jump exactly one screen, regardless of rank.
            return MoveLines.CountPiecesBetween(board, piece.X, piece.Y, targetX, targetY) == 1
                && CanCapture(board, piece, targetX, targetY, ignoreRank: true);
        }

        private static List<(int x, int y)> GetJumpingCannonMoves(Board board, Piece piece)
        {
            var legalMoves = new List<(int x, int y)>();
            foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
            {
                int newX = piece.X + dx;
                int newY = piece.Y + dy;
                if (!board.IsInBoard(newX, newY))
                    continue;

                // Non-capturing move: one step onto an empty square.
                if (board.Grid[newX, newY] == null)
                    legalMoves.Add((newX, newY));

                // Capture: the first piece along the line is the screen, the next one behind it is the only target.
                bool jumped = false;
                while (board.IsInBoard(newX, newY))
                {
                    if (board.Grid[newX, newY] != null)
                    {
                        if (jumped)
                        {
                            if (CanCapture(board, piece, newX, newY, ignoreRank: true))
                                legalMoves.Add((newX, newY));
                            break;
                        }
                        jumped = true;
                    }
                    newX += dx;
                    newY += dy;
                }
            }
            return legalMoves;
        }

        #endregion
    }
}
