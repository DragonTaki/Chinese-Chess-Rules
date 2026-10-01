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

        /// <summary>
        /// 暗吃 (hidden capture, <c>Rules.CanCaptureHiddenPiece</c>): the mover moved onto a
        /// face-down piece, which was revealed as an enemy piece it may capture by the normal
        /// rules (rank order, the Soldier/General pair; a Cannon's jump capture ignores rank),
        /// and captured it.
        /// </summary>
        HiddenCapture,

        /// <summary>
        /// 暗吃 onto a face-down piece revealed as the mover's own: the target stays, now face
        /// up, and the mover stays on its from-square.
        /// </summary>
        HiddenOwnPiece,

        /// <summary>
        /// 暗吃 onto a face-down enemy piece the mover may not capture (a stronger one, or a
        /// Soldier when the mover is a General) with <c>Rules.IsCaptureHiddenPieceStrongerSuicide</c> off:
        /// the target stays, now face up, and the mover returns to its from-square.
        /// </summary>
        HiddenStrongerReturn,

        /// <summary>
        /// The same with <c>Rules.IsCaptureHiddenPieceStrongerSuicide</c> on: the mover dies
        /// (taken off the board) and the target stays, now face up.
        /// </summary>
        HiddenStrongerSuicide,
    }
}
