/* ----- ----- ----- ----- */
// Elephant.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/30
// Version: v2.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Movements;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pieces.PieceTypes
{
    /// <summary>
    /// Represents the <b>Elephant (相/象)</b> piece in Chinese Chess.
    /// The Elephant moves exactly 2 squares diagonally and cannot cross the river.
    /// Its move can be blocked if the "elephant's eye" (the midpoint of its path) is occupied.
    /// </summary>
    public class Elephant : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Elephant"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the piece.</param>
        /// <param name="y">The initial Y-coordinate of the piece.</param>
        /// <param name="side">The player side this piece belongs to (Red or Black).</param>
        public Elephant(PieceInfo info)
            : base(info) { }

        /// <summary>
        /// Determines whether the target position is within the legal area for the Elephant.
        /// Elephants cannot cross the river.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the Elephant's allowed side; otherwise, <c>false</c>.</returns>
        protected override bool IsDestinationLegalFull(Board board, int targetX, int targetY)
        {
            switch (Side)
            {
                case PlayerSide.Black:
                    return targetY <= BoardConstants.Full.RiverLineYBlackSide;

                case PlayerSide.Red:
                    return targetY >= BoardConstants.Full.RiverLineYRedSide;

                case PlayerSide.None:
                case PlayerSide.Neutral:
                default:
                    throw new Exception("Unknown player side");  // Defensive check
            }
        }

        /// <summary>
        /// Checks whether the Elephant can move to the target position according to Chinese Chess rules.
        /// <para>
        /// - Must move exactly 2 squares diagonally.  
        /// - Cannot cross the river.  
        /// - Cannot jump over a piece ("elephant's eye" rule).  
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
            if (targetX != X && !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(X))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            var directions = MovePatterns.GetDiagonalTwoStep(Side);

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
            if (board.GameRules.CanElephantEyeBlockd && IsElephantEyeBlocked(board, dx, dy))
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
        /// <param name="x">The current X-coordinate of the Elephant.</param>
        /// <param name="y">The current Y-coordinate of the Elephant.</param>
        /// <param name="board">The current game board state.</param>
        /// <returns>
        /// A list of all possible (x, y) positions the Elephant can legally move to.
        /// </returns>
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetDiagonalLShape(Side);

            foreach (var (dx, dy) in directions)
            {
                int newX = X + dx;
                int newY = Y + dy;

                // Skip if outside board bounds
                if (!board.IsInBoard(newX, newY))
                    continue;

                // Skip if general will see general after move
                if (newX != X && !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(X))
                    return legalMoves;

                // Skip if elephant's eye is blocked
                if (board.GameRules.CanElephantEyeBlockd && IsElephantEyeBlocked(board, dx, dy))
                    continue;

                // Skip if destination occupied by ally
                if (board.IsLocationSamePlayerSide(Side, newX, newY) == true)
                    continue;

                // Add to legal moves
                legalMoves.Add((newX, newY));
            }

            return legalMoves;
        }

        protected override List<(int x, int y)> GetLegalMovesHalfCenter(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();
            // Not implement yet
            return legalMoves;
        }

        protected override List<(int x, int y)> GetLegalMovesHalfCross(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();
            // Not implement yet
            return legalMoves;
        }

        private bool IsElephantEyeBlocked(Board board, int dx, int dy)
        {
            // Elephant moves exactly 2 squares diagonally
            if (Math.Abs(dx) != 2 || Math.Abs(dy) != 2)
                throw new Exception("Invalid Elephant move offset.");

            // Compute the intermediate square (the "eye")
            int blockX = X + dx / 2;
            int blockY = Y + dy / 2;

            // Piece exists => blocked
            return board.Grid[blockX, blockY] != null;
        }
    }
}
