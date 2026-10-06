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
    /// The three teams (分隊) and the scoring of 三國 (DARK-CHESS-RULES §1.2, author decisions win):
    /// ① 帥將兵卒 ② 仕相俥傌炮 ③ 士象車馬包. The wiki's second split (讓子用) is not real play
    /// (author 2026-10-06: made up online, nobody uses it), so there is only this one.
    /// </summary>
    public static class ThreeKingdomsTeams
    {
        /// <summary>The team holding the Generals (將帥隊).</summary>
        public const int GeneralsTeam = 1;

        /// <summary>The team (1..3) a piece of <paramref name="type"/> and <paramref name="color"/> belongs to.</summary>
        /// <exception cref="ArgumentException">Not a piece of a 三國 set.</exception>
        public static int TeamOf(PieceType type, PieceColor color) => type switch
        {
            PieceType.General or PieceType.Soldier => GeneralsTeam,
            PieceType.Advisor or PieceType.Elephant or PieceType.Chariot or PieceType.Horse or PieceType.Cannon =>
                color == PieceColor.Red ? 2 : color == PieceColor.Black ? 3 : throw new ArgumentException($"No team for {color} {type}"),
            _ => throw new ArgumentException($"No team for {type}"),
        };

        /// <summary>The team of <paramref name="piece"/>.</summary>
        public static int TeamOf(PieceInfo piece) => TeamOf(piece.Type, piece.Color);

        /// <summary>
        /// 計分 (<see cref="HalfCrossWinCondition.Points"/>, author 2026-10-02): the points a team needs —
        /// 12 for the Generals' team, 10 for the other two. Players are ranked by points above it.
        /// </summary>
        public static int Threshold(int team) => team == GeneralsTeam ? 12 : 10;

        /// <summary>
        /// The points capturing <paramref name="type"/> is worth under <paramref name="condition"/>:
        /// the author's scoring (車／俥、將、帥 2, every other piece 1) for 計分, 全滅 and 收軍; the wiki's
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
