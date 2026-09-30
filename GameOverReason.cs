/* ----- ----- ----- ----- */
// GameOverReason.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.1
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Why <see cref="GameManager.GameOver"/> was raised.
    /// </summary>
    public enum GameOverReason
    {
        /// <summary>The loser is in check and has no legal move (將死).</summary>
        Checkmate,

        /// <summary>
        /// The loser is not in check but has no legal move (困斃) — in xiangqi the
        /// stalemated side loses. Includes the case where every remaining move would
        /// leave the Generals facing each other.
        /// </summary>
        Stalemate,

        /// <summary>The loser's total or step time ran out (see Rules.EndGameWhenTimesUp).</summary>
        TimeUp,

        /// <summary>The loser resigned (see <see cref="GameManager.Resign"/>).</summary>
        Resign,
    }
}
