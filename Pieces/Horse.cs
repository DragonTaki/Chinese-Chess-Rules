/* ----- ----- ----- ----- */
// Horse.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/30
// Version: v2.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    /// <summary>
    /// Represents the <b>Horse (傌/馬)</b> piece in Chinese Chess.
    /// The Horse moves in an L-shape (two squares in one direction, one square in perpendicular direction) and 
    /// cannot jump over a piece directly adjacent in the primary direction ("horse leg" rule).
    /// </summary>
    public class Horse : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Horse"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the piece.</param>
        /// <param name="y">The initial Y-coordinate of the piece.</param>
        /// <param name="side">The player side this piece belongs to (Red or Black).</param>
        public Horse(PieceInfo info)
            : base(info) { }

        /// <summary>
        /// Checks whether the Horse can move to the target position according to Chinese Chess rules.
        /// <para>
        /// - Must move in an "L" shape (1+2 or 2+1 squares).  
        /// - Must not be blocked by a piece in the primary movement direction ("horse leg").  
        /// - Cannot capture an allied piece.
        /// </para>
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current game board used to check piece positions.</param>
        /// <returns><c>true</c> if the move is legal for the Horse; otherwise, <c>false</c>.</returns>
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

            var directions = MovePatterns.GetDiagonalLShape(Side);

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

            // Check if horse's leg is hobbled
            if (board.GameRules.CanHorseLegHobbled && IsHorseLegHobbled(board, dx, dy))
                return false;

            // Check if there is an ally piece at the destination
            if (board.IsLocationSamePlayerSide(Side, targetX, targetY) == true)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a list of all legal moves the Horse can make from its current position.
        /// Each move is represented as a tuple of (x, y) coordinates.
        /// </summary>
        /// <param name="x">The current X-coordinate of the Horse.</param>
        /// <param name="y">The current Y-coordinate of the Horse.</param>
        /// <param name="board">The current board state.</param>
        /// <returns>A list of all possible (x, y) positions the Horse can legally move to.</returns>
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

                // Skip if horse's leg is hobbled
                if (board.GameRules.CanHorseLegHobbled && IsHorseLegHobbled(board, dx, dy))
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

        /// <summary>
        /// Determines whether the Horse's leg is hobbled in a specific movement direction.
        /// </summary>
        /// <param name="dx">The X-offset of the move (target - current).</param>
        /// <param name="dy">The Y-offset of the move (target - current).</param>
        /// <param name="board">The current board used to check blocking pieces.</param>
        /// <returns><c>true</c> if the horse's leg is hobbled; otherwise, <c>false</c>.</returns>
        private bool IsHorseLegHobbled(Board board, int dx, int dy)
        {
            // Two different class:
            // (A) dx = ±2, check x = ±1
            // (B) dy = ±2, check y = ±1
            int blockX = X;
            int blockY = Y;

            if (Math.Abs(dx) == 2)
                blockX += Math.Sign(dx);
            if (Math.Abs(dy) == 2)
                blockY += Math.Sign(dy);

            // Piece exists => hobbled
            return board.Grid[blockX, blockY] != null;
        }
    }
}
