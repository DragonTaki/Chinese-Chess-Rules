/* ----- ----- ----- ----- */
// ThreeKingdomsStandings.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// Where the players of a 三國 board stand: who still plays and the ranking (shared by the
    /// family, the client's <see cref="GameManager"/> and the rules host).
    /// </summary>
    public static class ThreeKingdomsStandings
    {
        /// <summary>
        /// Whether <paramref name="side"/> still plays: it has not resigned and is not out — a player
        /// with a team is out once that team has no piece left; one without a team once no team is
        /// left for it to claim (no piece of an unclaimed team remains).
        /// </summary>
        public static bool IsPlaying(Board board, PlayerSide side)
        {
            var state = board.ThreeKingdoms ?? throw new InvalidOperationException("A Three Kingdoms board has no ThreeKingdomsState");
            int i = ThreeKingdomsState.Index(side);
            if (state.Resigned[i])
                return false;
            return !IsOut(board, state, side);
        }

        /// <summary>
        /// Every player ranked, first place first (author decision 4 for 計分: the points above one's
        /// team's threshold). Ties keep the turn order. 全滅: the players still in first (by points),
        /// then the players out, the last one out first. 得失分: the points of one's team still on the
        /// board plus the points captured. 先得 200 分: the points captured.
        /// </summary>
        public static IReadOnlyList<PlayerSide> Rank(Board board)
        {
            var state = board.ThreeKingdoms ?? throw new InvalidOperationException("A Three Kingdoms board has no ThreeKingdomsState");
            var rules = board.GameRules;
            var players = ThreeKingdomsState.Players;
            return rules.HalfCrossWinCondition switch
            {
                HalfCrossWinCondition.Annihilation => players
                    .OrderBy(p => state.OutOrder[ThreeKingdomsState.Index(p)] == 0 ? 0 : 1)
                    .ThenByDescending(p => state.OutOrder[ThreeKingdomsState.Index(p)])
                    .ThenByDescending(p => state.Scores[ThreeKingdomsState.Index(p)])
                    .ToList(),
                _ => players.OrderByDescending(p => RankingScore(board, p)).ToList(),
            };
        }

        /// <summary>The number <see cref="Rank"/> compares for <paramref name="side"/> (計分, 得失分, 先得 200 分).</summary>
        public static int RankingScore(Board board, PlayerSide side)
        {
            var state = board.ThreeKingdoms ?? throw new InvalidOperationException("A Three Kingdoms board has no ThreeKingdomsState");
            var rules = board.GameRules;
            int i = ThreeKingdomsState.Index(side);
            int team = state.Teams[i];
            switch (rules.HalfCrossWinCondition)
            {
                case HalfCrossWinCondition.ScoreBalance:
                    // 總分 − 被吃子分數 = the points of the team's pieces still on the board.
                    int remaining = team == 0 ? 0 : board.QueryPieces(side: side)
                        .Sum(p => ThreeKingdomsTeams.Points(p.Type, rules.HalfCrossWinCondition));
                    return remaining + state.Scores[i];
                case HalfCrossWinCondition.FirstTo200:
                case HalfCrossWinCondition.Annihilation:
                    return state.Scores[i];
                default:
                    return state.Scores[i] - (team == 0 ? 0 : ThreeKingdomsTeams.Threshold(team));
            }
        }

        internal static bool IsOut(Board board, ThreeKingdomsState state, PlayerSide side)
        {
            if (state.Teams[ThreeKingdomsState.Index(side)] != 0)
                return board.QueryPieces(side: side).Count == 0;
            return !board.QueryPieces(side: PlayerSide.None)
                .Any(p => state.OwnerOf(ThreeKingdomsTeams.TeamOf(p.Type, p.Color)) == PlayerSide.None);
        }

    }
}
