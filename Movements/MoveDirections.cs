/* ----- ----- ----- ----- */
// MoveDirections.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/29
// Update Date: 2025/10/29
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core.Movements
{
    public static class MoveDirections
    {
        public static readonly (int dx, int dy)[] OrthogonalOneStep = new (int, int)[]
        {
            (-1, 0),  // Left
            (1, 0),   // Right
            (0, -1),   // Up
            (0, 1),  // Down
        };

        public static readonly (int dx, int dy)[] DiagonalOneStep = new (int, int)[]
        {
            (-1, -1),   // Top-left
            (1, -1),    // Top-right
            (-1, 1),  // Bottom-left
            (1, 1),   // Bottom-right
        };

        public static readonly (int dx, int dy)[] DiagonalTwoStep = new (int, int)[]
        {
            (-2, -2),   // Top-left
            (2, -2),    // Top-right
            (-2, 2),  // Bottom-left
            (2, 2),   // Bottom-right
        };

        public static readonly (int dx, int dy)[] DiagonalLShape = new (int, int)[]
        {
            (-1, -2),   // Left 1, Up 2
            (-2, -1),   // Left 2, Up 1
            (1, -2),    // Right 1, Up 2
            (2, -1),    // Right 2, Up 1
            (-1, 2),  // Left 1, Down 2
            (-2, 1),  // Left 2, Down 1
            (1, 2),   // Right 1, Down 2
            (2, 1),   // Right 2, Down 1
        };

        public static readonly (int dx, int dy)[][] SoldierFullBoard = new (int, int)[][]
        {
            new (int,int)[]  // Not cross river yet
                {
                    (0, -1)  // Up
                },
            new (int,int)[]  // Crossed
                {
                    (0, -1),   // Up
                    (-1, 0),  // Left
                    (1, 0)    // Right
                }
        };
    }
}
