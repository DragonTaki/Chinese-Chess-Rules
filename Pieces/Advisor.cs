/* ----- ----- ----- ----- */
// Advisor.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/05/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Constants.Game;
using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    /// <summary>
    /// Represents the <b>Advisor (仕/士)</b> piece in Chinese Chess.
    /// The Advisor protects the General and can only move diagonally by one step.
    /// It must always remain within the 3×3 palace area of its own side.
    /// </summary>
    public class Advisor : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Advisor"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the piece.</param>
        /// <param name="y">The initial Y-coordinate of the piece.</param>
        /// <param name="side">The player side this piece belongs to (Red or Black).</param>
        public Advisor(int x, int y, PlayerSide side)
            : base(PieceType.Advisor, x, y, side)
        {
        }

        /// <summary>
        /// Determines whether the target position is within the legal area where this piece is allowed to stay.
        /// For the Advisor, this means staying inside its palace area.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the palace; otherwise, <c>false</c>.</returns>
        protected override bool IsDestinationLegalFull(Board board, int targetX, int targetY)
        {
            if (board.GameRules.CanAdvisorLeavePalace)
            {
                // Only can stay in palace (九宮格)
                if (!board.IsInPalace(X, Y, Side))
                    return false;
            }
            else
            {
                if (!board.IsInBoard(X, Y))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the Advisor can move to the target position according to Chinese Chess rules.
        /// The Advisor must move exactly one step diagonally, remain in its palace, and cannot capture allied pieces.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current game board instance, used to check piece positions.</param>
        /// <returns><c>true</c> if the move is valid; otherwise, <c>false</c>.</returns>
        protected override bool IsValidMoveFull(Board board, int targetX, int targetY)
        {
            int dx = Math.Abs(targetX - X);
            int dy = Math.Abs(targetY - Y);

            // Must move 1 squares diagonally
            if (dx != 1 || dy != 1)
                return false;

            // Check if there is an ally piece at the destination
            if (!IsDestinationLegal(board, targetX, targetY))
                return false;

            return true;
        }

        /// <summary>
        /// Gets a list of all legal moves this Advisor can make from its current position.
        /// Each move is represented as a tuple of (x, y) coordinates.
        /// </summary>
        /// <param name="x">The current X-coordinate of the Advisor.</param>
        /// <param name="y">The current Y-coordinate of the Advisor.</param>
        /// <param name="board">The current game board state.</param>
        /// <returns>
        /// A list of all possible (x, y) positions the Advisor can legally move to.
        /// </returns>
        protected override List<(int x, int y)> GetLegalMovesFull(Board board, int x, int y)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            // Define every move directions
            (int dx, int dy)[] directions = new (int, int)[]
            {
                (1, 1),   // Top-right
                (-1, 1),  // Top-left
                (1, -1),  // Bottom-right
                (-1, -1)  // Bottom-left
            };

            // Try all possible diagonal moves
            foreach (var (dx, dy) in directions)
            {
                int newX = x + dx;
                int newY = y + dy;

                // Skip if outside palace boundaries
                if (!IsInLegalZone(newX, newY))
                    continue;

                // Skip if destination occupied by ally
                if (!IsDestinationLegal(newX, newY, board))
                    continue;

                // Add to legal moves
                legalMoves.Add((newX, newY));
            }

            return legalMoves;
        }
    }
}
