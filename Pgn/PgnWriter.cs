/* ----- ----- ----- ----- */
// PgnWriter.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;
using System.Text;

using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pgn
{
    /// <summary>One movetext entry for <see cref="PgnWriter"/>: the move (ICCS) and an optional comment.</summary>
    public readonly record struct PgnMoveEntry(string Move, string Comment);

    /// <summary>
    /// Writes Chinese-chess PGN text in the shared format <see cref="PgnReader"/> reads: tag
    /// pairs (<c>[Name "value"]</c>, <c>\</c> and <c>"</c> escaped), a blank line, then the
    /// movetext with one move number per line (<c>1. h2e2 {炮二平五} h9g7 {馬8進7}</c>; a game
    /// Black starts begins with <c>1... </c>), and the result token on its own line.
    /// </summary>
    public static class PgnWriter
    {
        /// <summary>
        /// The PGN text of <paramref name="tags"/> (in order; a null value is skipped) and
        /// <paramref name="moves"/>, the first one made by <paramref name="firstSide"/>,
        /// ending with <paramref name="result"/> (<c>1-0</c>, <c>0-1</c>, <c>1/2-1/2</c> or <c>*</c>).
        /// </summary>
        public static string Write(IEnumerable<KeyValuePair<string, string>> tags, IReadOnlyList<PgnMoveEntry> moves, PlayerSide firstSide, string result)
        {
            var sb = new StringBuilder();
            foreach (var (name, value) in tags)
            {
                if (value != null)
                    sb.Append('[').Append(name).Append(" \"").Append(EscapeTagValue(value)).Append("\"]\n");
            }
            sb.Append('\n');

            // Like the move numbers of MoveRecord: a Black-first game numbers Black's first move 1.
            int offset = firstSide == PlayerSide.Player2 ? 1 : 0;
            for (int i = 0; i < moves.Count; i++)
            {
                int slot = i + offset;  // even = Red's move, odd = Black's
                int number = slot / 2 + 1;
                if (slot % 2 == 0)
                {
                    if (i > 0)
                        sb.Append('\n');
                    sb.Append(number).Append(". ");
                }
                else if (i == 0)
                {
                    sb.Append(number).Append("... ");
                }
                else
                {
                    sb.Append(' ');
                }

                sb.Append(moves[i].Move);
                if (!string.IsNullOrEmpty(moves[i].Comment))
                    sb.Append(" {").Append(moves[i].Comment.Replace('{', '(').Replace('}', ')')).Append('}');
            }
            if (moves.Count > 0)
                sb.Append('\n');
            sb.Append(string.IsNullOrEmpty(result) ? "*" : result).Append('\n');
            return sb.ToString();
        }

        /// <summary>A tag value with <c>\</c> and <c>"</c> escaped (the inverse of the reader's unescaping); line breaks become spaces.</summary>
        public static string EscapeTagValue(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace('\r', ' ').Replace('\n', ' ');
    }
}
