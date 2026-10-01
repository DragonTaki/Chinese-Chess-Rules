/* ----- ----- ----- ----- */
// IccsMove.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

namespace Chinese_Chess_v3.Game.Core.Pgn
{
    /// <summary>
    /// One move in ICCS coordinates (e.g. <c>h2e2</c>, also written <c>H2-E2</c>), mapped
    /// onto this codebase's absolute board coordinates.
    /// <para>
    /// ICCS: files <c>a</c>..<c>i</c> from Red's left to Red's right, ranks <c>0</c>..<c>9</c>
    /// from Red's back rank to Black's back rank. Board: <c>x</c> 0..8 (same direction as the
    /// files), <c>y</c> 0..9 from Black's back rank (the first FEN rank) to Red's back rank.
    /// So <c>x = file - 'a'</c> and <c>y = 9 - rank</c>. These are absolute coordinates;
    /// which side is drawn at the bottom is only a UI (view) decision.
    /// </para>
    /// </summary>
    public readonly record struct IccsMove(int FromX, int FromY, int ToX, int ToY)
    {
        /// <summary>Board columns / rows of the Full (9x10) board ICCS describes.</summary>
        public const int Files = 9;
        public const int Ranks = 10;

        /// <summary>
        /// Parses <paramref name="text"/> (<c>h2e2</c>, <c>H2-E2</c>; case-insensitive, an
        /// optional '-' between the squares). Returns false for anything else, including a
        /// move that does not move (same from/to square).
        /// </summary>
        public static bool TryParse(string text, out IccsMove move)
        {
            move = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string s = text.Trim().ToLowerInvariant();
            if (s.Length == 5 && s[2] == '-')
                s = s.Remove(2, 1);
            if (s.Length != 4)
                return false;

            if (!TryParseSquare(s[0], s[1], out int fx, out int fy) || !TryParseSquare(s[2], s[3], out int tx, out int ty))
                return false;
            if (fx == tx && fy == ty)
                return false;

            move = new IccsMove(fx, fy, tx, ty);
            return true;
        }

        /// <summary>As <see cref="TryParse"/>, throwing <see cref="FormatException"/> on bad input.</summary>
        public static IccsMove Parse(string text) =>
            TryParse(text, out var move) ? move : throw new FormatException($"Not an ICCS move: '{text}'");

        /// <summary>Board coordinates of an ICCS square (file letter, rank digit).</summary>
        public static bool TryParseSquare(char file, char rank, out int x, out int y)
        {
            file = char.ToLowerInvariant(file);
            x = file - 'a';
            y = Ranks - 1 - (rank - '0');
            bool ok = file >= 'a' && file < 'a' + Files && rank >= '0' && rank <= '9';
            if (!ok)
                x = y = 0;
            return ok;
        }

        /// <summary>The ICCS name of board square (x, y), e.g. (7, 7) -> "h2".</summary>
        public static string FormatSquare(int x, int y)
        {
            if (x < 0 || x >= Files || y < 0 || y >= Ranks)
                throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is not on the 9x10 board");
            return $"{(char)('a' + x)}{Ranks - 1 - y}";
        }

        /// <summary>Lower-case ICCS without a dash, e.g. <c>h2e2</c>.</summary>
        public override string ToString() => FormatSquare(FromX, FromY) + FormatSquare(ToX, ToY);
    }
}
