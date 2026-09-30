/* ----- ----- ----- ----- */
// GameOverReason.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Why <see cref="GameManager.GameOver"/> was raised.
    /// </summary>
    public enum GameOverReason
    {
        /// <summary>The loser's total or step time ran out (see Rules.EndGameWhenTimesUp).</summary>
        TimeUp,
    }
}
