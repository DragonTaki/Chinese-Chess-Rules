/* ----- ----- ----- ----- */
// Elephant.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/02
// Version: v2.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Movements;

namespace Chinese_Chess_v3.Game.Core.Pieces.PieceTypes
{
    /// <summary>
    /// Represents the <b>Elephant (相/象)</b> piece in Chinese Chess.
    /// The Elephant moves exactly 2 squares diagonally and cannot cross the river (except a revealed 揭棋 piece).
    /// Its move can be blocked if the "elephant's eye" (the midpoint of its path) is occupied.
    /// </summary>
    public class Elephant : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Elephant"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
        public Elephant(PieceInfo info)
            : base(info) { }

        /// <summary>
        /// Determines whether the target position is within the legal area for the Elephant.
        /// Elephants cannot cross the river.
        /// </summary>
        /// <param name="board">The board the move is made on.</param>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the Elephant's allowed side; otherwise, <c>false</c>.</returns>
        protected override bool IsDestinationLegalFull(Board board, int targetX, int targetY)
        {
            // 揭棋 (Jieqi/FlipChess — Rules.IsJieqi): once revealed, an
            // Elephant can cross the river freely. A still-hidden Elephant
            // (IsFaceUp false) making its first move is not affected by
            // this — it's bound by the normal river restriction below, same
            // as any other Full-board game.
            if (board.GameRules.IsJieqi && CurrentInfo.IsFaceUp)
                return board.IsInBoard(targetX, targetY);

            if (!board.IsInBoard(targetX, targetY))
                return false;

            // The own half is keyed by colour, not player (Player1 can play Black).
            switch (Color)
            {
                case PieceColor.Black:
                    return targetY <= BoardConstants.Full.RiverLineYBlackSide;

                case PieceColor.Red:
                    return targetY >= BoardConstants.Full.RiverLineYRedSide;

                // Only the Full board's two colours have a river side; the other colours never
                // play on the Full board.
                case PieceColor.Yellow:
                case PieceColor.None:
                default:
                    throw new InvalidOperationException($"{Color} has no side of the river on the Full board");
            }
        }

        /// <summary>
        /// Checks whether the Elephant can move to the target position according to Chinese Chess rules.
        /// <para>
        /// - Must move exactly 2 squares diagonally.
        /// - Cannot cross the river.
        /// - Cannot jump over a piece ("elephant's eye" rule, when <see cref="Rules.CanElephantEyeBlocked"/>).
        /// - Cannot capture an allied piece.
        /// </para>
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current game board instance used to check piece positions.</param>
        /// <returns><c>true</c> if the move is legal for the Elephant; otherwise, <c>false</c>.</returns>
        protected override bool IsValidMoveFull(Board board, int targetX, int targetY)
        {
            // Check if still in valid area
            if (!IsDestinationLegalFull(board, targetX, targetY))
                return false;

            // Check if general will see general after move
            if (WouldExposeGeneralsFull(board, targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            var directions = MovePatterns.GetDiagonalTwoStep(Color);

            // Check if match move rule
            bool matched = false;
            foreach (var (dirX, dirY) in directions)
            {
                if (dx == dirX && dy == dirY)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;

            // Check if elephant's eye is blocked
            if (board.GameRules.CanElephantEyeBlocked && IsElephantEyeBlocked(board, dx, dy))
                return false;

            // Check if there is an ally piece at the destination
            if (board.IsLocationSamePlayerSide(Side, targetX, targetY) == true)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a list of all legal moves the Elephant can make from its current position.
        /// Each move is represented as a tuple of (x, y) coordinates.
        /// </summary>
        /// <param name="board">The current game board state.</param>
        /// <returns>
        /// A list of all possible (x, y) positions the Elephant can legally move to.
        /// </returns>
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetDiagonalTwoStep(Color);

            foreach (var (dx, dy) in directions)
            {
                int newX = X + dx;
                int newY = Y + dy;

                // Skip if outside this piece's legal area (board bounds plus
                // palace / river, same as IsValidMoveFull)
                if (!IsDestinationLegalFull(board, newX, newY))
                    continue;

                // Skip if general will see general after move
                if (WouldExposeGeneralsFull(board, newX, newY))
                    continue;

                // Skip if elephant's eye is blocked
                if (board.GameRules.CanElephantEyeBlocked && IsElephantEyeBlocked(board, dx, dy))
                    continue;

                // Skip if destination occupied by ally
                if (board.IsLocationSamePlayerSide(Side, newX, newY) == true)
                    continue;

                // Add to legal moves
                legalMoves.Add((newX, newY));
            }

            return legalMoves;
        }

        /// <summary>
        /// On HalfCenter (8×4, 明棋／暗棋半盤) there is no river — the
        /// Elephant just moves one square orthogonally like every other
        /// non-Cannon piece there (not two squares diagonally, unlike on
        /// the Full board).
        /// </summary>
        protected override bool IsValidMoveHalfCenter(Board board, int targetX, int targetY) =>
            IsValidOrthogonalOneStepDarkChess(board, targetX, targetY);

        protected override List<(int x, int y)> GetLegalMovesHalfCenter(Board board) =>
            GetOrthogonalOneStepMovesDarkChess(board);

        /// <summary>Same dark-chess mechanic as HalfCenter — see General.cs's HalfCross note.</summary>
        protected override bool IsValidMoveHalfCross(Board board, int targetX, int targetY) =>
            IsValidOrthogonalOneStepDarkChess(board, targetX, targetY);

        protected override List<(int x, int y)> GetLegalMovesHalfCross(Board board) =>
            GetOrthogonalOneStepMovesDarkChess(board);

        private bool IsElephantEyeBlocked(Board board, int dx, int dy)
        {
            // Elephant moves exactly 2 squares diagonally
            if (Math.Abs(dx) != 2 || Math.Abs(dy) != 2)
                throw new ArgumentException($"Not an Elephant move offset: ({dx},{dy})");

            // Compute the intermediate square (the "eye")
            int blockX = X + dx / 2;
            int blockY = Y + dy / 2;

            // Piece exists => blocked
            return board.Grid[blockX, blockY] != null;
        }
    }
}
