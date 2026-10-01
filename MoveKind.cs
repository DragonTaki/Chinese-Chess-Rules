/* ----- ----- ----- ----- */
// MoveKind.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// What kind of action a <see cref="MoveRecord"/> is. Every kind uses the mover's turn
    /// (clocks, turn switch and undo treat them all as one move).
    /// </summary>
    public enum MoveKind
    {
        /// <summary>An ordinary move, with or without capturing a face-up piece.</summary>
        Move,

        /// <summary>
        /// 翻子 (dark chess, HalfCenter): a face-down piece is turned face up in place
        /// (from-square = to-square). The first flip of a game also decides which colour
        /// each player owns (see <see cref="GameManager.ColorOf"/>).
        /// </summary>
        Flip,
    }
}
