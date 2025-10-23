/* ----- ----- ----- ----- */
// Cannon.cs
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
    /// Represents the <b>Cannon (炮/包)</b> piece in Chinese Chess.
    /// The Cannon moves like the Rook — any number of empty squares horizontally or vertically — 
    /// but captures differently: it must have exactly one piece between itself and its target when capturing.
    /// </summary>
    public class Cannon : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cannon"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the piece.</param>
        /// <param name="y">The initial Y-coordinate of the piece.</param>
        /// <param name="side">The player side this piece belongs to (Red or Black).</param>
        public Cannon(int x, int y, PlayerSide side)
            : base(PieceType.Cannon, x, y, side)
        {
        }

        /// <summary>
        /// Determines whether the target position is within the legal area where this piece is allowed to move.
        /// The Cannon has no palace or river restrictions; it only needs to stay within the board boundaries.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within the game board bounds; otherwise, <c>false</c>.</returns>
        public override bool IsInLegalZone(int targetX, int targetY)
        {
            // No specific zone limit for chariot, but method reserved for consistency
            return BoardConstants.IsInBounds(targetX, targetY);
        }

        /// <summary>
        /// Checks whether the Cannon can move to the target position according to Chinese Chess rules.
        /// <para>
        /// - The Cannon must move strictly in a straight line (horizontal or vertical).  
        /// - For a normal move (non-capture), there must be no pieces in between.  
        /// - For a capture, there must be exactly one piece between the Cannon and its target, and the target must be an enemy.
        /// </para>
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current game board instance used to check piece positions.</param>
        /// <returns><c>true</c> if the move follows the Cannon's movement rules; otherwise, <c>false</c>.</returns>
        public override bool IsValidMove(int targetX, int targetY, Board board)
        {
            if (!IsInLegalZone(targetX, targetY))
                return false;
                
            int dx = targetX - X;
            int dy = targetY - Y;

            // Only allow straight line movement (no diagonal moves)
            if (dx != 0 && dy != 0)
                return false;

            // Count how many pieces are between start and end positions
            int count = CountPiecesBetween(X, Y, targetX, targetY, board);

            Piece targetPiece = board.Grid[targetX, targetY];
            if (targetPiece == null)
            {
                // No piece on target — must have no pieces in between
                return count == 0;
            }
            else
            {
                // Capturing — must have exactly one piece in between, and target must be an enemy
                return count == 1 && targetPiece.Side != this.Side;
            }
        }

        /// <summary>
        /// Gets a list of all legal moves the Cannon can make from its current position.
        /// Each move is represented as a tuple of (x, y) coordinates.
        /// </summary>
        /// <param name="x">The current X-coordinate of the Cannon.</param>
        /// <param name="y">The current Y-coordinate of the Cannon.</param>
        /// <param name="board">The current game board state.</param>
        /// <returns>
        /// A list of all possible (x, y) positions the Cannon can legally move to.
        /// </returns>
        public override List<(int x, int y)> GetLegalMoves(int x, int y, Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            // Define every move directions
            (int dx, int dy)[] directions = new (int, int)[]
            {
                (1, 0),   // Right
                (-1, 0),  // Left
                (0, 1),   // Up
                (0, -1)   // Down
            };

            foreach (var (dx, dy) in directions)
            {
                bool jumped = false;  // Whether the Cannon has jumped over a piece

                int currX = x + dx;
                int currY = y + dy;

                // Continue scanning until reaching the edge of the board
                while (BoardConstants.IsInBounds(currX, currY))
                {
                    Piece target = board.Grid[currX, currY];

                    if (!jumped)
                    {
                        if (target == null)
                        {
                            // Can move freely before jumping
                            legalMoves.Add((currX, currY));
                        }
                        else
                        {
                            // The first encountered piece is the "screen" piece to jump over
                            jumped = true;
                        }
                    }
                    else
                    {
                        // After jumping, the next piece encountered must be an enemy to capture
                        if (target != null && target.Side != this.Side)
                        {
                            // Add to legal moves
                            legalMoves.Add((currX, currY));
                        }
                        break;  // Stop searching after a potential capture
                    }

                    currX += dx;
                    currY += dy;
                }
            }

            return legalMoves;
        }

        /// <summary>
        /// Counts how many pieces exist between two positions along a straight line.
        /// Used by the Cannon to validate its movement or capture.
        /// </summary>
        /// <param name="startX">The X-coordinate of the starting position.</param>
        /// <param name="startY">The Y-coordinate of the starting position.</param>
        /// <param name="endX">The X-coordinate of the ending position.</param>
        /// <param name="endY">The Y-coordinate of the ending position.</param>
        /// <param name="board">The current game board used to access piece positions.</param>
        /// <returns>The number of pieces found between the start and end positions.</returns>
        private int CountPiecesBetween(int startX, int startY, int endX, int endY, Board board)
        {
            int count = 0;

            int dx = Math.Sign(endX - startX);  // Step direction in X
            int dy = Math.Sign(endY - startY);  // Step direction in Y

            int x = startX + dx;
            int y = startY + dy;

            // Traverse until reaching the destination
            while (x != endX || y != endY)
            {
                if (board.Grid[x, y] != null)
                    count++;  // Count each intervening piece

                x += dx;
                y += dy;
            }

            return count;
        }
    }
}