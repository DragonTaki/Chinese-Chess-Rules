/* ----- ----- ----- ----- */
// Rules.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/02
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Represents the rules configuration for different board types in Chinese Chess.
    /// Contains settings for Full board, Jieqi (揭棋), Half board, and HalfCross (三國) variants.
    /// </summary>
    public class Rules
    {
        /// <summary>
        /// A copy of these rules, so one game's rules can be changed (e.g. by a loaded saved
        /// game, <see cref="Saves.SavedGame.RulesFor"/>) without touching the rules it came
        /// from. Shallow: <see cref="PieceRankings"/> and <see cref="HalfCrossTeamSetup"/> are
        /// shared with the original (nothing changes them during play).
        /// </summary>
        public Rules Clone() => (Rules)MemberwiseClone();

        #region Timer Setting

        // Read by GameManager when it creates the two players' clocks (the defaults below
        // are what a game uses unless the player settings say otherwise).

        /// <summary>Each side's total time (局時); countdown only (count-up has no limit). Default: 30 minutes</summary>
        public TimeSpan TotalTimeLimit { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>Time allowed per move (步時), when <see cref="EnableStepTimer"/>; countdown only. Default: 5 minutes</summary>
        public TimeSpan StepTimeLimit { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Time added back to a side's total after each of its moves (每步加秒); only countdown clocks apply it, count-up is plain timing. Default: none</summary>
        public TimeSpan IncrementPerMove { get; set; } = TimeSpan.Zero;

        /// <summary>Whether the per-move limit (步時) applies (countdown only; a count-up clock always measures the step time, without a limit). Default: true</summary>
        public bool EnableStepTimer { get; set; } = true;

        /// <summary>Whether the clocks count down to the limits or up from zero (count-up only measures time: no limit, no time-up loss, no increment). Default: CountDown</summary>
        public TimerMode TimerMode { get; set; } = TimerMode.CountDown;

        /// <summary>Whether a side whose clock runs out loses (countdown only: a count-up clock never runs out). Default: true</summary>
        public bool EndGameWhenTimesUp { get; set; } = true;

        #endregion

        #region Full Board Rules (大盤規則設定)

        /// <summary>
        /// Whether the General can see the opposing General directly (王見王). Default: false
        /// </summary>
        public bool CanGeneralSeeGeneral { get; set; } = false;

        /// <summary>
        /// Whether the General can leave the palace (將帥出宮). Default: false
        /// </summary>
        public bool CanGeneralLeavePalace { get; set; } = false;

        /// <summary>
        /// Whether the Advisors can leave the palace (士出宮). Default: false
        /// </summary>
        public bool CanAdvisorLeavePalace { get; set; } = false;

        /// <summary>
        /// Whether the Elephant's eye can be blocked (塞象眼). Default: true
        /// </summary>
        public bool CanElephantEyeBlocked { get; set; } = true;

        /// <summary>
        /// Whether the Horse's leg can be hobbled (蹩馬腳). Default: true
        /// </summary>
        public bool CanHorseLegHobbled { get; set; } = true;

        /// <summary>
        /// Whether a piece can capture one of its own side's pieces (吃己棋). Full board: any own
        /// piece except the General. HalfCenter: any face-up own piece, General included, by the
        /// same rank rules as an enemy piece (a face-down one is still a 暗吃 that turns out to be
        /// one's own: the target is only revealed). Not used by 三國 (no capturing within a
        /// faction). Author decisions 2026-10-02. Default: false
        /// </summary>
        public bool CanCaptureOwnPiece { get; set; } = false;

        #endregion

        #region Half Board Rules (小盤規則設定)

        /// <summary>
        /// Whether the board uses hidden pieces (暗棋). Default: true
        /// </summary>
        public bool IsHiddenChess { get; set; } = true;

        /// <summary>
        /// Whether a piece can capture hidden pieces (暗吃). Default: false
        /// </summary>
        public bool CanCaptureHiddenPiece { get; set; } = false;

        /// <summary>
        /// What happens when a hidden-piece capture (暗吃) reveals a target stronger than the attacker
        /// (吃到比自己大的子). One toggle, two outcomes. true: the attacker dies and the revealed
        /// target stays. false: the attacker returns to its origin square alive and the target
        /// becomes revealed. Equal rank is a plain capture. Default: true
        /// </summary>
        public bool IsCaptureHiddenPieceStrongerSuicide { get; set; } = true;

        /// <summary>
        /// HalfCenter 自殺: whether a piece may move onto a face-up enemy piece it is too weak to
        /// capture by rank (a one-square orthogonal capture; rank-free captures never need it). The
        /// mover dies and the target stays (<c>MoveKind.Suicide</c>). Author decision 2026-10-02.
        /// Default: false
        /// </summary>
        public bool CanSuicide { get; set; } = false;

        /// <summary>
        /// Whether multiple captures in a row are allowed (連吃). Default: false
        /// </summary>
        public bool IsAllowChainCapture { get; set; } = false;

        /// <summary>
        /// The 車衝馬斜 variant (one switch, the two always go together). 車衝: Chariots move
        /// multiple grids along a straight line over empty squares; a capture after moving more
        /// than one square ignores rank, capturing the adjacent piece by a one-step move follows
        /// rank. 馬斜: Horses move one square diagonally instead of orthogonally, and such a
        /// capture ignores rank (takes any enemy piece). HalfCenter. Default: false
        /// </summary>
        public bool IsChariotRushHorseDiagonal { get; set; } = false;

        /// <summary>
        /// Whether Cannons must jump over one piece to capture (包跳吃子). Default: true
        /// </summary>
        public bool IsCannonMustJumpToCapture { get; set; } = true;

        #endregion

        #region Rank Settings (小盤棋子大小)

        /// <summary>
        /// Piece ranking order for Half Board (from strongest to weakest)
        /// </summary>
        public PieceType[] PieceRankings { get; set; } = new PieceType[]
        {
            PieceType.General,
            PieceType.Advisor,
            PieceType.Elephant,
            PieceType.Chariot,
            PieceType.Horse,
            PieceType.Cannon,
            PieceType.Soldier,
        };

        #endregion

        #region HalfCross Board Rule Options (三國半盤規則選項)

        /// <summary>
        /// 分隊: which of the two team splits a 三國半盤 game uses.
        /// 未實作: a per-kind rule option (settings.ini <c>[rules.three_kingdoms]</c>) that no
        /// gameplay reads yet. Default: <see cref="HalfCrossTeamVariant.Standard"/>
        /// </summary>
        public HalfCrossTeamVariant HalfCrossTeamVariant { get; set; } = HalfCrossTeamVariant.Standard;

        /// <summary>
        /// 勝負方式: how a 三國半盤 game is won. 未實作: a
        /// per-kind rule option that no gameplay reads yet. Default: <see cref="HalfCrossWinCondition.Points"/> (the author's scoring)
        /// </summary>
        public HalfCrossWinCondition HalfCrossWinCondition { get; set; } = HalfCrossWinCondition.Points;

        #endregion

        #region HalfCross Board Team Setup (三國半盤隊伍)

        /// <summary>
        /// Piece composition for each of HalfCross's three independently
        /// hostile factions (confirmed by the author — not an alliance by
        /// color). <c>side</c> is the actual owning faction; <c>color</c> is
        /// only the visual color (a physical set only has two, so faction 3
        /// necessarily reuses both — see <see cref="Players.PlayerSide.Player3"/>).
        /// Key: arbitrary faction number, Value: that faction's pieces.
        /// </summary>
        public Dictionary<int, List<(PieceType type, int count, PieceColor color, PlayerSide side)>> HalfCrossTeamSetup { get; set; }
            = new Dictionary<int, List<(PieceType, int, PieceColor, PlayerSide)>>()
        {
            // Faction 1 (Player1, red pieces)
            [1] = new List<(PieceType, int, PieceColor, PlayerSide)>()
            {
                (PieceType.Advisor,  2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Elephant, 2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Chariot,  2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Horse,    2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Cannon,   2, PieceColor.Red, PlayerSide.Player1),
            },

            // Faction 2 (Player2, black pieces)
            [2] = new List<(PieceType, int, PieceColor, PlayerSide)>()
            {
                (PieceType.Advisor,  2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Elephant, 2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Chariot,  2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Horse,    2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Cannon,   2, PieceColor.Black, PlayerSide.Player2),
            },

            // Faction 3: the Generals' side (將帥方) — its own independent faction (PlayerSide.Player3),
            // even though half its pieces are colored to look like the
            // other two factions' pieces (see the field doc above).
            [3] = new List<(PieceType, int, PieceColor, PlayerSide)>()
            {
                (PieceType.General, 1, PieceColor.Red,   PlayerSide.Player3),
                (PieceType.General, 1, PieceColor.Black, PlayerSide.Player3),
                (PieceType.Soldier, 5, PieceColor.Red,   PlayerSide.Player3),
                (PieceType.Soldier, 5, PieceColor.Black, PlayerSide.Player3),
            },
        };

        #endregion
    }
}
