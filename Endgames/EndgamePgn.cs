/* ----- ----- ----- ----- */
// EndgamePgn.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/01
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Globalization;

using Chinese_Chess_v3.Game.Core.Pgn;

namespace Chinese_Chess_v3.Game.Core.Endgames
{
    /// <summary>
    /// Reads one endgame puzzle from Chinese-chess PGN text (the file name, tag and movetext
    /// rules shared with the other kinds are <see cref="PgnReader"/>).
    /// <para>
    /// Puzzle tags: <c>FEN</c> and <c>Difficulty</c> (1-5) are required; <c>Title</c>,
    /// <c>Goal</c>, <c>Category</c>, <c>MoveLimit</c>, <c>Description</c>, <c>Source</c>
    /// optional; any other tag is kept in <see cref="PgnGameFile.Tags"/>. The movetext is the
    /// main-line solution.
    /// </para>
    /// </summary>
    public static class EndgamePgn
    {
        /// <summary>
        /// Whether <paramref name="fileName"/> follows the naming rule; returns its Id
        /// (4 digits) and name part. Same as <see cref="PgnReader.TryParseFileName"/>.
        /// </summary>
        public static bool TryParseFileName(string fileName, out string id, out string name) =>
            PgnReader.TryParseFileName(fileName, out id, out name);

        /// <summary>
        /// Parses PGN <paramref name="text"/> of the file <paramref name="fileName"/>.
        /// </summary>
        /// <param name="folderCategory">The category from the file's folder, used when the file has no <c>[Category]</c> tag.</param>
        /// <exception cref="FormatException">Bad file name, missing/invalid required tag,
        /// unparsable FEN, an unplayable position, or an unreadable movetext token.</exception>
        public static EndgamePuzzle Parse(string text, string fileName, PgnOrigin origin, string folderCategory = "", string filePath = null)
        {
            var content = PgnReader.Read(text, fileName, origin, folderCategory, filePath);

            string fen = content.Optional("FEN") ?? throw new FormatException("missing [FEN] tag");
            var firstColor = PgnReader.ParsePosition(fen);

            int difficulty = PgnReader.ParseDifficulty(content) ?? throw new FormatException("missing [Difficulty] tag");

            int? moveLimit = null;
            string moveLimitText = content.Optional("MoveLimit");
            if (moveLimitText != null)
            {
                if (!int.TryParse(moveLimitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit < 1)
                    throw new FormatException($"[MoveLimit \"{moveLimitText}\"] is not a positive number");
                moveLimit = limit;
            }

            return new EndgamePuzzle(content, fen, firstColor)
            {
                Difficulty = difficulty,
                Goal = content.Optional("Goal") ?? string.Empty,
                MoveLimit = moveLimit,
            };
        }

        /// <summary>
        /// Plays <paramref name="puzzle"/>'s solution from its position on a scratch board
        /// (<see cref="PgnReader.CheckMoves"/>). Returns null when it plays through,
        /// otherwise the reason.
        /// </summary>
        public static string CheckSolution(EndgamePuzzle puzzle) =>
            PgnReader.CheckMoves(puzzle.Fen, puzzle.Solution);
    }
}
