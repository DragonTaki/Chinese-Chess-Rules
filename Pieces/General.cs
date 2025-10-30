/* ----- ----- ----- ----- */
// General.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/30
// Version: v2.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Represents the <b>General (帥/將)</b> piece in Chinese Chess.
    /// The General moves exactly 1 square horizontally or vertically and must stay within the palace (九宮格).
    /// It cannot move diagonally and cannot capture allied pieces.
    /// </summary>
    public class General : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="General"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the piece.</param>
        /// <param name="y">The initial Y-coordinate of the piece.</param>
        /// <param name="side">The player side this piece belongs to (Red or Black).</param>
        public General(PieceInfo info)
            : base(info) { }

        /// <summary>
        /// Determines whether the target position is within the palace where the General can move.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the palace; otherwise, <c>false</c>.</returns>
        protected override bool IsDestinationLegalFull(Board board, int targetX, int targetY)
        {
            if (!board.GameRules.CanGeneralLeavePalace)
            {
                // Only can stay in palace (九宮格)
                if (!board.IsInPalace(Side, targetX, targetY))
                    return false;
            }
            else
            {
                if (!board.IsInBoard(targetX, targetY))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the General can move to the target position according to Chinese Chess rules.
        /// <para>
        /// - Must move exactly 1 square horizontally or vertically.  
        /// - Must remain within the palace.  
        /// - Cannot capture an allied piece.
        /// </para>
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current game board instance used to check piece positions.</param>
        /// <returns><c>true</c> if the move is legal for the General; otherwise, <c>false</c>.</returns>
        protected override bool IsValidMoveFull(Board board, int targetX, int targetY)
        {
            // Check if still in valid area
            if (!IsDestinationLegalFull(board, targetX, targetY))
                return false;

            // Check if two side general face to face
            if (!board.GameRules.CanGeneralSeeGeneral && !board.IsGeneralTargetLegal(Side, targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            var directions = MovePatterns.GetOrthogonalOneStep(Side);

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

            // Check if there is an ally piece at the destination
            if (board.IsLocationSamePlayerSide(Side, targetX, targetY) == true)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a list of all legal moves the General can make from its current position.
        /// Each move is represented as a tuple of (x, y) coordinates.
        /// </summary>
        /// <param name="x">The current X-coordinate of the General.</param>
        /// <param name="y">The current Y-coordinate of the General.</param>
        /// <param name="board">The current game board state.</param>
        /// <returns>
        /// A list of all possible (x, y) positions the General can legally move to.
        /// </returns>
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetOrthogonalOneStep(Side);

            foreach (var (dx, dy) in directions)
            {
                int newX = X + dx;
                int newY = Y + dy;

                // Skip if outside board bounds
                if (!board.IsInBoard(newX, newY))
                    continue;

                // Skip if destination occupied by ally
                if (board.IsLocationSamePlayerSide(Side, newX, newY) == true)
                    continue;

                // Check if two side general face to face
                if (!board.GameRules.CanGeneralSeeGeneral && !board.IsGeneralTargetLegal(Side, newX, newY))
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
    }
}
