/* ----- ----- ----- ----- */
// MovePatterns.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/30
// Version: v1.0
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Movements
{
    /// <summary>
    /// Provides side-specific movement patterns by combining
    /// base directions (from MoveDirections) with transformation matrices (from MoveMatrix).
    /// </summary>
    public static class MovePatterns
    {
        /// <summary>
        /// Returns all orthogonal one-step moves for the given side.
        /// </summary>
        public static (int dx, int dy)[] GetOrthogonalOneStep(PlayerSide side)
            => MoveMatrix.TransformDirections(MoveDirections.OrthogonalOneStep, side);

        /// <summary>
        /// Returns all diagonal one-step moves for the given side.
        /// </summary>
        public static (int dx, int dy)[] GetDiagonalOneStep(PlayerSide side)
            => MoveMatrix.TransformDirections(MoveDirections.DiagonalOneStep, side);

        /// <summary>
        /// Returns all diagonal two-step moves for the given side.
        /// </summary>
        public static (int dx, int dy)[] GetDiagonalTwoStep(PlayerSide side)
            => MoveMatrix.TransformDirections(MoveDirections.DiagonalTwoStep, side);

        /// <summary>
        /// Returns all L-shaped moves (for knights) for the given side.
        /// </summary>
        public static (int dx, int dy)[] GetDiagonalLShape(PlayerSide side)
            => MoveMatrix.TransformDirections(MoveDirections.DiagonalLShape, side);

        /// <summary>
        /// Returns all soldier moves for the given side and state (0=未過河, 1=過河).
        /// </summary>
        public static (int dx, int dy)[] GetSoldierDirections(PlayerSide side, bool crossedRiver)
        {
            int index = crossedRiver ? 1 : 0;
            return MoveMatrix.TransformDirections(MoveDirections.SoldierFullBoard[index], side);
        }
    }
}
