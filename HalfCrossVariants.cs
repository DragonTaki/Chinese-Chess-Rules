/* ----- ----- ----- ----- */
// HalfCrossVariants.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/02
// Update Date: 2026/10/02
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// 三國半盤 way of deciding the winner (勝負方式):
    /// the author's own scoring by default, the wiki's ways as alternatives (<c>ThreeKingdomsStandings.Rank</c>).
    /// 收軍 (<see cref="Recall"/>) is not decided yet, so a game with it cannot be started.
    /// </summary>
    public enum HalfCrossWinCondition
    {
        /// <summary>計分（預設, the author's scoring): 車／俥、將、帥 2 points, every other piece 1; the 帥將兵卒 team wins at 12 captured points, the other two at 10.</summary>
        Points,

        /// <summary>全滅 (wiki): the side that eliminates both others is first.</summary>
        Annihilation,

        /// <summary>收軍 (wiki): the first side to capture as many pieces as it holds recalls its army and is first.</summary>
        Recall,

        /// <summary>得失分 (wiki): in a deadlock the higher score (total - lost + captured) wins.</summary>
        ScoreBalance,

        /// <summary>先得 200 分 (wiki): only captured points (wiki values) count; the first side to reach 200 wins.</summary>
        FirstTo200,
    }
}
