/* ----- ----- ----- ----- */
// OpeningLine.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Pgn;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Openings
{
    /// <summary>
    /// One opening (開局) for opening practice, as loaded from a PGN file (see
    /// docs/OPENINGS.md): the shared file data (<see cref="PgnGameFile"/>) plus the opening's
    /// own tags. <see cref="PgnGameFile.Moves"/> is the opening line that is played onto the
    /// board when the opening is started (<c>GameManager.StartOpening</c>); the player
    /// continues from the position after it, with
    /// <see cref="PgnGameFile.SideToMoveAfterMoves"/> to move.
    /// </summary>
    public sealed record OpeningLine : PgnGameFile
    {
        /// <summary>The <c>[Difficulty]</c> tag (optional), 1 to 5; null when the file has none.</summary>
        public int? Difficulty { get; init; }

        /// <summary>
        /// The <c>[ECCO]</c> tag (optional): the opening's code in the Encyclopedia of Chinese
        /// Chess Openings (中國象棋開局編號, <c>A00</c>-<c>E99</c>), upper case; null when the
        /// file has none.
        /// </summary>
        public string Ecco { get; init; }

        public OpeningLine() { }

        /// <summary>The shared fields from <paramref name="content"/>; the opening's own tags are set by the caller.</summary>
        internal OpeningLine(PgnFileContent content, string fen, PlayerSide sideToMove)
            : base(content, fen, sideToMove) { }
    }
}
