/* ----- ----- ----- ----- */
// Soldier.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/30
// Version: v2.0
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
        public Soldier(PieceInfo info)
            : base(info) { }

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
        protected override bool IsValidMoveFull(Board board, int targetX, int targetY)
        {
            // Check if still in valid area
            if (!IsDestinationLegalFull(board, targetX, targetY))
                return false;

            // Check if general will see general after move
            if (targetX != X && targetX != X && !board.GameRules.CanGeneralSeeGeneral && board.IsGeneralFaceToFaceAfterMove(X))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            var directions = MovePatterns.GetSoldierDirections(Side, HasCrossedRiver(Y));

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
        /// Gets all legal moves the Soldier can make from its current position.
        /// </summary>
        /// <param name="x">The current X-coordinate of the Soldier.</param>
        /// <param name="y">The current Y-coordinate of the Soldier.</param>
        /// <param name="board">The current board state.</param>
        /// <returns>A list of all possible (x, y) positions the Soldier can legally move to.</returns>
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetSoldierDirections(Side, HasCrossedRiver(Y));

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
        /// Determines whether the Soldier has crossed the river.
        /// </summary>
        /// <param name="y">The current Y-coordinate of the Soldier.</param>
        /// <returns><c>true</c> if the Soldier has crossed the river; otherwise, <c>false</c>.</returns>
        private bool HasCrossedRiver(int y)
        {
            switch (Side)
            {
                case PlayerSide.Black:
                    return y >= BoardConstants.Full.RiverLineYBlackSide;

                case PlayerSide.Red:
                    return y <= BoardConstants.Full.RiverLineYRedSide;

                case PlayerSide.None:
                case PlayerSide.Neutral:
                default:
                    throw new Exception("Unknown player side");  // Defensive check
            }
        }
    }
}
