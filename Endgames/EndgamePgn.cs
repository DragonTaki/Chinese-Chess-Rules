/* ----- ----- ----- ----- */
// EndgamePgn.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Endgames
{
    /// <summary>
    /// Reads one endgame puzzle from Chinese-chess PGN text (format: docs/ENDGAMES.md).
    /// <para>
    /// Tag pairs (<c>[Name "value"]</c>, names case-insensitive): <c>FEN</c> and
    /// <c>Difficulty</c> (1-5) are required; <c>Title</c>, <c>Goal</c>, <c>Category</c>,
    /// <c>MoveLimit</c>, <c>Description</c>, <c>Source</c> optional; any other tag is kept in
    /// <see cref="EndgamePuzzle.Tags"/>. Movetext: the main-line solution in ICCS
    /// (<c>h2e2</c> / <c>H2-E2</c>); move numbers (<c>1.</c>, <c>1...</c>), results
    /// (<c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c>, <c>*</c>), NAGs (<c>$1</c>), move suffixes
    /// (<c>!</c>, <c>?</c>), <c>{comments}</c>, <c>;</c> line comments and <c>(variations)</c>
    /// (nested too) are skipped.
    /// </para>
    /// </summary>
    public static class EndgamePgn
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
        /// Parses PGN <paramref name="text"/> of the file <paramref name="fileName"/>.
        /// </summary>
        /// <param name="folderCategory">The category from the file's folder, used when the file has no <c>[Category]</c> tag.</param>
        /// <exception cref="FormatException">Bad file name, missing/invalid required tag,
        /// unparsable FEN, an unplayable position, or an unreadable movetext token.</exception>
        public static EndgamePuzzle Parse(string text, string fileName, EndgameOrigin origin, string folderCategory = "", string filePath = null)
        {
            if (!TryParseFileName(fileName, out var id, out var nameFromFile))
                throw new FormatException($"file name '{fileName}' is not '<4 digits>-<name>.pgn'");

            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string movetext = SplitTags(text ?? string.Empty, tags);

            if (!tags.TryGetValue("FEN", out var fen) || string.IsNullOrWhiteSpace(fen))
                throw new FormatException("missing [FEN] tag");
            var (pieces, sideToMove) = XiangqiFen.Parse(fen);
            string positionError = XiangqiFen.ValidatePosition(pieces, sideToMove);
            if (positionError != null)
                throw new FormatException($"FEN is not a playable position: {positionError}");

            if (!tags.TryGetValue("Difficulty", out var difficultyText))
                throw new FormatException("missing [Difficulty] tag");
            if (!int.TryParse(difficultyText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int difficulty) || difficulty < 1 || difficulty > 5)
                throw new FormatException($"[Difficulty \"{difficultyText}\"] is not 1-5");

            int? moveLimit = null;
            if (tags.TryGetValue("MoveLimit", out var moveLimitText) && !string.IsNullOrWhiteSpace(moveLimitText))
            {
                if (!int.TryParse(moveLimitText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int limit) || limit < 1)
                    throw new FormatException($"[MoveLimit \"{moveLimitText}\"] is not a positive number");
                moveLimit = limit;
            }

            string category = Optional(tags, "Category") ?? (folderCategory ?? string.Empty);

            return new EndgamePuzzle
            {
                Id = id,
                FileName = fileName,
                FilePath = filePath,
                Origin = origin,
                Category = category,
                Title = Optional(tags, "Title") ?? nameFromFile,
                Difficulty = difficulty,
                Goal = Optional(tags, "Goal") ?? string.Empty,
                Fen = fen.Trim(),
                SideToMove = sideToMove,
                MoveLimit = moveLimit,
                Solution = ParseMovetext(movetext),
                Description = Optional(tags, "Description"),
                Source = Optional(tags, "Source"),
                Tags = tags,
            };
        }

        /// <summary>
        /// Plays <paramref name="puzzle"/>'s solution from its position on a scratch board
        /// (no <see cref="GameManager"/>): every move must be a legal move of the side to move
        /// and no move may follow the end of the game (a side without legal moves). Returns
        /// null when it plays through, otherwise the reason.
        /// </summary>
        public static string CheckSolution(EndgamePuzzle puzzle)
        {
            var (pieces, side) = XiangqiFen.Parse(puzzle.Fen);
            var board = new Board(BoardType.Full);
            board.Initialize(pieces);

            for (int i = 0; i < puzzle.Solution.Count; i++)
            {
                var move = puzzle.Solution[i];
                string where = $"solution move {i + 1} ({move})";
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
            using var reader = new StringReader(text.TrimStart('﻿'));
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

        private static string Optional(Dictionary<string, string> tags, string name) =>
            tags.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

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
