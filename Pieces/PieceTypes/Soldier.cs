/* ----- ----- ----- ----- */
// Soldier.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/02
// Version: v2.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Movements;

namespace Chinese_Chess_v3.Game.Core.Pieces.PieceTypes
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
        /// Initializes a new instance of the <see cref="Soldier"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece's initial state (type, position, color, side); copied.</param>
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
            if (WouldExposeGeneralsFull(board, targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            var directions = MovePatterns.GetSoldierDirections(Color, HasCrossedRiver(Y));

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
        /// <param name="board">The current board state.</param>
        /// <returns>A list of all possible (x, y) positions the Soldier can legally move to.</returns>
        protected override List<(int x, int y)> GetLegalMovesFull(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            var directions = MovePatterns.GetSoldierDirections(Color, HasCrossedRiver(Y));

            foreach (var (dx, dy) in directions)
            {
                int newX = X + dx;
                int newY = Y + dy;

                // Skip if outside this piece's legal area (the board bounds, same as
                // IsValidMoveFull)
                if (!IsDestinationLegalFull(board, newX, newY))
                    continue;

                // Skip if general will see general after move
                if (WouldExposeGeneralsFull(board, newX, newY))
                    continue;

                // Skip if destination occupied by ally
                if (board.IsLocationSamePlayerSide(Side, newX, newY) == true)
                    continue;

                // Add to legal moves
                legalMoves.Add((newX, newY));
            }

            return legalMoves;
        }

        /// <summary>
        /// On HalfCenter (8×4, 明棋／暗棋半盤) there is no river — the
        /// Soldier is just the weakest rank and moves one square
        /// orthogonally in any direction, like every other non-Cannon piece
        /// there (not forward-only, unlike on the Full board).
        /// </summary>
        protected override bool IsValidMoveHalfCenter(Board board, int targetX, int targetY) =>
            IsValidOrthogonalOneStepDarkChess(board, targetX, targetY);

        protected override List<(int x, int y)> GetLegalMovesHalfCenter(Board board) =>
            GetOrthogonalOneStepMovesDarkChess(board);

        /// <summary>Same dark-chess mechanic as HalfCenter — see General.cs's HalfCross note.</summary>
        protected override bool IsValidMoveHalfCross(Board board, int targetX, int targetY) =>
            IsValidOrthogonalOneStepDarkChess(board, targetX, targetY);

        protected override List<(int x, int y)> GetLegalMovesHalfCross(Board board) =>
            GetOrthogonalOneStepMovesDarkChess(board);

        /// <summary>
        /// Determines whether the Soldier has crossed the river.
        /// </summary>
        /// <param name="y">The current Y-coordinate of the Soldier.</param>
        /// <returns><c>true</c> if the Soldier has crossed the river; otherwise, <c>false</c>.</returns>
        private bool HasCrossedRiver(int y)
        {
            // The river side is keyed by colour, not player (Player1 can play Black).
            switch (Color)
            {
                // Black's own half is Y 0-4 (RiverLineYBlackSide is its last
                // row), Red's is Y 5-9 (RiverLineYRedSide is its first row);
                // a soldier has crossed only once it stands on the far half — the
                // same test as Board.IsPassRiver.
                case PieceColor.Black:
                    return y > BoardConstants.Full.RiverLineYBlackSide;

                case PieceColor.Red:
                    return y < BoardConstants.Full.RiverLineYRedSide;

                // Only the Full board's two colours have a river side; the other colours never
                // play on the Full board.
                case PieceColor.Yellow:
                case PieceColor.None:
                default:
                    throw new InvalidOperationException($"{Color} has no side of the river on the Full board");
            }
        }
    }
}
