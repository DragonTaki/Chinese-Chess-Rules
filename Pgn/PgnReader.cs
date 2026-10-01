/* ----- ----- ----- ----- */
// PgnReader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pgn
{
    /// <summary>
    /// A PGN file read into its parts, before a kind (<c>EndgamePgn</c>, <c>OpeningPgn</c>)
    /// checks its own tags: the file name's Id and name, every tag pair, and the main-line
    /// moves.
    /// </summary>
    public sealed class PgnFileContent
    {
        public string Id { get; init; }
        public string NameFromFile { get; init; }
        public string FileName { get; init; }
        public string FilePath { get; init; }
        public PgnOrigin Origin { get; init; }

        /// <summary>The category from the file's folder (used when there is no <c>[Category]</c> tag).</summary>
        public string FolderCategory { get; init; } = string.Empty;

        /// <summary>Tag pairs; names case-insensitive, the first occurrence of a name wins.</summary>
        public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The main-line ICCS moves of the movetext.</summary>
        public List<IccsMove> Moves { get; init; } = new();

        /// <summary>A tag's trimmed value; null when missing or blank.</summary>
        public string Optional(string name) =>
            Tags.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
    }

    /// <summary>
    /// The Chinese-chess PGN format shared by every kind of game file (format:
    /// docs/ENDGAMES.md section 3): the file naming rule (<c>0001-名稱.pgn</c>), tag pairs
    /// (<c>[Name "value"]</c>, names case-insensitive, <c>\"</c> and <c>\\</c> escapes) and the
    /// movetext - the main line in ICCS (<c>h2e2</c> / <c>H2-E2</c>); move numbers
    /// (<c>1.</c>, <c>1...</c>), results (<c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c>, <c>*</c>),
    /// NAGs (<c>$1</c>), move suffixes (<c>!</c>, <c>?</c>), <c>{comments}</c>, <c>;</c> line
    /// comments and <c>(variations)</c> (nested too) are skipped. Which tags are required is
    /// up to each kind.
    /// </summary>
    public static class PgnReader
    {
        /// <summary>File names must look like <c>0001-七星聚會.pgn</c>.</summary>
        private static readonly Regex FileNamePattern = new(@"^(?<id>\d{4})-(?<name>.+)\.pgn$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex TagPattern = new(@"^\[\s*(?<name>[A-Za-z0-9_]+)\s+""(?<value>(?:[^""\\]|\\.)*)""\s*\]$", RegexOptions.CultureInvariant);

        private static readonly Regex MoveNumberPrefix = new(@"^\d+\.+", RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether <paramref name="fileName"/> follows the naming rule; returns its Id
        /// (4 digits) and name part.
        /// </summary>
        public static bool TryParseFileName(string fileName, out string id, out string name)
        {
            var m = FileNamePattern.Match(fileName ?? string.Empty);
            id = m.Success ? m.Groups["id"].Value : null;
            name = m.Success ? m.Groups["name"].Value.Trim() : null;
            return m.Success && name.Length > 0;
        }

        /// <summary>
        /// Reads PGN <paramref name="text"/> of the file <paramref name="fileName"/>: the file
        /// name, the tag pairs and the main-line moves. No tag is required here.
        /// </summary>
        /// <param name="numberedFileName">Whether the file name must follow the
        /// <c>0001-名稱.pgn</c> rule (endgames, openings). Saved games are named differently
        /// (<c>SystemSettings</c>): with false any <c>*.pgn</c> name is accepted, <c>Id</c> is
        /// null and the name part is the file name without its extension.</param>
        /// <exception cref="FormatException">Bad file name, a malformed tag line or an
        /// unreadable movetext token.</exception>
        public static PgnFileContent Read(string text, string fileName, PgnOrigin origin, string folderCategory = "", string filePath = null, bool numberedFileName = true)
        {
            string id = null;
            string nameFromFile;
            if (numberedFileName)
            {
                if (!TryParseFileName(fileName, out id, out nameFromFile))
                    throw new FormatException($"file name '{fileName}' is not '<4 digits>-<name>.pgn'");
            }
            else
            {
                nameFromFile = Path.GetFileNameWithoutExtension(fileName ?? string.Empty).Trim();
                if (nameFromFile.Length == 0)
                    throw new FormatException($"file name '{fileName}' has no name");
            }

            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string movetext = SplitTags(text ?? string.Empty, tags);

            return new PgnFileContent
            {
                Id = id,
                NameFromFile = nameFromFile,
                FileName = fileName,
                FilePath = filePath,
                Origin = origin,
                FolderCategory = folderCategory ?? string.Empty,
                Tags = tags,
                Moves = ParseMovetext(movetext),
            };
        }

        /// <summary>
        /// Parses <paramref name="fen"/> and checks the position can be played
        /// (<see cref="XiangqiFen.ValidatePosition"/>); returns the side to move.
        /// </summary>
        /// <exception cref="FormatException">Unparsable FEN or an unplayable position.</exception>
        public static PlayerSide ParsePosition(string fen)
        {
            var (pieces, sideToMove) = XiangqiFen.Parse(fen);
            string positionError = XiangqiFen.ValidatePosition(pieces, sideToMove);
            if (positionError != null)
                throw new FormatException($"FEN is not a playable position: {positionError}");
            return sideToMove;
        }

        /// <summary>
        /// The <c>[Difficulty]</c> tag, 1 to 5; null when the tag is missing or blank.
        /// </summary>
        /// <exception cref="FormatException">The tag is present but not 1-5.</exception>
        public static int? ParseDifficulty(PgnFileContent content)
        {
            if (!content.Tags.TryGetValue("Difficulty", out var text) || string.IsNullOrWhiteSpace(text))
                return null;
            if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int difficulty) || difficulty < 1 || difficulty > 5)
                throw new FormatException($"[Difficulty \"{text}\"] is not 1-5");
            return difficulty;
        }

        /// <summary>
        /// Plays <paramref name="moves"/> from <paramref name="fen"/> on a scratch board (no
        /// <see cref="GameManager"/>): every move must be a legal move of the side to move and
        /// no move may follow the end of the game (a side without legal moves). Returns null
        /// when they play through, otherwise the reason.
        /// </summary>
        /// <param name="rules">The rules to play by; null for the default <see cref="Rules"/>.</param>
        public static string CheckMoves(string fen, IReadOnlyList<IccsMove> moves, Rules rules = null)
        {
            var (pieces, side) = XiangqiFen.Parse(fen);
            var board = new Board(BoardType.Full, rules);
            board.Initialize(pieces);

            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                string where = $"move {i + 1} ({move})";
                if (!board.HasAnyLegalMove(side))
                    return $"{where}: the game is already over";

                var piece = board.GetPiece(move.FromX, move.FromY);
                if (piece == null)
                    return $"{where}: no piece on {IccsMove.FormatSquare(move.FromX, move.FromY)}";
                if (piece.Side != side)
                    return $"{where}: the piece on {IccsMove.FormatSquare(move.FromX, move.FromY)} is not {side}'s";
                if (!piece.CanMoveTo(board, move.ToX, move.ToY))
                    return $"{where}: illegal move";

                // Same order as GameManager.ExecuteMove.
                board.AdvanceTurn();
                if (board.GetPiece(move.ToX, move.ToY) != null)
                    board.RemovePiece(move.ToX, move.ToY);
                board.MovePiece(move.FromX, move.FromY, move.ToX, move.ToY);
                side = side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
            }
            return null;
        }

        /// <summary>Reads the leading tag pairs into <paramref name="tags"/>; returns the rest (the movetext).</summary>
        private static string SplitTags(string text, Dictionary<string, string> tags)
        {
            using var reader = new StringReader(text.TrimStart('\uFEFF'));
            var movetext = new StringBuilder();
            bool inTags = true;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (inTags)
                {
                    if (trimmed.Length == 0)
                        continue;
                    if (trimmed.StartsWith('['))
                    {
                        var m = TagPattern.Match(trimmed);
                        if (!m.Success)
                            throw new FormatException($"bad tag line: {trimmed}");
                        // The first occurrence wins, as the rest of the file reads it.
                        tags.TryAdd(m.Groups["name"].Value, Unescape(m.Groups["value"].Value));
                        continue;
                    }
                    inTags = false;
                }
                movetext.AppendLine(line);
            }
            return movetext.ToString();
        }

        private static string Unescape(string value) =>
            value.Replace("\\\"", "\"").Replace("\\\\", "\\");

        /// <summary>The main-line ICCS moves of a movetext (comments, variations, numbers, results and NAGs removed).</summary>
        private static List<IccsMove> ParseMovetext(string movetext)
        {
            var mainLine = new StringBuilder();
            int variationDepth = 0;
            for (int i = 0; i < movetext.Length; i++)
            {
                char c = movetext[i];
                if (c == '{')
                {
                    int end = movetext.IndexOf('}', i + 1);
                    if (end < 0)
                        throw new FormatException("unclosed {comment}");
                    i = end;
                    mainLine.Append(' ');
                    continue;
                }
                if (c == ';')
                {
                    int end = movetext.IndexOf('\n', i + 1);
                    i = end < 0 ? movetext.Length : end;
                    mainLine.Append(' ');
                    continue;
                }
                if (c == '(')
                {
                    variationDepth++;
                    continue;
                }
                if (c == ')')
                {
                    if (variationDepth == 0)
                        throw new FormatException("')' without '('");
                    variationDepth--;
                    mainLine.Append(' ');
                    continue;
                }
                if (variationDepth == 0)
                    mainLine.Append(c);
            }
            if (variationDepth != 0)
                throw new FormatException("unclosed (variation)");

            var moves = new List<IccsMove>();
            foreach (var rawToken in mainLine.ToString().Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = MoveNumberPrefix.Replace(rawToken, string.Empty).TrimEnd('!', '?');
                if (token.Length == 0 || token.StartsWith('$') || IsResult(token))
                    continue;
                if (!IccsMove.TryParse(token, out var move))
                    throw new FormatException($"movetext token '{rawToken}' is not an ICCS move");
                moves.Add(move);
            }
            return moves;
        }

        private static bool IsResult(string token) =>
            token is "1-0" or "0-1" or "1/2-1/2" or "½-½" or "*";
    }
}
