/* ----- ----- ----- ----- */
// EndgamePuzzle.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Endgames
{
    /// <summary>Where an endgame puzzle file was loaded from.</summary>
    public enum EndgameOrigin
    {
        /// <summary>Shipped with the game (the built-in <c>Assets/Endgames</c> folder).</summary>
        BuiltIn,

        /// <summary>The player's own endgame folder (per-user app data).</summary>
        User,
    }

    /// <summary>
    /// One endgame puzzle (殘局) as loaded from a PGN file (see docs/ENDGAMES.md). Plain,
    /// immutable data: the original file content only - player results and best scores are
    /// kept elsewhere. A record so a changed copy can be made with <c>with</c>.
    /// </summary>
    public sealed record EndgamePuzzle
    {
        /// <summary>The 4-digit number of the file name (<c>0001</c> of <c>0001-七星聚會.pgn</c>).</summary>
        public string Id { get; init; }

        /// <summary>The file name (<c>0001-七星聚會.pgn</c>); puzzles are sorted by it (ordinal).</summary>
        public string FileName { get; init; }

        /// <summary>Full path of the file it was loaded from.</summary>
        public string FilePath { get; init; }

        /// <summary>Built-in or the player's own folder.</summary>
        public EndgameOrigin Origin { get; init; }

        /// <summary>
        /// The category: the <c>[Category]</c> tag, else the name of the subfolder (directly
        /// under the loaded root) the file is in; empty when neither exists.
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>The <c>[Title]</c> tag, else the name part of the file name (<c>七星聚會</c>).</summary>
        public string Title { get; init; }

        /// <summary>The <c>[Difficulty]</c> tag, 1 (easiest) to 5.</summary>
        public int Difficulty { get; init; }

        /// <summary>The <c>[Goal]</c> tag as written (e.g. <c>紅先勝</c>, <c>紅先和</c>); empty when missing.</summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>The <c>[FEN]</c> tag: the start position, including the side to move.</summary>
        public string Fen { get; init; }

        /// <summary>The side to move in <see cref="Fen"/> (Red = Player1, Black = Player2).</summary>
        public PlayerSide SideToMove { get; init; }

        /// <summary>The <c>[MoveLimit]</c> tag (optional). Loaded only; not enforced yet (see docs/PLAN.md).</summary>
        public int? MoveLimit { get; init; }

        /// <summary>
        /// The main-line solution from the movetext, both sides' moves in order starting with
        /// <see cref="SideToMove"/>; empty when the file has none. Loaded and checked for
        /// legality, not shown by the UI (see docs/PLAN.md).
        /// </summary>
        public IReadOnlyList<IccsMove> Solution { get; init; } = new List<IccsMove>();

        /// <summary>The <c>[Description]</c> tag (optional).</summary>
        public string Description { get; init; }

        /// <summary>The <c>[Source]</c> tag (optional): where the puzzle comes from (出處).</summary>
        public string Source { get; init; }

        /// <summary>Every tag pair of the file as written, including ones not mapped above.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

        public override string ToString() => $"{FileName} ({Origin}, {Category}): {Title}";
    }
}
