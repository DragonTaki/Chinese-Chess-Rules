/* ----- ----- ----- ----- */
// PlayerTimerPresets.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/31
// Update Date: 2025/10/31
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

namespace Chinese_Chess_v3.Game.Core.Players
{
    /// <summary>
    /// 定義預設的棋局計時設定組
    /// </summary>
    public static class PlayerTimerPresets
    {
        public static readonly List<(int Total, int Step, int Add, string Name)> Presets = new()
        {
            (50, 5, 20, "慢棋1"),
            (30, 5, 20, "慢棋2"),
            (10, 3, 10, "快棋1"),
            (10, 3, 5,  "快棋2"),
            (5,  1, 3,  "超快棋")
        };
    }
}
