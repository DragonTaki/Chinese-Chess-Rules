/* ----- ----- ----- ----- */
// GameKind.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/02
// Update Date: 2026/10/02
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// The kinds of game a new game can be (the new-game menu's modes). Each kind has its own
    /// rule set (<see cref="GameRuleSets"/>; the player's choices per kind are the
    /// <c>[rules.*]</c> sections of settings.ini, docs/SETTINGS.md). Whether a half-board game
    /// is dark or open is the kind itself (<see cref="DarkHalf"/> / <see cref="OpenHalf"/>),
    /// not a rule setting.
    /// </summary>
    public enum GameKind
    {
        /// <summary>傳統大盤: the standard Full-board game (also endgames, openings and saved games).</summary>
        Traditional,

        /// <summary>揭棋大盤: the Full board with the pieces shuffled face down (<c>Board.IsJieqi</c>; no start position yet).</summary>
        Flip,

        /// <summary>暗棋半盤: the HalfCenter board, every piece face down (<see cref="Rules.IsHiddenChess"/> on).</summary>
        DarkHalf,

        /// <summary>明棋半盤: the HalfCenter board, every piece face up (<see cref="Rules.IsHiddenChess"/> off).</summary>
        OpenHalf,

        /// <summary>三國半盤: the HalfCross board for three players (its rules are still being specified).</summary>
        ThreeKingdoms,
    }
}
