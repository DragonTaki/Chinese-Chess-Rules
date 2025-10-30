/* ----- ----- ----- ----- */
// Cannon.cs
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

namespace Chinese_Chess_v3.Game.Core.Pieces.PieceTypes
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
        public Cannon(PieceInfo info)
            : base(info) { }

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
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetOrthogonalOneStep(Side);

            foreach (var (dx, dy) in directions)
            {
                bool jumped = false;  // Whether the Cannon has jumped over a piece

                int newX = X + dx;
                int newY = Y + dy;

                // Skip if general will see general after move
                if (newX != X && !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(X))
                    return legalMoves;

                // Continue scanning until reaching the edge of the board
                while (board.IsInBoard(newX, newY))
                {
                    Piece target = board.Grid[newX, newY];

                    if (!jumped)
                    {
                        if (target == null)
                        {
                            // Can move freely before jumping
                            legalMoves.Add((newX, newY));
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
                            legalMoves.Add((newX, newY));
                        }
                        break;  // Stop searching after a potential capture
                    }

                    newX += dx;
                    newY += dy;
                }
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