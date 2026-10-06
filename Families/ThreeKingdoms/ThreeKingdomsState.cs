/* ----- ----- ----- ----- */
// ThreeKingdomsState.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// What a 三國 game knows beyond its pieces (kept on the board, <see cref="Boards.Board.ThreeKingdoms"/>,
    /// so the stateless rules can read it): which team each player claimed, each player's points and
    /// who resigned (棄權). Indexed by player number 1..3 (<see cref="PlayerSide.Player1"/> = 1).
    /// </summary>
    public sealed class ThreeKingdomsState
    {
        /// <summary>The team (1..3) each player claimed; 0 while unclaimed. Index 0 is unused.</summary>
        public int[] Teams { get; } = new int[4];

        /// <summary>Each player's points (captures, by the game's way of scoring). Index 0 is unused.</summary>
        public int[] Scores { get; } = new int[4];

        /// <summary>
        /// Whether each player resigned (棄權, author 2026-10-02): its pieces stay on the board and may
        /// be captured (and scored), its turns are skipped. Index 0 is unused.
        /// </summary>
        public bool[] Resigned { get; } = new bool[4];

        /// <summary>
        /// The order in which the players went out (their last piece was captured): 1 for the first
        /// one out, 2 for the next; 0 while a player is in. Ranks the players out of 全滅. Index 0 is unused.
        /// </summary>
        public int[] OutOrder { get; } = new int[4];

        /// <summary>The player number of <paramref name="side"/> (1..3).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="side"/> is not Player1..Player3.</exception>
        public static int Index(PlayerSide side) => side switch
        {
            PlayerSide.Player1 => 1,
            PlayerSide.Player2 => 2,
            PlayerSide.Player3 => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Not a Three Kingdoms player"),
        };

        /// <summary>The player with number <paramref name="index"/> (1..3).</summary>
        public static PlayerSide SideOf(int index) => index switch
        {
            1 => PlayerSide.Player1,
            2 => PlayerSide.Player2,
            3 => PlayerSide.Player3,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Not a Three Kingdoms player number"),
        };

        /// <summary>The three players in turn order (Player1 → Player2 → Player3, author 2026-10-02).</summary>
        public static readonly PlayerSide[] Players = { PlayerSide.Player1, PlayerSide.Player2, PlayerSide.Player3 };

        /// <summary>The player that claimed <paramref name="team"/>; <see cref="PlayerSide.None"/> while nobody has.</summary>
        public PlayerSide OwnerOf(int team)
        {
            for (int i = 1; i <= 3; i++)
            {
                if (Teams[i] == team)
                    return SideOf(i);
            }
            return PlayerSide.None;
        }

        /// <summary>A copy (for undo).</summary>
        public ThreeKingdomsState Clone()
        {
            var copy = new ThreeKingdomsState();
            Array.Copy(Teams, copy.Teams, 4);
            Array.Copy(Scores, copy.Scores, 4);
            Array.Copy(Resigned, copy.Resigned, 4);
            Array.Copy(OutOrder, copy.OutOrder, 4);
            return copy;
        }

        /// <summary>
        /// Undo: takes the claims, scores and outs back to <paramref name="before"/> (a <see cref="Clone"/>
        /// taken before an action). Resignations are not actions, so they stay.
        /// </summary>
        public void RestoreActionState(ThreeKingdomsState before)
        {
            Array.Copy(before.Teams, Teams, 4);
            Array.Copy(before.Scores, Scores, 4);
            Array.Copy(before.OutOrder, OutOrder, 4);
        }
    }
}
