/* ----- ----- ----- ----- */
// MoveMatrix.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/30
// Update Date: 2026/10/02
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Movements
{
    /// <summary>
    /// Defines the directional transformation matrices for each piece colour. Orientation is
    /// keyed by colour, never by player number (Player1 can play Black): Red (at the bottom,
    /// high y) is the base (identity matrix); Black is rotated 180° (reverses both X and Y).
    /// Other colours default to Red's matrix.
    /// </summary>
    public static class MoveMatrix
    {
        /// <summary>
        /// Stores each colour’s transformation matrix.
        /// Allows for easy expansion to more colours (e.g. three kingdoms).
        /// </summary>
        private static readonly Dictionary<PieceColor, int[,]> _matrixMap = new()
        {
            [PieceColor.Red] = new int[,]
                {
                    { 1, 0 },
                    { 0, 1 }
                },  // Identity (Base)
            [PieceColor.Black] = new int[,]
                {
                    { -1,  0 },
                    {  0, -1 }
                },  // 180° Rotation
        };

        /// <summary>
        /// Retrieves the transformation matrix for a given colour.
        /// Any undefined colours (e.g. Yellow, None) default to Red.
        /// </summary>
        public static int[,] GetMatrix(PieceColor color)
        {
            if (_matrixMap.TryGetValue(color, out var matrix))
                return matrix;

            // Default to Red if the colour is not found
            return _matrixMap[PieceColor.Red];
        }

        /// <summary>
        /// Transforms a direction vector (dx, dy) based on the colour’s orientation.
        /// </summary>
        /// <param name="dx">Base X direction (Red perspective)</param>
        /// <param name="dy">Base Y direction (Red perspective)</param>
        /// <param name="color">Piece colour</param>
        /// <returns>Transformed (dx, dy)</returns>
        public static (int dx, int dy) TransformDirection(int dx, int dy, PieceColor color)
        {
            var m = GetMatrix(color);
            int tx = m[0, 0] * dx + m[0, 1] * dy;
            int ty = m[1, 0] * dx + m[1, 1] * dy;
            return (tx, ty);
        }

        /// <summary>
        /// Transforms an array of directions according to the colour’s orientation.
        /// </summary>
        /// <param name="directions">Array of base directions (Red perspective)</param>
        /// <param name="color">Piece colour</param>
        /// <returns>Array of transformed directions</returns>
        public static (int dx, int dy)[] TransformDirections((int dx, int dy)[] directions, PieceColor color)
        {
            var m = GetMatrix(color);
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
