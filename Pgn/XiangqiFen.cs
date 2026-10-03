/* ----- ----- ----- ----- */
// XiangqiFen.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/02
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pgn
{
    /// <summary>
    /// Standard xiangqi FEN for the Full (9x10) board, e.g. the start position
    /// <c>rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w</c>.
    /// <para>
    /// Ranks are listed from Black's back rank to Red's back rank, each from Red's left
    /// (file a) to Red's right (file i); digits are runs of empty points. Upper case is Red
    /// (<see cref="PieceColor.Red"/>), lower case Black (<see cref="PieceColor.Black"/>).
    /// Letters: K general, A advisor, B elephant (E accepted), N horse (H accepted),
    /// R chariot, C cannon, P soldier. The next field is the colour to move: <c>w</c> or
    /// <c>r</c> = Red, <c>b</c> = Black (missing = Red); later fields (castling, move
    /// counters) are ignored.
    /// </para>
    /// <para>
    /// Players are numbered by turn order: the colour to move is
    /// <see cref="PlayerSide.Player1"/>'s, the other one <see cref="PlayerSide.Player2"/>'s, so
    /// a parsed position always has Player1 to move and its pieces are owned accordingly
    /// (<see cref="PieceColors.AssignOwners"/>). Writing uses only the pieces' colours.
    /// </para>
    /// <para>
    /// The i-th rank maps to board row <c>y = i</c> and the j-th point to column <c>x = j</c>,
    /// so the start FEN reproduces <see cref="PieceConstants.InitialClassicPieces"/> exactly
    /// (Black's back rank is y = 0). Coordinates are absolute - not tied to which side the
    /// UI draws at the bottom.
    /// </para>
    /// </summary>
    public static class XiangqiFen
    {
        /// <summary>The standard start position.</summary>
        public const string StartPosition = "rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR w";

        private const int Columns = 9;
        private const int Rows = 10;

        /// <summary>
        /// Parses a FEN into face-up pieces and the colour to move (<c>FirstColor</c>). The
        /// pieces of <c>FirstColor</c> are owned by <see cref="PlayerSide.Player1"/> (who moves
        /// first), the others by <see cref="PlayerSide.Player2"/>. Syntax only (piece letters,
        /// 10 ranks of 9 points); whether the position is playable is checked by
        /// <see cref="ValidatePosition"/>.
        /// </summary>
        /// <exception cref="FormatException">The text is not a valid xiangqi FEN.</exception>
        public static (List<PieceInfo> Pieces, PieceColor FirstColor) Parse(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new FormatException("FEN is empty");

            var fields = fen.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var ranks = fields[0].Split('/');
            if (ranks.Length != Rows)
                throw new FormatException($"FEN has {ranks.Length} ranks, expected {Rows}");

            var firstColor = PieceColor.Red;
            if (fields.Length > 1)
            {
                firstColor = fields[1].ToLowerInvariant() switch
                {
                    "w" or "r" => PieceColor.Red,
                    "b" => PieceColor.Black,
                    _ => throw new FormatException($"FEN side to move '{fields[1]}' is not w/r/b"),
                };
            }

            var pieces = new List<PieceInfo>();
            for (int y = 0; y < Rows; y++)
            {
                int x = 0;
                foreach (char c in ranks[y])
                {
                    if (c >= '1' && c <= '9')
                    {
                        x += c - '0';
                        continue;
                    }
                    if (!TryGetPieceType(c, out var type))
                        throw new FormatException($"FEN rank {y + 1}: unknown piece letter '{c}'");
                    if (x >= Columns)
                        throw new FormatException($"FEN rank {y + 1} is longer than {Columns} points");

                    var color = char.IsUpper(c) ? PieceColor.Red : PieceColor.Black;
                    var side = color == firstColor ? PlayerSide.Player1 : PlayerSide.Player2;
                    pieces.Add(new PieceInfo(type, x, y, color, side, isFaceUp: true, isDead: false, turnIndex: 0));
                    x++;
                }
                if (x != Columns)
                    throw new FormatException($"FEN rank {y + 1} has {x} points, expected {Columns}");
            }

            return (pieces, firstColor);
        }

        /// <summary>
        /// The FEN of <paramref name="pieces"/> (living Red/Black pieces on the 9x10 board, by
        /// their <see cref="PieceInfo.Color"/>; owners are not written) with
        /// <paramref name="colorToMove"/> (<c>w</c> for Red, <c>b</c> for Black).
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="colorToMove"/> is not Red/Black,
        /// or a piece is off the board, not Red/Black, of an unsupported type, or two pieces
        /// share a point.</exception>
        public static string Format(IEnumerable<PieceInfo> pieces, PieceColor colorToMove)
        {
            if (colorToMove != PieceColor.Red && colorToMove != PieceColor.Black)
                throw new ArgumentException($"The colour to move must be Red or Black, not {colorToMove}", nameof(colorToMove));

            var grid = new char[Columns, Rows];
            foreach (var p in pieces.Where(p => p != null && !p.IsDead))
            {
                if (p.X < 0 || p.X >= Columns || p.Y < 0 || p.Y >= Rows)
                    throw new ArgumentException($"{p.Type} at ({p.X},{p.Y}) is off the 9x10 board", nameof(pieces));
                if (p.Color != PieceColor.Red && p.Color != PieceColor.Black)
                    throw new ArgumentException($"{p.Type} at ({p.X},{p.Y}) is {p.Color}; FEN only has Red and Black", nameof(pieces));
                if (grid[p.X, p.Y] != '\0')
                    throw new ArgumentException($"Two pieces on ({p.X},{p.Y})", nameof(pieces));

                char letter = GetLetter(p.Type);
                grid[p.X, p.Y] = p.Color == PieceColor.Red ? letter : char.ToLowerInvariant(letter);
            }

            var sb = new StringBuilder();
            for (int y = 0; y < Rows; y++)
            {
                if (y > 0)
                    sb.Append('/');
                int empty = 0;
                for (int x = 0; x < Columns; x++)
                {
                    char c = grid[x, y];
                    if (c == '\0')
                    {
                        empty++;
                        continue;
                    }
                    if (empty > 0)
                        sb.Append((char)('0' + empty));
                    empty = 0;
                    sb.Append(c);
                }
                if (empty > 0)
                    sb.Append((char)('0' + empty));
            }
            sb.Append(colorToMove == PieceColor.Red ? " w" : " b");
            return sb.ToString();
        }

        /// <summary>The FEN of a board's current pieces with <paramref name="colorToMove"/> (e.g. <c>GameManager.ColorOf(CurrentTurn)</c>).</summary>
        public static string Format(Board board, PieceColor colorToMove) =>
            Format(board.GetAllPieces().Select(p => p.CurrentInfo), colorToMove);

        /// <summary>
        /// Whether <paramref name="pieces"/> (as <see cref="Parse"/> returns them: owned by
        /// Player1 / Player2) is a playable Full-board position with Player1 to move: exactly
        /// one General per side, each inside its own colour's palace; the Generals not facing
        /// each other; Player2 (who just moved) not in check. Returns null when playable,
        /// otherwise the reason (English, for the log). Piece placement beyond that (e.g. an
        /// elephant off its points) is not checked.
        /// </summary>
        public static string ValidatePosition(List<PieceInfo> pieces)
        {
            var board = new Board(BoardType.Full);
            board.Initialize(pieces);

            foreach (var side in new[] { PlayerSide.Player1, PlayerSide.Player2 })
            {
                int count = pieces.Count(p => p.Type == PieceType.General && p.Side == side);
                if (count != 1)
                    return $"{side} has {count} Generals, expected 1";
                var general = board.GetGeneral(side);
                if (!board.IsInPalace(general.Color, general.X, general.Y))
                    return $"{side}'s General at ({general.X},{general.Y}) is outside its palace";
            }

            if (board.AreGeneralsFacing())
                return "the Generals face each other";

            if (board.IsSideInCheck(PlayerSide.Player2))
                return $"{PlayerSide.Player2} (who just moved) is in check but it is {PlayerSide.Player1}'s move";

            return null;
        }

        private static bool TryGetPieceType(char c, out PieceType type)
        {
            type = char.ToUpperInvariant(c) switch
            {
                'K' => PieceType.General,
                'A' => PieceType.Advisor,
                'B' or 'E' => PieceType.Elephant,
                'N' or 'H' => PieceType.Horse,
                'R' => PieceType.Chariot,
                'C' => PieceType.Cannon,
                'P' => PieceType.Soldier,
                _ => PieceType.None,
            };
            return type != PieceType.None;
        }

        private static char GetLetter(PieceType type) => type switch
        {
            PieceType.General => 'K',
            PieceType.Advisor => 'A',
            PieceType.Elephant => 'B',
            PieceType.Horse => 'N',
            PieceType.Chariot => 'R',
            PieceType.Cannon => 'C',
            PieceType.Soldier => 'P',
            _ => throw new ArgumentException($"{type} has no FEN letter"),
        };
    }
}
