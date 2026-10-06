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
    /// The wiki's 收軍 is not real play (author 2026-10-06: made up, nobody uses it) and is not offered.
    /// </summary>
    public enum HalfCrossWinCondition
    {
        /// <summary>計分（預設, the author's scoring): every piece 1 point, Generals included; ranked by the points above the team's starting piece count (帥將兵卒 12, the other two 10), a tie going to whoever reached it first.</summary>
        Points,

        /// <summary>全滅 (wiki): the side that eliminates both others is first.</summary>
        Annihilation,

        /// <summary>得失分 (wiki): in a deadlock the higher score (total - lost + captured) wins.</summary>
        ScoreBalance,

        /// <summary>先得 200 分 (wiki): only captured points (wiki values) count; the first side to reach 200 wins.</summary>
        FirstTo200,
    }
}
