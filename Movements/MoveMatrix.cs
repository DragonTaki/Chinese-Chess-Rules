/* ----- ----- ----- ----- */
// MoveMatrix.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/30
// Update Date: 2025/10/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Movements
{
    /// <summary>
    /// Defines the directional transformation matrices for each player side.
    /// Red side is the base (identity matrix).
    /// Black side is rotated 180° (reverses both X and Y).
    /// Other sides default to Red's matrix.
    /// </summary>
    public static class MoveMatrix
    {
        /// <summary>
        /// Stores each side’s transformation matrix.
        /// Allows for easy expansion to more sides (e.g. three kingdoms).
        /// </summary>
        private static readonly Dictionary<PlayerSide, int[,]> matrixMap = new()
        {
            [PlayerSide.Red] = new int[,]
                {
                    { 1, 0 },
                    { 0, 1 }
                },  // Identity (Base)
            [PlayerSide.Black] = new int[,]
                {
                    { -1,  0 },
                    {  0, -1 }
                },  // 180° Rotation
        };

        /// <summary>
        /// Retrieves the transformation matrix for a given side.
        /// Any undefined sides (e.g. Neutral, None) default to Red.
        /// </summary>
        public static int[,] GetMatrix(PlayerSide side)
        {
            if (matrixMap.TryGetValue(side, out var matrix))
                return matrix;

            // Default to Red if side not found
            return matrixMap[PlayerSide.Red];
        }

        /// <summary>
        /// Transforms a direction vector (dx, dy) based on the side’s orientation.
        /// </summary>
        /// <param name="dx">Base X direction (Red perspective)</param>
        /// <param name="dy">Base Y direction (Red perspective)</param>
        /// <param name="side">Player side</param>
        /// <returns>Transformed (dx, dy)</returns>
        public static (int dx, int dy) TransformDirection(int dx, int dy, PlayerSide side)
        {
            var m = GetMatrix(side);
            int tx = m[0, 0] * dx + m[0, 1] * dy;
            int ty = m[1, 0] * dx + m[1, 1] * dy;
            return (tx, ty);
        }

        /// <summary>
        /// Transforms an array of directions according to the side’s orientation.
        /// </summary>
        /// <param name="directions">Array of base directions (Red perspective)</param>
        /// <param name="side">Player side</param>
        /// <returns>Array of transformed directions</returns>
        public static (int dx, int dy)[] TransformDirections((int dx, int dy)[] directions, PlayerSide side)
        {
            var m = GetMatrix(side);
            var result = new (int dx, int dy)[directions.Length];

            for (int i = 0; i < directions.Length; i++)
            {
                int tx = m[0, 0] * directions[i].dx + m[0, 1] * directions[i].dy;
                int ty = m[1, 0] * directions[i].dx + m[1, 1] * directions[i].dy;
                result[i] = (tx, ty);
            }

            return result;
        }
    }
}
