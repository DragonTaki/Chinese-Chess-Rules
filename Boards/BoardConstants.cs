/* ----- ----- ----- ----- */
// BoardConstants.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/13
// Update Date: 2025/10/29
// Version: v2.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core.Boards
{
    public static class BoardConstants
    {
        public static class Full
        {
            /* ----- Defining the board's grid size ----- */
            public const int Columns = 9;
            public const int Rows = 10;

            /*
                    X →           Black Side
               (0,Y)(1,Y)(2,Y)(3,Y)(4,Y)(5,Y)(6,Y)(7,Y)(8,Y)
             (X, 0)  +---+---+---+---+---+---+---+---+ ← Y=0
                     |   |   |   | \ | / |   |   |   |
             (X, 1)  +---+---+---+---+---+---+---+---+ ← Y=1
                     |   |   |   | / | \ |   |   |   |
             (X, 2)  +---+---+---+---+---+---+---+---+ ← Y=2
                     |   |   |   |   |   |   |   |   |
             (X, 3)  +---+---+---+---+---+---+---+---+ ← Y=3
                     |   |   |   |   |   |   |   |   |
             (X, 4)  +---+---+---+---+---+---+---+---+ ← Y=4
                     |       T h e   R i v e r       |
             (X, 5)  +---+---+---+---+---+---+---+---+ ← Y=5
                     |   |   |   |   |   |   |   |   |
             (X, 6)  +---+---+---+---+---+---+---+---+ ← Y=6
                     |   |   |   |   |   |   |   |   |
             (X, 7)  +---+---+---+---+---+---+---+---+ ← Y=7
                     |   |   |   | \ | / |   |   |   |
             (X, 8)  +---+---+---+---+---+---+---+---+ ← Y=8
                     |   |   |   | / | \ |   |   |   |
             (X, 9)  +---+---+---+---+---+---+---+---+ ← Y=9
                     0   1   2   3   4   5   6   7   8 
                    X →             Red Side
            */
            // Left to right (x-axis): 0~8; Top to bottom (y-axis): 0~9
            // Red area (y-axis): 0~4; Black area (y-axis): 5~9

            /* ----- In palace: General, Advisor ----- */
            // Both side palace area 3 <= X <= 5
            public static readonly (int MinX, int MaxX) PalaceXRange = (3, 5);
            // Red palace area (3, 7) to (5, 9)
            public static readonly (int MinY, int MaxY) RedPalaceYRange = (7, 9);

            // Black palace area (3, 0) to (5, 2)
            public static readonly (int MinY, int MaxY) BlackPalaceYRange = (0, 2);

            /* ----- In own side OR crossed river ----- */
            public const int RiverLineYRedSide = 5;    // Red side (Y >= 5)
            public const int RiverLineYBlackSide = 4;  // Black side (Y <= 4)
        }

        public static class HalfCenter
        {
            /* ----- Defining the board's grid size ----- */
            // For "明棋半盤", "暗棋半盤"
            public const int Columns = 8;
            public const int Rows = 4;
            /*
                    X →           Black Side
                 (0,Y)(1,Y)(2,Y)(3,Y)(4,Y)(5,Y)(6,Y)(7,Y)
                     +---+---+---+---+---+---+---+---+
             (X, 0)  |   |   |   |   |   |   |   |   | ← Y=0
                     +---+---+---+---+---+---+---+---+
             (X, 1)  |   |   |   |   |   |   |   |   | ← Y=1
                     +---+---+---+---+---+---+---+---+
             (X, 2)  |   |   |   |   |   |   |   |   | ← Y=2
                     +---+---+---+---+---+---+---+---+
             (X, 3)  |   |   |   |   |   |   |   |   | ← Y=3
                     +---+---+---+---+---+---+---+---+
                       0   1   2   3   4   5   6   7
                    X →             Red Side
            */
            // Left to right (x-axis): 0~7; Top to bottom (y-axis): 0~3
        }

        public static class HalfCross
        {
            /* ----- Defining the board's grid size ----- */
            // For only "三國半盤象棋"
            public const int Columns = 9;
            public const int Rows = 5;
            /*
                    X →           Black Side
               (0,Y)(1,Y)(2,Y)(3,Y)(4,Y)(5,Y)(6,Y)(7,Y)(8,Y)
             (X, 0)  +---+---+---+---+---+---+---+---+ ← Y=0
                     |   |   |   |   |   |   |   |   |
             (X, 1)  +---+---+---+---+---+---+---+---+ ← Y=1
                     |   |   |   |   |   |   |   |   |
             (X, 2)  +---+---+---+---+---+---+---+---+ ← Y=2
                     |   |   |   |   |   |   |   |   |
             (X, 3)  +---+---+---+---+---+---+---+---+ ← Y=3
                     |   |   |   |   |   |   |   |   |
             (X, 4)  +---+---+---+---+---+---+---+---+ ← Y=4
                     0   1   2   3   4   5   6   7   8 
                    X →             Red Side
            */
            // Left to right (x-axis): 0~8; Top to bottom (y-axis): 0~4
        }
    }
}
