/* ----- ----- ----- ----- */
// BoardAnalysis.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/01
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Pieces.PieceTypes;

namespace Chinese_Chess_v3.Game.Core.Boards
{
    /// <summary>
    /// Position analysis for board hints. Pure logic on top of the pieces' legality APIs
    /// (<see cref="Piece.CanMoveTo"/>); never modifies the board (captures are simulated
    /// with <see cref="Board.SimulateMove{T}"/>).
    /// </summary>
    public static class BoardAnalysis
    {
        /// <summary>
        /// Hanging pieces (無根子可被吃) of both sides: pieces the opponent can legally
        /// capture right now, regardless of whose turn it is, where after at least one of
        /// those captures no friendly piece can legally capture back on that square.
        /// </summary>
        /// <remarks>
        /// "Legally" includes the check rules on boards that use them
        /// (<see cref="Board.UsesCheckRules"/>): an attacker pinned to its own General does
        /// not count as an attacker, and a defender whose recapture would expose its own
        /// General (or face the Generals) does not count as a defender. On those boards the
        /// Generals themselves are never reported — an attacked General is check, shown by
        /// the check rules, not a piece that can be taken. On the dark-chess boards (no
        /// check rules, a General can be captured) they are treated like any other piece.
        /// On the dark-chess boards a face-down piece is left out entirely — never reported,
        /// never counted as an attacker or a defender (see <see cref="IsIdentityKnown"/>) —
        /// so the hints never depend on, or reveal, a hidden piece's side or type. It still
        /// counts as an occupied square (e.g. a Cannon screen), which is public.
        /// </remarks>
        public static List<Piece> GetHangingPieces(Board board)
        {
            var result = new List<Piece>();
            if (board == null)
                return result;

            // Snapshot: the analysis must not depend on the live list's order or be
            // affected by anything else touching it while it runs.
            var pieces = board.GetAllPieces().ToArray();
            foreach (var target in pieces)
            {
                if (IsHanging(board, pieces, target))
                    result.Add(target);
            }
            return result;
        }

        /// <summary>
        /// Whether <paramref name="target"/> is hanging (see <see cref="GetHangingPieces"/>).
        /// </summary>
        public static bool IsHanging(Board board, Piece target)
        {
            if (board == null || target == null)
                return false;
            return IsHanging(board, board.GetAllPieces().ToArray(), target);
        }

        private static bool IsHanging(Board board, Piece[] pieces, Piece target)
        {
            if (board.GetPiece(target.X, target.Y) != target)
                return false;
            if (board.UsesCheckRules && target is General)
                return false;
            if (!IsIdentityKnown(board, target))
                return false;

            int x = target.X;
            int y = target.Y;
            foreach (var attacker in pieces)
            {
                // A 自殺 move (Rules.CanSuicide) kills the mover, not the target: not an attack.
                if (!IsIdentityKnown(board, attacker) || attacker.IsSameFaction(target) || !attacker.CanMoveTo(board, x, y)
                    || attacker.IsSuicideMove(board, x, y))
                    continue;

                // A single capture that cannot be answered is enough.
                if (!CanRecapture(board, pieces, attacker, target))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Whether, after <paramref name="attacker"/> captures <paramref name="target"/>,
        /// some other piece of the target's side can legally capture the attacker on that
        /// square.
        /// </summary>
        private static bool CanRecapture(Board board, Piece[] pieces, Piece attacker, Piece target)
        {
            int x = target.X;
            int y = target.Y;
            return board.SimulateMove(attacker, x, y, () =>
            {
                foreach (var defender in pieces)
                {
                    if (defender == target || !IsIdentityKnown(board, defender) || !defender.IsSameFaction(target)
                        || board.IsSimulatedCapture(defender))
                        continue;
                    if (defender.CanMoveTo(board, x, y) && !defender.IsSuicideMove(board, x, y))
                        return true;
                }
                return false;
            }, fallback: false);
        }

        /// <summary>
        /// Whether the analysis may use <paramref name="piece"/>'s side and type. On the
        /// dark-chess boards (HalfCenter, HalfCross) only for a face-up piece: a face-down
        /// one's identity is hidden information, and it cannot move before it is flipped
        /// anyway, so it neither attacks nor defends. Always on the Full board — there a
        /// face-down piece only exists in 揭棋 (<see cref="Rules.IsJieqi"/>), whose side is
        /// public and which moves as the type its square starts with, not as itself.
        /// </summary>
        private static bool IsIdentityKnown(Board board, Piece piece) =>
            board.Type == BoardType.Full || piece.CurrentInfo.IsFaceUp;
    }
}
