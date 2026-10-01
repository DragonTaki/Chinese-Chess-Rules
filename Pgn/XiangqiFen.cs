/* ----- ----- ----- ----- */
// XiangqiFen.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/01
// Version: v1.0
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
    /// (<see cref="PlayerSide.Player1"/>, <see cref="PieceColor.Red"/>), lower case Black
    /// (<see cref="PlayerSide.Player2"/>, <see cref="PieceColor.Black"/>). Letters:
    /// K general, A advisor, B elephant (E accepted), N horse (H accepted), R chariot,
    /// C cannon, P soldier. The next field is the side to move: <c>w</c> or <c>r</c> = Red,
    /// <c>b</c> = Black (missing = Red); later fields (castling, move counters) are ignored.
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
        /// Parses a FEN into face-up pieces and the side to move. Syntax only (piece letters,
        /// 10 ranks of 9 points); whether the position is playable is checked by
        /// <see cref="ValidatePosition"/>.
        /// </summary>
        /// <exception cref="FormatException">The text is not a valid xiangqi FEN.</exception>
        public static (List<PieceInfo> Pieces, PlayerSide SideToMove) Parse(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen))
                throw new FormatException("FEN is empty");

            var fields = fen.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var ranks = fields[0].Split('/');
            if (ranks.Length != Rows)
                throw new FormatException($"FEN has {ranks.Length} ranks, expected {Rows}");

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

                    var side = char.IsUpper(c) ? PlayerSide.Player1 : PlayerSide.Player2;
                    var color = side == PlayerSide.Player1 ? PieceColor.Red : PieceColor.Black;
                    pieces.Add(new PieceInfo(type, x, y, color, side, isFaceUp: true, isDead: false, turnIndex: 0));
                    x++;
                }
                if (x != Columns)
                    throw new FormatException($"FEN rank {y + 1} has {x} points, expected {Columns}");
            }

            var sideToMove = PlayerSide.Player1;
            if (fields.Length > 1)
            {
                sideToMove = fields[1].ToLowerInvariant() switch
                {
                    "w" or "r" => PlayerSide.Player1,
                    "b" => PlayerSide.Player2,
                    _ => throw new FormatException($"FEN side to move '{fields[1]}' is not w/r/b"),
                };
            }

            return (pieces, sideToMove);
        }

        /// <summary>
        /// The FEN of <paramref name="pieces"/> (living Red/Black pieces on the 9x10 board)
        /// with <paramref name="sideToMove"/> (<c>w</c> for Red, <c>b</c> for Black).
        /// </summary>
        /// <exception cref="ArgumentException">A piece is off the board, not Red/Black, of an
        /// unsupported type, or two pieces share a point.</exception>
        public static string Format(IEnumerable<PieceInfo> pieces, PlayerSide sideToMove)
        {
            if (sideToMove != PlayerSide.Player1 && sideToMove != PlayerSide.Player2)
                throw new ArgumentException($"Side to move must be Player1 or Player2, not {sideToMove}", nameof(sideToMove));

            var grid = new char[Columns, Rows];
            foreach (var p in pieces.Where(p => p != null && !p.IsDead))
            {
                if (p.X < 0 || p.X >= Columns || p.Y < 0 || p.Y >= Rows)
                    throw new ArgumentException($"{p.Type} at ({p.X},{p.Y}) is off the 9x10 board", nameof(pieces));
                if (p.Side != PlayerSide.Player1 && p.Side != PlayerSide.Player2)
                    throw new ArgumentException($"{p.Type} at ({p.X},{p.Y}) belongs to {p.Side}; FEN only has Red and Black", nameof(pieces));
                if (grid[p.X, p.Y] != '\0')
                    throw new ArgumentException($"Two pieces on ({p.X},{p.Y})", nameof(pieces));

                char letter = GetLetter(p.Type);
                grid[p.X, p.Y] = p.Side == PlayerSide.Player1 ? letter : char.ToLowerInvariant(letter);
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
            sb.Append(sideToMove == PlayerSide.Player1 ? " w" : " b");
            return sb.ToString();
        }

        /// <summary>The FEN of a board's current pieces with <paramref name="sideToMove"/>.</summary>
        public static string Format(Board board, PlayerSide sideToMove) =>
            Format(board.GetAllPieces().Select(p => p.CurrentInfo), sideToMove);

        /// <summary>
        /// Whether <paramref name="pieces"/> is a playable Full-board position with
        /// <paramref name="sideToMove"/> to move: exactly one General per side, each inside
        /// its own palace; the Generals not facing each other; the side that just moved not
        /// in check. Returns null when playable, otherwise the reason (English, for the log).
        /// Piece placement beyond that (e.g. an elephant off its points) is not checked.
        /// </summary>
        public static string ValidatePosition(List<PieceInfo> pieces, PlayerSide sideToMove)
        {
            var board = new Board(BoardType.Full);
            board.Initialize(pieces);

            foreach (var side in new[] { PlayerSide.Player1, PlayerSide.Player2 })
            {
                int count = pieces.Count(p => p.Type == PieceType.General && p.Side == side);
                if (count != 1)
                    return $"{side} has {count} Generals, expected 1";
                var general = board.GetGeneral(side);
                if (!board.IsInPalace(side, general.X, general.Y))
                    return $"{side}'s General at ({general.X},{general.Y}) is outside its palace";
            }

            if (board.AreGeneralsFacing())
                return "the Generals face each other";

            var opponent = sideToMove == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
            if (board.IsSideInCheck(opponent))
                return $"{opponent} is in check but it is {sideToMove}'s move";

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
