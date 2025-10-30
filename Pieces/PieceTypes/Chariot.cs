/* ----- ----- ----- ----- */
// Chariot.cs
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
        public Chariot(PieceInfo info)
            : base(info) { }

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

            // Determine direction and step (get X/Y, +1/-1)
            int stepX = Math.Sign(dx);
            int stepY = Math.Sign(dy);

            int newX = X + stepX;
            int newY = Y + stepY;

            // Check path obstruction (車衝無障礙物)
            while (newX != targetX || newY != targetY)
            {
                if (board.Grid[newX, newY] != null)
                    return false;

                newX += stepX;
                newY += stepY;
            }

            // Check if there is an ally piece at the destination
            if (board.IsLocationSamePlayerSide(Side, targetX, targetY) == true)
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
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetOrthogonalOneStep(Side);

            foreach (var (dx, dy) in directions)
            {
                int newX = X + dx;
                int newY = Y + dy;

                // Skip if general will see general after move
                if (newX != X && !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(X))
                    return legalMoves;

                // Continue scanning until reaching the edge of the board or an obstacle
                while (board.IsInBoard(newX, newY))
                {
                    Piece obstacle = board.Grid[newX, newY];

                    if (obstacle == null)
                    {
                        // No piece — legal move
                        legalMoves.Add((newX, newY));
                    }
                    else
                    {
                        // Encounter piece — can capture if enemy, then stop
                        if (obstacle.Side != this.Side)
                            // Add to legal moves
                            legalMoves.Add((newX, newY));
                        break;
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
    }
}
