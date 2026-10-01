/* ----- ----- ----- ----- */
// SavedGame.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pgn;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Saves
{
    /// <summary>
    /// One saved game (棋譜存檔) as loaded from a PGN file written by
    /// <see cref="SavedGamePgn.Write"/>: the shared file data (<see cref="PgnGameFile"/>:
    /// <c>Fen</c> = the game's start position, <c>Moves</c> = every move of the game,
    /// <c>Category</c> = the mode folder it is in, <c>Title</c> = the file name without
    /// <c>.pgn</c>) plus the saved game's own tags. Loaded into a game by
    /// <see cref="GameManager.LoadSavedGame"/>.
    /// </summary>
    public sealed record SavedGame : PgnGameFile
    {
        /// <summary>The <c>[Event]</c> tag: what kind of game it was (<see cref="SavedGamePgn.EventName"/>).</summary>
        public GameMode Mode { get; init; } = GameMode.Normal;

        /// <summary>From the <c>[Origin]</c> tag: the 4-digit Id of the endgame puzzle / opening file the game was started from; null when none.</summary>
        public string OriginId { get; init; }

        /// <summary>From the <c>[Origin]</c> tag: the title of the endgame puzzle / opening the game was started from; null for a normal game.</summary>
        public string OriginTitle { get; init; }

        /// <summary>
        /// The <c>[PresetPlies]</c> tag: how many leading <see cref="PgnGameFile.Moves"/> were
        /// preset (an opening's line) and cannot be undone (<see cref="GameManager.UndoFloor"/>);
        /// 0 when missing.
        /// </summary>
        public int PresetPlies { get; init; }

        /// <summary>The <c>[Red]</c> tag (player name); null when missing.</summary>
        public string RedName { get; init; }

        /// <summary>The <c>[Black]</c> tag (player name); null when missing.</summary>
        public string BlackName { get; init; }

        /// <summary>The <c>[Date]</c> tag as written (<c>yyyy.MM.dd</c>); null when missing.</summary>
        public string Date { get; init; }

        /// <summary>The <c>[Result]</c> tag: <c>1-0</c>, <c>0-1</c> or <c>*</c> (game not over).</summary>
        public string Result { get; init; } = "*";

        /// <summary>The <c>[Termination]</c> tag: how an ended game ended; null for a game still in play.</summary>
        public GameOverReason? Termination { get; init; }

        /// <summary>The <c>[BoardType]</c> tag; only <see cref="Boards.BoardType.Full"/> games can be saved.</summary>
        public BoardType BoardType { get; init; } = BoardType.Full;

        /// <summary>The winner by <see cref="Result"/>: Player1 for <c>1-0</c>, Player2 for <c>0-1</c>, otherwise None.</summary>
        public PlayerSide Winner => Result switch
        {
            "1-0" => PlayerSide.Player1,
            "0-1" => PlayerSide.Player2,
            _ => PlayerSide.None,
        };

        // ----- Time control and clocks (null = tag missing: the current default is used) -----

        /// <summary>From <c>[TimeControl]</c> (<c>total+increment</c>, seconds): each side's total time (<see cref="Rules.TotalTimeLimit"/>).</summary>
        public TimeSpan? TotalTimeLimit { get; init; }

        /// <summary>From <c>[TimeControl]</c>: the time added back after each move (<see cref="Rules.IncrementPerMove"/>; only countdown clocks apply it).</summary>
        public TimeSpan? IncrementPerMove { get; init; }

        /// <summary>The <c>[StepTime]</c> tag (seconds): the per-move limit (<see cref="Rules.StepTimeLimit"/>).</summary>
        public TimeSpan? StepTimeLimit { get; init; }

        /// <summary>The <c>[StepTimer]</c> tag: whether the per-move limit applies (<see cref="Rules.EnableStepTimer"/>).</summary>
        public bool? EnableStepTimer { get; init; }

        /// <summary>The <c>[TimerMode]</c> tag (<c>CountDown</c>/<c>CountUp</c>, <see cref="Rules.TimerMode"/>).</summary>
        public Players.TimerMode? TimerMode { get; init; }

        /// <summary>The <c>[LoseOnTimeUp]</c> tag (<see cref="Rules.EndGameWhenTimesUp"/>).</summary>
        public bool? EndGameWhenTimesUp { get; init; }

        /// <summary>
        /// The <c>[RedTimeUsed]</c> / <c>[RedStepUsed]</c> tags (seconds): Red's (Player1's)
        /// elapsed total and current step time when saved; null when neither tag is there.
        /// </summary>
        public ClockState? RedClock { get; init; }

        /// <summary>The <c>[BlackTimeUsed]</c> / <c>[BlackStepUsed]</c> tags: Black's (Player2's) clock, like <see cref="RedClock"/>.</summary>
        public ClockState? BlackClock { get; init; }

        // ----- Full-board rules (null = tag missing: the current default is used) -----

        /// <summary>The <c>[GeneralCanSeeGeneral]</c> tag (<see cref="Rules.CanGeneralSeeGeneral"/>).</summary>
        public bool? CanGeneralSeeGeneral { get; init; }

        /// <summary>The <c>[GeneralCanLeavePalace]</c> tag (<see cref="Rules.CanGeneralLeavePalace"/>).</summary>
        public bool? CanGeneralLeavePalace { get; init; }

        /// <summary>The <c>[AdvisorCanLeavePalace]</c> tag (<see cref="Rules.CanAdvisorLeavePalace"/>).</summary>
        public bool? CanAdvisorLeavePalace { get; init; }

        /// <summary>The <c>[ElephantEyeBlocks]</c> tag (<see cref="Rules.CanElephantEyeBlockd"/>).</summary>
        public bool? CanElephantEyeBlockd { get; init; }

        /// <summary>The <c>[HorseLegBlocks]</c> tag (<see cref="Rules.CanHorseLegHobbled"/>).</summary>
        public bool? CanHorseLegHobbled { get; init; }

        /// <summary>
        /// The rules this game is played by when loaded: a copy of <paramref name="defaults"/>
        /// (the current settings' rules) with every time-control and rule value the file has
        /// put over it. A file without those tags (saved before they existed) plays by
        /// <paramref name="defaults"/> unchanged.
        /// </summary>
        public Rules RulesFor(Rules defaults)
        {
            var rules = (defaults ?? new Rules()).Clone();
            if (TotalTimeLimit is TimeSpan total) rules.TotalTimeLimit = total;
            if (IncrementPerMove is TimeSpan increment) rules.IncrementPerMove = increment;
            if (StepTimeLimit is TimeSpan step) rules.StepTimeLimit = step;
            if (EnableStepTimer is bool stepTimer) rules.EnableStepTimer = stepTimer;
            if (TimerMode is Players.TimerMode mode) rules.TimerMode = mode;
            if (EndGameWhenTimesUp is bool loseOnTimeUp) rules.EndGameWhenTimesUp = loseOnTimeUp;
            if (CanGeneralSeeGeneral is bool seeGeneral) rules.CanGeneralSeeGeneral = seeGeneral;
            if (CanGeneralLeavePalace is bool generalLeaves) rules.CanGeneralLeavePalace = generalLeaves;
            if (CanAdvisorLeavePalace is bool advisorLeaves) rules.CanAdvisorLeavePalace = advisorLeaves;
            if (CanElephantEyeBlockd is bool elephantEye) rules.CanElephantEyeBlockd = elephantEye;
            if (CanHorseLegHobbled is bool horseLeg) rules.CanHorseLegHobbled = horseLeg;
            return rules;
        }

        /// <summary>The moves are checked with the file's own rules over the default <see cref="Rules"/>.</summary>
        internal override Rules RulesForMoveCheck() => RulesFor(new Rules());

        public SavedGame() { }

        /// <summary>The shared fields from <paramref name="content"/>; the saved game's own tags are set by the caller.</summary>
        internal SavedGame(PgnFileContent content, string fen, PlayerSide sideToMove)
            : base(content, fen, sideToMove) { }
    }
}
