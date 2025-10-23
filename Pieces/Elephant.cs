/* ----- ----- ----- ----- */
// Elephant.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/22
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Constants.Game;
using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core.Pieces
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
        public Elephant(int x, int y, PlayerSide side)
            : base(PieceType.Elephant, x, y, side)
        {
        }

        /// <summary>
        /// Determines whether the target position is within the legal area for the Elephant.
        /// Elephants cannot cross the river.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the Elephant's allowed side; otherwise, <c>false</c>.</returns>
        public override bool IsInLegalZone(int targetX, int targetY)
        {
            // Elephants cannot across the river (不可過河)
            if (Side == PlayerSide.Red)
                return targetY >= BoardConstants.RedYSideRiverLine && targetY <= 9;
            else
                return targetY <= BoardConstants.BlackYSideRiverLine && targetY >= 0;
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
        public override bool IsValidMove(int targetX, int targetY, Board board)
        {
            int dx = targetX - X;
            int dy = targetY - Y;

            // Must move 2 squares diagonally (象走田)
            if (Math.Abs(dx) != 2 || Math.Abs(dy) != 2)
                return false;

            // Check if within allowed side
            if (!IsInLegalZone(targetX, targetY))
                return false;

            // Determine the elephant's eye position
            int midX = X + dx / 2;
            int midY = Y + dy / 2;

            // Check if in board
            if (midX < 0 || midX >= BoardConstants.Columns || midY < 0 || midY >= BoardConstants.Rows)
                return false;  // Outside the bound
        
            // Check if "elephant's eye" is blocked (卡象眼)
            if (board.Grid[midX, midY] != null)
                return false;

            // Check if there is an ally piece at the destination
            if (!IsDestinationLegal(targetX, targetY, board))
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
        public override List<(int x, int y)> GetLegalMoves(int x, int y, Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            // Define every move directions
            (int dx, int dy)[] directions = new (int, int)[]
            {
                (2, 2),   // Top-right
                (-2, 2),  // Top-left
                (2, -2),  // Bottom-right
                (-2, -2)  // Bottom-left
            };

            // Try all possible diagonal moves
            foreach (var (dx, dy) in directions)
            {
                int newX = x + dx;
                int newY = y + dy;

                if (!IsInLegalZone(newX, newY))
                    continue;

                int midX = x + dx / 2;
                int midY = y + dy / 2;

                // Skip if elephant's eye is blocked
                if (board.Grid[midX, midY] != null)
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