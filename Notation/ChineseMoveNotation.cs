/* ----- ----- ----- ----- */
// ChineseMoveNotation.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Notation
{
    /// <summary>
    /// Standard four-character Chinese move notation (中文縱線格式, e.g. 炮二平五, 馬８進７),
    /// following https://www.xqbase.com/protocol/cchess_move.htm, for the Full (9x10) board.
    /// <para>
    /// Characters 1-2 name the piece (piece + file, or a 前/中/後/一..五 prefix + piece),
    /// character 3 is the action (進 forward, 退 backward, 平 sideways), character 4 the
    /// target: the number of steps for a straight 進/退 of 帥/將, 俥/車, 炮/砲 and 兵/卒, the
    /// target file for every 平 and for any move of 傌/馬, 相/象, 仕/士.
    /// </para>
    /// <para>
    /// Files are counted from each side's own right-hand side: Red (Player1, back rank y = 9)
    /// file = 9 - x, written 一..九; Black (Player2, back rank y = 0) file = x + 1, written with
    /// full-width digits １..９ (the spec's rule: every number in a Black move is an Arabic
    /// digit, shown full-width on a computer, except the 一..五 soldier labels below).
    /// </para>
    /// <para>
    /// Piece characters are fixed standard characters by side (Red 帥仕相俥傌炮兵, Black
    /// 將士象車馬砲卒), independent of how the pieces are drawn (piece skins).
    /// </para>
    /// <para>
    /// Several same pieces on one file (the file number is then dropped, keeping four
    /// characters): 2 -> 前/後, 3 -> 前/中/後, 4 or more -> 一/二/三/四/五 from the front
    /// (the spec writes these for soldiers; applied to any piece type so custom positions
    /// still get a unique name). 仕/士 and 相/象 never take a prefix: on one file the front
    /// one can only retreat and the back one only advance, so file + action is unique.
    /// When two or more files each hold two or more of the pieces (in practice soldiers), all
    /// of those pieces are labelled 一, 二, ... together: files in the order of the side's own
    /// file numbers (right to left from that side), front to back within a file; pieces on
    /// other files keep the plain form (example from the spec: 一兵平五, 兵五進一). The
    /// 前/中/後/一..五 labels are Chinese characters for both sides (一卒平５).
    /// </para>
    /// </summary>
    public static class ChineseMoveNotation
    {
        private const string RedNumerals = "一二三四五六七八九";
        private const string BlackNumerals = "１２３４５６７８９";

        public const char Forward = '進';
        public const char Backward = '退';
        public const char Sideways = '平';

        /// <summary>
        /// Notation of moving the piece at (fromX, fromY) to (toX, toY), read on the board
        /// <b>before</b> the move (the prefix depends on the other pieces on the file).
        /// Null if the board is not the Full board or there is no Player1/Player2 piece there.
        /// </summary>
        public static string Format(Board board, int fromX, int fromY, int toX, int toY)
        {
            if (board == null || board.Type != BoardType.Full)
                return null;
            var position = board.GetAllPieces()
                .Where(p => !board.IsSimulatedCapture(p))
                .Select(p => p.CurrentInfo);
            return Format(position, fromX, fromY, toX, toY);
        }

        /// <summary>
        /// As <see cref="Format(Board, int, int, int, int)"/> on a position snapshot (e.g. a
        /// replayed position); dead pieces are ignored. Coordinates are Full-board coordinates.
        /// Null if no live Player1/Player2 piece of a notated type stands on (fromX, fromY), or
        /// the move does not move.
        /// </summary>
        public static string Format(IEnumerable<PieceInfo> position, int fromX, int fromY, int toX, int toY)
        {
            var live = position.Where(p => p != null && !p.IsDead).ToList();
            var mover = live.FirstOrDefault(p => p.X == fromX && p.Y == fromY);
            if (mover == null || (fromX == toX && fromY == toY))
                return null;
            var side = mover.Side;
            if (side != PlayerSide.Player1 && side != PlayerSide.Player2)
                return null;
            char pieceChar = PieceChar(mover.Type, side);
            if (pieceChar == '\0')
                return null;

            string name = PieceName(live, mover, pieceChar);

            int forwardSteps = side == PlayerSide.Player1 ? fromY - toY : toY - fromY;
            if (forwardSteps == 0)
                return name + Sideways + FileChar(side, toX);

            char action = forwardSteps > 0 ? Forward : Backward;
            char target = IsStraightMover(mover.Type)
                ? Number(side, System.Math.Abs(forwardSteps))
                : FileChar(side, toX);
            return name + action + target;
        }

        /// <summary>The standard character of <paramref name="type"/> for <paramref name="side"/>; '\0' if none.</summary>
        public static char PieceChar(PieceType type, PlayerSide side)
        {
            bool red = side == PlayerSide.Player1;
            return type switch
            {
                PieceType.General => red ? '帥' : '將',
                PieceType.Advisor => red ? '仕' : '士',
                PieceType.Elephant => red ? '相' : '象',
                PieceType.Chariot => red ? '俥' : '車',
                PieceType.Horse => red ? '傌' : '馬',
                PieceType.Cannon => red ? '炮' : '砲',
                PieceType.Soldier => red ? '兵' : '卒',
                _ => '\0',
            };
        }

        /// <summary>The side's own file number (1 = its right-most file) of board column <paramref name="x"/>.</summary>
        public static int OwnFile(PlayerSide side, int x) =>
            side == PlayerSide.Player1 ? BoardConstants.Full.Columns - x : x + 1;

        /// <summary>Red: 一..九; Black: full-width １..９.</summary>
        private static char Number(PlayerSide side, int n) =>
            (side == PlayerSide.Player1 ? RedNumerals : BlackNumerals)[n - 1];

        private static char FileChar(PlayerSide side, int x) => Number(side, OwnFile(side, x));

        /// <summary>進/退 counts steps for these; the others (馬 相 仕) name the target file.</summary>
        private static bool IsStraightMover(PieceType type) =>
            type == PieceType.General || type == PieceType.Chariot ||
            type == PieceType.Cannon || type == PieceType.Soldier;

        /// <summary>Distance of a piece from its own back rank (larger = further forward).</summary>
        private static int Advance(PieceInfo p) =>
            p.Side == PlayerSide.Player1 ? BoardConstants.Full.Rows - 1 - p.Y : p.Y;

        /// <summary>The first two characters: piece + file, or a label + piece (see the class summary).</summary>
        private static string PieceName(List<PieceInfo> live, PieceInfo mover, char pieceChar)
        {
            string plain = pieceChar.ToString() + FileChar(mover.Side, mover.X);
            if (mover.Type == PieceType.Advisor || mover.Type == PieceType.Elephant || mover.Type == PieceType.General)
                return plain;

            var same = live.Where(p => p.Type == mover.Type && p.Side == mover.Side).ToList();
            var multiFiles = same.GroupBy(p => p.X).Where(g => g.Count() >= 2).Select(g => g.Key).ToList();
            if (!multiFiles.Contains(mover.X))
                return plain;

            if (multiFiles.Count >= 2)
            {
                // Label every piece on the crowded files together.
                var ordered = same.Where(p => multiFiles.Contains(p.X))
                    .OrderBy(p => OwnFile(p.Side, p.X))
                    .ThenByDescending(Advance)
                    .ToList();
                return Label(ordered.IndexOf(mover), ordered.Count, forceNumbers: true) + pieceChar;
            }

            var onFile = same.Where(p => p.X == mover.X).OrderByDescending(Advance).ToList();
            return Label(onFile.IndexOf(mover), onFile.Count, forceNumbers: false) + pieceChar;
        }

        /// <summary>
        /// Label of the <paramref name="index"/>-th (0 = front) of <paramref name="count"/>
        /// pieces: 前後 for 2, 前中後 for 3, otherwise (or always with
        /// <paramref name="forceNumbers"/>) Chinese numerals 一, 二, ...
        /// </summary>
        private static string Label(int index, int count, bool forceNumbers)
        {
            if (!forceNumbers && count == 2)
                return index == 0 ? "前" : "後";
            if (!forceNumbers && count == 3)
                return index == 0 ? "前" : index == 1 ? "中" : "後";
            return index < RedNumerals.Length ? RedNumerals[index].ToString() : (index + 1).ToString();
        }
    }
}
