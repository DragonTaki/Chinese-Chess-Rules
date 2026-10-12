/* ----- ----- ----- ----- */
// MoveRequest.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/12
// Update Date: 2026/10/12
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>A move intent to send to the authoritative server, in board coordinates.</summary>
    public sealed record MoveRequest(int FromX, int FromY, int ToX, int ToY);
}
