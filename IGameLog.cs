/* ----- ----- ----- ----- */
// IGameLog.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Receives the human-readable game log lines <see cref="GameManager"/> writes
    /// (selections, moves, captures, game over). Declared in Core so the rules layer
    /// never references a UI type; the sidebar's logger box implements it and is
    /// handed to <see cref="GameManager.SetLogger"/>.
    /// </summary>
    public interface IGameLog
    {
        void AddMessage(string message);
    }
}
