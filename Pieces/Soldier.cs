/* ----- ----- ----- ----- */
// Soldier.cs
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
    /// Represents the <b>Soldier (兵/卒)</b> piece in Chinese Chess.
    /// <para>
    /// Soldiers move 1 step forward before crossing the river and can move horizontally 
    /// (left or right) after crossing the river. They cannot move backward.
    /// </para>
    /// </summary>
    public class Soldier : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Soldier"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the Soldier.</param>
        /// <param name="y">The initial Y-coordinate of the Soldier.</param>
        /// <param name="side">The player side this Soldier belongs to (Red or Black).</param>
        public Soldier(int x, int y, PlayerSide side)
            : base(PieceType.Soldier, x, y, side)
        {
        }

        /// <summary>
        /// Determines whether the target position is within the board bounds.
        /// Soldiers have no special zone limitation.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the board; otherwise, <c>false</c>.</returns>
        public override bool IsInLegalZone(int targetX, int targetY)
        {
            // No specific zone limit for chariot, but method reserved for consistency
            return BoardConstants.IsInBounds(targetX, targetY);
        }

        /// <summary>
        /// Determines whether a move to the target position is valid according to Chinese Chess rules.
        /// <para>
        /// - Can move 1 step forward anytime.  
        /// - Can move 1 step horizontally only after crossing the river.  
        /// - Cannot move backward.  
        /// - Cannot capture a piece from the same side.
        /// </para>
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current board state used to check piece positions.</param>
        /// <returns><c>true</c> if the move is valid for the Soldier; otherwise, <c>false</c>.</returns>
        public override bool IsValidMove(int targetX, int targetY, Board board)
        {
            if (!IsInLegalZone(targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            // Soldier can only move 1 step
            if (Math.Abs(dx) + Math.Abs(dy) != 1)
                return false;

            // Forward direction: Red (Y--), Black (Y++)
            int forward = (Side == PlayerSide.Red) ? -1 : 1;

            // Always allow forward step
            if (dy == forward && dx == 0)
                return true;

            // Allow side move only after crossing river
            if (dy == 0 && Math.Abs(dx) == 1 && HasCrossedRiver(Y))
                return true;

            // Check if there is an ally piece at the destination
            if (!IsDestinationLegal(targetX, targetY, board))
                return false;

            return false;
        }

        /// <summary>
        /// Gets all legal moves the Soldier can make from its current position.
        /// </summary>
        /// <param name="x">The current X-coordinate of the Soldier.</param>
        /// <param name="y">The current Y-coordinate of the Soldier.</param>
        /// <param name="board">The current board state.</param>
        /// <returns>A list of all possible (x, y) positions the Soldier can legally move to.</returns>
        public override List<(int x, int y)> GetLegalMoves(int x, int y, Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            // Define every possible move directions
            int forward = (Side == PlayerSide.Red) ? -1 : 1;
            (int dx, int dy)[] directions = HasCrossedRiver(y)
                ? new (int, int)[]
                {
                    (0, forward),   // Forward
                    (-1, 0),        // Left
                    (1, 0)          // Right
                }
                : new (int, int)[]
                {
                    (0, forward)    // Only Forward
                };

            foreach (var (dx, dy) in directions)
            {
                int newX = x + dx;
                int newY = y + dy;

                if (!BoardConstants.IsInBounds(newX, newY))
                    continue;

                // Skip if destination occupied by ally
                if (!IsDestinationLegal(newX, newY, board))
                    continue;

                // Add to legal moves
                legalMoves.Add((newX, newY));
            }

            return legalMoves;
        }

        /// <summary>
        /// Determines whether the Soldier has crossed the river.
        /// </summary>
        /// <param name="y">The current Y-coordinate of the Soldier.</param>
        /// <returns><c>true</c> if the Soldier has crossed the river; otherwise, <c>false</c>.</returns>
        private bool HasCrossedRiver(int y)
        {
            return Side == PlayerSide.Red
                ? y <= BoardConstants.RedYSideRiverLine
                : y >= BoardConstants.BlackYSideRiverLine;
        }
    }
}