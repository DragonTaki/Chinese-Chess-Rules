/* ----- ----- ----- ----- */
// Cannon.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/01
// Version: v2.1
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
    /// That is the Full board; on the dark-chess HalfCenter board it steps one square instead
    /// (see <see cref="IsValidMoveHalfCenter"/>).
    /// </summary>
    public class Cannon : Piece
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cannon"/> class with the specified initial state.
        /// </summary>
        /// <param name="info">The piece\'s initial state (type, position, color, side); copied.</param>
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
            if (WouldExposeGeneralsFull(board, targetX, targetY))
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

                // Continue scanning until reaching the edge of the board
                while (board.IsInBoard(newX, newY))
                {
                    Piece target = board.Grid[newX, newY];

                    if (!jumped)
                    {
                        if (target == null)
                        {
                            // Can move freely before jumping (unless general
                            // would see general after move — checked per square)
                            if (!WouldExposeGeneralsFull(board, newX, newY))
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
                        // After jumping, skip empty squares; the next piece
                        // encountered must be an enemy to capture
                        if (target != null)
                        {
                            if (target.Side != this.Side && !WouldExposeGeneralsFull(board, newX, newY))
                            {
                                // Add to legal moves
                                legalMoves.Add((newX, newY));
                            }
                            break;  // Stop searching after a potential capture
                        }
                    }

                    newX += dx;
                    newY += dy;
                }
            }

            return legalMoves;
        }

        /// <summary>
        /// On HalfCenter (8×4, 明棋／暗棋半盤, Taiwanese dark chess), the Cannon's
        /// non-capturing move is one square orthogonally onto an empty square, like
        /// every other piece — it does not slide. How it captures is governed by
        /// <c>Rules.IsCannonMustJumpToCapture</c> (包跳吃子):
        /// <list type="bullet">
        /// <item>enabled (the default, standard Taiwanese rule): only by jumping
        /// exactly one piece (the screen, 炮台 — any piece, face-up or face-down)
        /// along a row or column, any distance, onto the first piece behind it.
        /// The jump capture ignores rank (不受等級限制): any enemy piece, the General
        /// included. It cannot capture an adjacent piece directly.</item>
        /// <item>disabled (炮不跳, a local variant): no jumps at all — it captures an
        /// adjacent piece by rank, exactly like the other one-step pieces.</item>
        /// </list>
        /// Hidden targets follow <see cref="Piece"/>'s dark-chess capture check either way.
        /// </summary>
        protected override bool IsValidMoveHalfCenter(Board board, int targetX, int targetY)
        {
            if (!board.GameRules.IsCannonMustJumpToCapture)
                return IsValidOrthogonalOneStepDarkChess(board, targetX, targetY);

            if (!IsDestinationLegalHalfCenter(board, targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            // Straight line only, and not its own square
            if ((dx != 0 && dy != 0) || (dx == 0 && dy == 0))
                return false;

            Piece targetPiece = board.Grid[targetX, targetY];

            // Non-capturing move: one step onto an empty square
            if (targetPiece == null)
                return Math.Abs(dx) + Math.Abs(dy) == 1;

            // Capture: jump exactly one screen, regardless of rank
            return CountPiecesBetween(X, Y, targetX, targetY, board) == 1
                && CanCaptureInDarkChess(board, targetX, targetY, ignoreRank: true);
        }

        /// <summary>See <see cref="IsValidMoveHalfCenter"/>.</summary>
        protected override List<(int x, int y)> GetLegalMovesHalfCenter(Board board)
        {
            if (!board.GameRules.IsCannonMustJumpToCapture)
                return GetOrthogonalOneStepMovesDarkChess(board);

            List<(int x, int y)> legalMoves = new List<(int x, int y)>();

            foreach (var (dx, dy) in MoveDirections.OrthogonalOneStep)
            {
                int newX = X + dx;
                int newY = Y + dy;

                if (!board.IsInBoard(newX, newY))
                    continue;

                // Non-capturing move: one step onto an empty square
                if (board.Grid[newX, newY] == null)
                    legalMoves.Add((newX, newY));

                // Capture: the first piece along the line is the screen, the next one
                // behind it (empty squares in between are skipped) is the only target.
                bool jumped = false;
                while (board.IsInBoard(newX, newY))
                {
                    Piece target = board.Grid[newX, newY];
                    if (target != null)
                    {
                        if (jumped)
                        {
                            if (CanCaptureInDarkChess(board, newX, newY, ignoreRank: true))
                                legalMoves.Add((newX, newY));
                            break;
                        }
                        jumped = true;
                    }

                    newX += dx;
                    newY += dy;
                }
            }

            return legalMoves;
        }

        /// <summary>
        /// HalfCross (三國暗棋) — see General.cs's HalfCross note. Still the
        /// long-range version: slides any number of empty squares, and
        /// <c>Rules.IsCannonMustJumpToCapture</c> governs the jump-to-capture
        /// requirement, captures following rank. Not the HalfCenter rules: HalfCross
        /// is being re-specified as a separate system (docs/DARK-CHESS-RULES.md), so
        /// its rules are left as they are until then.
        /// </summary>
        protected override bool IsValidMoveHalfCross(Board board, int targetX, int targetY)
        {
            if (!board.IsInBoard(targetX, targetY))
                return false;

            int dx = targetX - X;
            int dy = targetY - Y;

            if (dx != 0 && dy != 0)
                return false;

            int count = CountPiecesBetween(X, Y, targetX, targetY, board);
            Piece targetPiece = board.Grid[targetX, targetY];

            if (targetPiece == null)
                return count == 0;

            int requiredScreens = board.GameRules.IsCannonMustJumpToCapture ? 1 : 0;
            return count == requiredScreens && CanCaptureInDarkChess(board, targetX, targetY);
        }

        protected override List<(int x, int y)> GetLegalMovesHalfCross(Board board)
        {
            List<(int x, int y)> legalMoves = new List<(int x, int y)>();
            bool mustJump = board.GameRules.IsCannonMustJumpToCapture;

            var directions = MovePatterns.GetOrthogonalOneStep(Side);

            foreach (var (dx, dy) in directions)
            {
                bool jumped = false;

                int newX = X + dx;
                int newY = Y + dy;

                while (board.IsInBoard(newX, newY))
                {
                    Piece target = board.Grid[newX, newY];

                    if (!jumped)
                    {
                        if (target == null)
                        {
                            legalMoves.Add((newX, newY));
                        }
                        else if (!mustJump)
                        {
                            if (CanCaptureInDarkChess(board, newX, newY))
                                legalMoves.Add((newX, newY));
                            break;
                        }
                        else
                        {
                            jumped = true;
                        }
                    }
                    else
                    {
                        if (target != null)
                        {
                            if (CanCaptureInDarkChess(board, newX, newY))
                                legalMoves.Add((newX, newY));
                            break;
                        }
                    }

                    newX += dx;
                    newY += dy;
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