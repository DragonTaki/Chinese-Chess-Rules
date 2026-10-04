/* ----- ----- ----- ----- */
// PlayerTimerPresets.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/31
// Update Date: 2026/10/04
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

namespace Chinese_Chess_v3.Game.Core.Players
{
    /// <summary>A clock preset: its display name and the three limits, each with its unit in the type.</summary>
    /// <param name="Name">Display name (慢棋1 ...).</param>
    /// <param name="TotalTime">Each side's total time (局時).</param>
    /// <param name="StepTime">Time per move (步時).</param>
    /// <param name="Increment">Time added after each move (加秒).</param>
    public readonly record struct TimerPreset(string Name, TimeSpan TotalTime, TimeSpan StepTime, TimeSpan Increment);

    /// <summary>
    /// Predefined clock presets, offered as 計時預設 on each 單機規則設定 tab (author decision 2026-10-02).
    /// </summary>
    public static class PlayerTimerPresets
    {
        public static readonly IReadOnlyList<TimerPreset> Presets = new[]
        {
            new TimerPreset("慢棋1", TimeSpan.FromMinutes(50), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(20)),
            new TimerPreset("慢棋2", TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(20)),
            new TimerPreset("快棋1", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10)),
            new TimerPreset("快棋2", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(5)),
            new TimerPreset("超快棋", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(3)),
        };
    }
}
