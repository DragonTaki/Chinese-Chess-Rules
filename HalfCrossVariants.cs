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
    /// 三國半盤 team split (分隊, docs/DARK-CHESS-RULES.md §1.2). A rule option only: no
    /// gameplay reads it yet (未實作; <see cref="Rules.HalfCrossTeamSetup"/> is the split the
    /// unfinished HalfCross code uses).
    /// </summary>
    public enum HalfCrossTeamVariant
    {
        /// <summary>第一種（較公平，預設）: 帥將兵卒 / 仕相俥傌炮 / 士象車馬包.</summary>
        Standard,

        /// <summary>第二種（讓子用）: 兵卒 / 帥仕相將士象 / 俥傌炮車馬包.</summary>
        Handicap,
    }

    /// <summary>
    /// 三國半盤 way of deciding the winner (勝負方式, docs/DARK-CHESS-RULES.md §1.2「作者決定」 4-5):
    /// the author's own scoring by default, the wiki's ways as alternatives. A rule option only:
    /// no gameplay reads it yet (未實作).
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
