/* ----- ----- ----- ----- */
// SavedGamePgn.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pgn;

namespace Chinese_Chess_v3.Game.Core.Saves
{
    /// <summary>
    /// The saved-game PGN format (棋譜存檔, docs/PLAN.md notation phase 2): written by
    /// <see cref="Write"/>, read by <see cref="Parse"/> with the shared <see cref="PgnReader"/>
    /// rules (any <c>*.pgn</c> file name).
    /// <para>
    /// Tags, in this order: <c>Game</c> (<c>Chinese Chess</c>), <c>Event</c> (mode:
    /// <c>對局</c>/<c>殘局</c>/<c>開局</c>), <c>Date</c> (<c>yyyy.MM.dd</c>), <c>Red</c>, <c>Black</c>
    /// (player names), <c>Result</c> (<c>1-0</c>/<c>0-1</c>/<c>*</c>), <c>Termination</c> (how an
    /// ended game ended: <see cref="GameOverReason"/> name; only when over), <c>FEN</c> (the
    /// game's start position - for an opening the position before its preset line),
    /// <c>PresetPlies</c> (leading moves that are an opening's preset line; only when &gt; 0),
    /// <c>Origin</c> (the endgame puzzle / opening: <c>0001-七星聚會</c>, or just the title when
    /// it has no Id; only for those modes), <c>BoardType</c> (<c>Full</c>), <c>Format</c>
    /// (<c>ICCS</c>). Movetext: every move in ICCS with move numbers, the Chinese notation as a
    /// <c>{comment}</c> after each move.
    /// </para>
    /// <para>
    /// Reading requires <c>FEN</c>; every other tag is optional (missing <c>Event</c> = normal
    /// game). Unknown tags are kept in <see cref="PgnGameFile.Tags"/>.
    /// </para>
    /// </summary>
    public static class SavedGamePgn
    {
        /// <summary>The <c>[Game]</c> tag value.</summary>
        public const string GameTagValue = "Chinese Chess";

        private static readonly Regex OriginPattern = new(@"^(?<id>\d{4})-(?<title>.+)$", RegexOptions.CultureInvariant);

        /// <summary>The <c>[Event]</c> tag value of <paramref name="mode"/>.</summary>
        public static string EventName(GameMode mode) => mode switch
        {
            GameMode.Endgame => "殘局",
            GameMode.Opening => "開局",
            _ => "對局",
        };

        /// <summary>The mode of an <c>[Event]</c> value (the names of <see cref="EventName"/> or the enum names); Normal for anything else.</summary>
        public static GameMode ParseEvent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GameMode.Normal;
            string v = value.Trim();
            foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
            {
                if (v == EventName(mode) || string.Equals(v, mode.ToString(), StringComparison.OrdinalIgnoreCase))
                    return mode;
            }
            return GameMode.Normal;
        }

        /// <summary>
        /// The PGN text of <paramref name="game"/> as it stands (moves so far, result if over).
        /// </summary>
        /// <exception cref="InvalidOperationException">The game cannot be saved
        /// (<see cref="GameManager.CanSave"/>: only the Full board, with a known start position).</exception>
        public static string Write(GameManager game, string redName, string blackName, DateTime date)
        {
            ArgumentNullException.ThrowIfNull(game);
            if (!game.CanSave)
                throw new InvalidOperationException("Only a Full-board game with a known start position can be saved");

            string result = !game.IsGameOver ? "*" : (game.Winner == Players.PlayerSide.Player1 ? "1-0" : "0-1");
            string origin = game.Mode == GameMode.Normal || game.OriginTitle == null
                ? null
                : (game.OriginId != null ? $"{game.OriginId}-{game.OriginTitle}" : game.OriginTitle);

            var tags = new List<KeyValuePair<string, string>>
            {
                new("Game", GameTagValue),
                new("Event", EventName(game.Mode)),
                new("Date", date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)),
                new("Red", redName ?? string.Empty),
                new("Black", blackName ?? string.Empty),
                new("Result", result),
                new("Termination", game.IsGameOver ? game.Result?.Reason.ToString() : null),
                new("FEN", game.InitialFen),
                new("PresetPlies", game.UndoFloor > 0 ? game.UndoFloor.ToString(CultureInfo.InvariantCulture) : null),
                new("Origin", origin),
                new("BoardType", game.Board.Type.ToString()),
                new("Format", "ICCS"),
            };
            var moves = game.Moves.Select(m => new PgnMoveEntry(m.Iccs, m.Notation)).ToList();
            return PgnWriter.Write(tags, moves, game.FirstTurn, result);
        }

        /// <summary>
        /// Parses saved-game PGN <paramref name="text"/> of the file <paramref name="fileName"/>
        /// (any <c>*.pgn</c> name).
        /// </summary>
        /// <param name="folderCategory">The mode folder the file is in (the category).</param>
        /// <exception cref="FormatException">Missing/unparsable FEN, an unplayable position,
        /// a board type other than Full, a bad <c>[PresetPlies]</c>, a malformed tag line or an
        /// unreadable movetext token.</exception>
        public static SavedGame Parse(string text, string fileName, PgnOrigin origin, string folderCategory = "", string filePath = null)
        {
            var content = PgnReader.Read(text, fileName, origin, folderCategory, filePath, numberedFileName: false);
            string fen = content.Optional("FEN") ?? throw new FormatException("missing [FEN] tag");
            var sideToMove = PgnReader.ParsePosition(fen);

            var boardType = BoardType.Full;
            string boardTypeText = content.Optional("BoardType");
            if (boardTypeText != null && (!Enum.TryParse(boardTypeText, true, out boardType) || boardType != BoardType.Full))
                throw new FormatException($"[BoardType \"{boardTypeText}\"] is not supported (only Full)");

            int presetPlies = 0;
            string presetText = content.Optional("PresetPlies");
            if (presetText != null &&
                (!int.TryParse(presetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out presetPlies) || presetPlies < 0 || presetPlies > content.Moves.Count))
                throw new FormatException($"[PresetPlies \"{presetText}\"] is not 0-{content.Moves.Count}");

            string originId = null;
            string originTitle = content.Optional("Origin");
            if (originTitle != null)
            {
                var m = OriginPattern.Match(originTitle);
                if (m.Success)
                {
                    originId = m.Groups["id"].Value;
                    originTitle = m.Groups["title"].Value.Trim();
                }
            }

            GameOverReason? termination = null;
            string terminationText = content.Optional("Termination");
            if (terminationText != null && Enum.TryParse(terminationText, true, out GameOverReason reason))
                termination = reason;

            string result = content.Optional("Result");
            return new SavedGame(content, fen, sideToMove)
            {
                Mode = ParseEvent(content.Optional("Event")),
                OriginId = originId,
                OriginTitle = originTitle,
                PresetPlies = presetPlies,
                RedName = content.Optional("Red"),
                BlackName = content.Optional("Black"),
                Date = content.Optional("Date"),
                Result = result is "1-0" or "0-1" ? result : "*",
                Termination = termination,
                BoardType = boardType,
            };
        }
    }
}
