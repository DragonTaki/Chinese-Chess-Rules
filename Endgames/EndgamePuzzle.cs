/* ----- ----- ----- ----- */
// EndgamePuzzle.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/01
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pgn;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Endgames
{
    /// <summary>
    /// One endgame puzzle (殘局) as loaded from a PGN file (see docs/ENDGAMES.md): the shared
    /// file data (<see cref="PgnGameFile"/>: Id, file, origin, category, title, FEN, moves,
    /// description, source, tags) plus the puzzle's own tags. The moves are the puzzle's
    /// solution (<see cref="Solution"/>). Player results and best scores are kept elsewhere.
    /// </summary>
    public sealed record EndgamePuzzle : PgnGameFile
    {
        /// <summary>The <c>[Difficulty]</c> tag (required), 1 (easiest) to 5.</summary>
        public int Difficulty { get; init; }

        /// <summary>The <c>[Goal]</c> tag as written (e.g. <c>紅先勝</c>, <c>紅先和</c>); empty when missing.</summary>
        public string Goal { get; init; } = string.Empty;

        /// <summary>The <c>[MoveLimit]</c> tag (optional). Loaded only; not enforced yet (see docs/PLAN.md).</summary>
        public int? MoveLimit { get; init; }

        /// <summary>
        /// The main-line solution: the same list as <see cref="PgnGameFile.Moves"/> (both
        /// sides' moves in order starting with <see cref="PgnGameFile.SideToMove"/>; empty
        /// when the file has none). Loaded and checked for legality, not shown by the UI
        /// (see docs/PLAN.md).
        /// </summary>
        public IReadOnlyList<IccsMove> Solution
        {
            get => Moves;
            init => Moves = value;
        }

        public EndgamePuzzle() { }

        /// <summary>The shared fields from <paramref name="content"/>; the puzzle's own tags are set by the caller.</summary>
        internal EndgamePuzzle(PgnFileContent content, string fen, PlayerSide sideToMove)
            : base(content, fen, sideToMove) { }
    }
}
