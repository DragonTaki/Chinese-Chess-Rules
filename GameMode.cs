/* ----- ----- ----- ----- */
// GameMode.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// What kind of game a <see cref="GameManager"/> is playing (<see cref="GameManager.Mode"/>):
    /// decides the saved game's <c>[Event]</c> tag and its save folder / file name.
    /// </summary>
    public enum GameMode
    {
        /// <summary>對局: a game from the standard start or a custom position.</summary>
        Normal,

        /// <summary>殘局: a game started from an endgame puzzle.</summary>
        Endgame,

        /// <summary>開局: a game started from an opening (its preset line already played).</summary>
        Opening,
    }
}
