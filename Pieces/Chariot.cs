/* ----- ----- ----- ----- */
// Chariot.cs
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
    /// Represents the <b>Chariot (俥/車)</b> piece in Chinese Chess.
    /// The Chariot moves any number of squares horizontally or vertically, like the Rook in Western chess.
    /// It cannot jump over other pieces and cannot capture allied pieces.
    /// </summary>
    public class Chariot : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Chariot"/> class with the specified position and player side.
        /// </summary>
        /// <param name="x">The initial X-coordinate of the piece.</param>
        /// <param name="y">The initial Y-coordinate of the piece.</param>
        /// <param name="side">The player side this piece belongs to (Red or Black).</param>
        public Chariot(int x, int y, PlayerSide side)
            : base(PieceType.Chariot, x, y, side)
        {
        }

        /// <summary>
        /// Determines whether the target position is within the legal area where this piece can move.
        /// The Chariot has no palace or river restrictions; it only needs to stay within board bounds.
        /// </summary>
        /// <param name="targetX">The X-coordinate of the destination.</param>
        /// <param name="targetY">The Y-coordinate of the destination.</param>
        /// <returns><c>true</c> if the destination is within board boundaries; otherwise, <c>false</c>.</returns>
        public override bool IsInLegalZone(int targetX, int targetY)
        {
            // No specific zone limit for Chariot, kept for consistency with other pieces
            return BoardConstants.IsInBounds(targetX, targetY);
        }

        /// <summary>
        /// Checks whether the Chariot can move to the target position according to Chinese Chess rules.
        /// <para>
        /// - The Chariot moves in a straight line horizontally or vertically.  
        /// - It cannot jump over other pieces.  
        /// - The destination cannot contain an allied piece.
        /// </para>
        /// </summary>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <param name="board">The current game board instance used to check piece positions.</param>
        /// <returns><c>true</c> if the move follows the Chariot's movement rules; otherwise, <c>false</c>.</returns>
        public override bool IsValidMove(int targetX, int targetY, Board board)
        {
            if (!IsInLegalZone(targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            // Only allow straight line movement (no diagonal moves)
            if (dx != 0 && dy != 0)
                return false;

            // Determine direction and step (get X/Y, +1/-1)
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);

            int currX = X + stepX;
            int currY = Y + stepY;

            // Check path obstruction (車衝無障礙物)
            while (currX != targetX || currY != targetY)
            {
                if (board.Grid[currX, currY] != null)
                    return false;

                currX += stepX;
                currY += stepY;
            }

            // Check if there is an ally piece at the destination
            if (!IsDestinationLegal(targetX, targetY, board))
                return false;

            return true;
        }

        /// <summary>
        /// Gets a list of all legal moves the Chariot can make from its current position.
        /// Each move is represented as a tuple of (x, y) coordinates.
        /// </summary>
        /// <param name="x">The current X-coordinate of the Chariot.</param>
        /// <param name="y">The current Y-coordinate of the Chariot.</param>
        /// <param name="board">The current game board state.</param>
        /// <returns>
        /// A list of all possible (x, y) positions the Chariot can legally move to.
        /// </returns>
        public override List<(int x, int y)> GetLegalMoves(int x, int y, Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            // Define every move directions
            (int dx, int dy)[] directions = new (int, int)[]
            {
                (1, 0),  // Right
                (-1, 0), // Left
                (0, 1),  // Up
                (0, -1)  // Down
            };

            foreach (var (dx, dy) in directions)
            {
                int currX = x + dx;
                int currY = y + dy;

                // Continue scanning until reaching the edge of the board or an obstacle
                while (BoardConstants.IsInBounds(currX, currY))
                {
                    Piece obstacle = board.Grid[currX, currY];

                    if (obstacle == null)
                    {
                        // No piece — legal move
                        legalMoves.Add((currX, currY));
                    }
                    else
                    {
                        // Encounter piece — can capture if enemy, then stop
                        if (obstacle.Side != this.Side)
                            // Add to legal moves
                            legalMoves.Add((currX, currY));
                        break;
                    }

                    currX += dx;
                    currY += dy;
                }
            }

            return legalMoves;
        }
    }
}