/* ----- ----- ----- ----- */
// Horse.cs
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
        public Horse(int x, int y, PlayerSide side)
            : base(PieceType.Horse, x, y, side)
        {
        }

        /// <summary>
        /// Determines whether the target position is within the board bounds.
        /// Horses have no special zone limitation.
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
        public override bool IsValidMove(int targetX, int targetY, Board board)
        {
            if (!IsInLegalZone(targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            int absDx = Math.Abs(dx);
            int absDy = Math.Abs(dy);

            // Must move in "L" shape (1 step + 2 step) (馬走日)
            if (!((absDx == 1 && absDy == 2) || (absDx == 2 && absDy == 1)))
                return false;

            // Check if "horse's leg" is hobbled (蹩馬腳)
            int blockX = X + (dx / absDx);  // one step in X if 2-step move is in X
            int blockY = Y + (dy / absDy);  // one step in Y if 2-step move is in Y

            if (absDx == 2 && board.Grid[blockX, Y] != null)
                return false;
            if (absDy == 2 && board.Grid[X, blockY] != null)
                return false;

            // Check if there is an ally piece at the destination
            if (!IsDestinationLegal(targetX, targetY, board))
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
        public override List<(int x, int y)> GetLegalMoves(int x, int y, Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            // Define all L-shaped move directions
            (int dx, int dy)[] directions = new (int, int)[]
            {
                (2, 1),    // Right 2, Up 1
                (1, 2),    // Right 1, Up 2
                (-1, 2),   // Left 1, Up 2
                (-2, 1),   // Left 2, Up 1
                (-2, -1),  // Left 2, Down 1
                (-1, -2),  // Left 1, Down 2
                (1, -2),   // Right 1, Down 2
                (2, -1)    // Right 2, Down 1
            };

            foreach (var (dx, dy) in directions)
            {
                int newX = x + dx;
                int newY = y + dy;

                // Skip if outside board bounds
                if (!BoardConstants.IsInBounds(newX, newY))
                    continue;

                // Determine the horse's leg position
                int blockX = x + (Math.Abs(dx) == 2 ? Math.Sign(dx) : 0);
                int blockY = y + (Math.Abs(dy) == 2 ? Math.Sign(dy) : 0);

                // Skip if horse's leg is hobbled
                if (board.Grid[blockX, blockY] != null)
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