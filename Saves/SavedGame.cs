/* ----- ----- ----- ----- */
// SavedGame.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

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

        public SavedGame() { }

        /// <summary>The shared fields from <paramref name="content"/>; the saved game's own tags are set by the caller.</summary>
        internal SavedGame(PgnFileContent content, string fen, PlayerSide sideToMove)
            : base(content, fen, sideToMove) { }
    }
}
