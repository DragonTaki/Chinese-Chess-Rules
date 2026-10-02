/* ----- ----- ----- ----- */
// MovePatterns.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/30
// Update Date: 2026/10/02
// Version: v1.1
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Movements
{
    /// <summary>
    /// Provides colour-specific movement patterns (oriented by piece colour, not player) by combining
    /// base directions (from MoveDirections) with transformation matrices (from MoveMatrix).
    /// </summary>
    public static class MovePatterns
    {
        /// <summary>
        /// Returns all orthogonal one-step moves for the given colour.
        /// </summary>
        public static (int dx, int dy)[] GetOrthogonalOneStep(PieceColor color)
            => MoveMatrix.TransformDirections(MoveDirections.OrthogonalOneStep, color);

        /// <summary>
        /// Returns all diagonal one-step moves for the given colour.
        /// </summary>
        public static (int dx, int dy)[] GetDiagonalOneStep(PieceColor color)
            => MoveMatrix.TransformDirections(MoveDirections.DiagonalOneStep, color);

        /// <summary>
        /// Returns all diagonal two-step moves for the given colour.
        /// </summary>
        public static (int dx, int dy)[] GetDiagonalTwoStep(PieceColor color)
            => MoveMatrix.TransformDirections(MoveDirections.DiagonalTwoStep, color);

        /// <summary>
        /// Returns all L-shaped moves (for the Horse) for the given colour.
        /// </summary>
        public static (int dx, int dy)[] GetDiagonalLShape(PieceColor color)
            => MoveMatrix.TransformDirections(MoveDirections.DiagonalLShape, color);

        /// <summary>
        /// Returns all soldier moves for the given colour and state (<paramref name="crossedRiver"/>: false = not crossed yet, true = crossed).
        /// </summary>
        public static (int dx, int dy)[] GetSoldierDirections(PieceColor color, bool crossedRiver)
        {
            int index = crossedRiver ? 1 : 0;
            return MoveMatrix.TransformDirections(MoveDirections.SoldierFullBoard[index], color);
        }
    }
}
