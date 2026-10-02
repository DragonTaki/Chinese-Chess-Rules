/* ----- ----- ----- ----- */
// OpeningPgn.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Text.RegularExpressions;

using Chinese_Chess_v3.Game.Core.Pgn;

namespace Chinese_Chess_v3.Game.Core.Openings
{
    /// <summary>
    /// Reads one opening from Chinese-chess PGN text (format: docs/OPENINGS.md; the file
    /// name, tag and movetext rules are the shared <see cref="PgnReader"/> ones, as for
    /// endgames).
    /// <para>
    /// Opening tags, all optional: <c>FEN</c> (default: the standard start position),
    /// <c>Title</c>, <c>Category</c>, <c>Difficulty</c> (1-5), <c>ECCO</c> (<c>A00</c>-<c>E99</c>),
    /// <c>Description</c>, <c>Source</c>; any other tag is kept in
    /// <see cref="PgnGameFile.Tags"/>. The movetext is the opening line. A file needs a line
    /// or a <c>[FEN]</c> - without both it would only be the normal start position.
    /// </para>
    /// </summary>
    public static class OpeningPgn
    {
        private static readonly Regex EccoPattern = new(@"^[A-E][0-9]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses PGN <paramref name="text"/> of the file <paramref name="fileName"/>.
        /// </summary>
        /// <param name="folderCategory">The category from the file's folder, used when the file has no <c>[Category]</c> tag.</param>
        /// <exception cref="FormatException">Bad file name, an invalid tag, unparsable FEN,
        /// an unplayable position, an unreadable movetext token, or neither a line nor a FEN.</exception>
        public static OpeningLine Parse(string text, string fileName, PgnOrigin origin, string folderCategory = "", string filePath = null)
        {
            var content = PgnReader.Read(text, fileName, origin, folderCategory, filePath);

            string fenTag = content.Optional("FEN");
            if (fenTag == null && content.Moves.Count == 0)
                throw new FormatException("no opening line and no [FEN] tag");
            string fen = fenTag ?? XiangqiFen.StartPosition;
            var firstColor = PgnReader.ParsePosition(fen);

            string ecco = content.Optional("ECCO");
            if (ecco != null && !EccoPattern.IsMatch(ecco))
                throw new FormatException($"[ECCO \"{ecco}\"] is not a code A00-E99");

            var line = new OpeningLine(content, fen, firstColor)
            {
                Difficulty = PgnReader.ParseDifficulty(content),
                Ecco = ecco?.ToUpperInvariant(),
            };
            return line with { PlayerSide = PgnReader.ParsePlayerSide(content) ?? line.SideToMoveAfterMoves };
        }
    }
}
