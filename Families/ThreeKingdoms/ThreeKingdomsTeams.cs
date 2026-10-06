/* ----- ----- ----- ----- */
// ThreeKingdomsTeams.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// 三國's scoring (DARK-CHESS-RULES §1.2, author decisions win). The teams are the game's
    /// <see cref="Rules.HalfCrossTeams"/> (自訂分隊); a team's threshold is its piece count
    /// (<see cref="ThreeKingdomsTeamSplit.PieceCount"/>).
    /// </summary>
    public static class ThreeKingdomsTeams
    {
        /// <summary>
        /// The points capturing <paramref name="type"/> is worth under <paramref name="condition"/>:
        /// the author's scoring (車／俥、將、帥 2, every other piece 1) for 計分 and 全滅; the wiki's
        /// values (將帥車 50, 馬炮 35, 兵卒 20, 士象 15) for 得失分 and 先得 200 分.
        /// </summary>
        public static int Points(PieceType type, HalfCrossWinCondition condition) => condition switch
        {
            HalfCrossWinCondition.ScoreBalance or HalfCrossWinCondition.FirstTo200 => type switch
            {
                PieceType.General or PieceType.Chariot => 50,
                PieceType.Horse or PieceType.Cannon => 35,
                PieceType.Soldier => 20,
                _ => 15,
            },
            _ => type is PieceType.General or PieceType.Chariot ? 2 : 1,
        };
    }
}
