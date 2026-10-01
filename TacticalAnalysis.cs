/* ----- ----- ----- ----- */
// TacticalAnalysis.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// What <see cref="TacticalAnalysis.Analyze"/> needs to know about the position
    /// before the move (the "newly ..." events). Taken with
    /// <see cref="TacticalAnalysis.TakeSnapshot"/> on the real board right before the
    /// move is applied.
    /// </summary>
    public sealed class TacticalSnapshot
    {
        /// <summary>Opponent chariots the mover could legally capture before the move.</summary>
        internal HashSet<Piece> CapturableChariots { get; }

        /// <summary>Opponent chariots/horses/cannons that were hanging before the move.</summary>
        internal HashSet<Piece> HangingMajors { get; }

        internal TacticalSnapshot(HashSet<Piece> capturableChariots, HashSet<Piece> hangingMajors)
        {
            CapturableChariots = capturableChariots;
            HangingMajors = hangingMajors;
        }
    }

    /// <summary>
    /// Detects the tactical events (<see cref="TacticalEventType"/>) of a completed move on
    /// a board that uses check rules (standard xiangqi). Pure logic on top of the pieces'
    /// legality APIs; never modifies the board (opponent replies are simulated with
    /// <see cref="Board.SimulateMove{T}"/>).
    /// </summary>
    /// <remarks>
    /// Usage: <see cref="TakeSnapshot"/> before the move, apply the move, then
    /// <see cref="Analyze"/> on the resulting board. Both calls must run on the real
    /// board, not inside a simulation (<see cref="Board.HasAnyLegalMove"/> and
    /// <see cref="BoardAnalysis.GetHangingPieces"/> do not skip simulated captures).
    /// </remarks>
    public static class TacticalAnalysis
    {
        private static readonly PieceType[] MajorTypes = { PieceType.Chariot, PieceType.Horse, PieceType.Cannon };

        /// <summary>
        /// Records the pre-move facts <see cref="Analyze"/> compares against, for a move
        /// about to be made by <paramref name="mover"/>.
        /// </summary>
        public static TacticalSnapshot TakeSnapshot(Board board, PlayerSide mover)
        {
            var opponent = OpponentOf(mover);
            var capturable = new HashSet<Piece>();
            var hanging = new HashSet<Piece>();
            if (board == null || !board.UsesCheckRules)
                return new TacticalSnapshot(capturable, hanging);

            var pieces = board.GetAllPieces().ToArray();
            foreach (var chariot in pieces)
            {
                if (chariot.Side == opponent && chariot.Type == PieceType.Chariot && CanSideCapture(board, pieces, mover, chariot))
                    capturable.Add(chariot);
            }
            foreach (var p in BoardAnalysis.GetHangingPieces(board))
            {
                if (p.Side == opponent && IsMajor(p.Type))
                    hanging.Add(p);
            }
            return new TacticalSnapshot(capturable, hanging);
        }

        /// <summary>
        /// The tactical events of <paramref name="move"/>, evaluated on
        /// <paramref name="board"/> as it is right after the move (the opponent to move),
        /// in <see cref="TacticalEventType"/> declaration order. Empty on boards without
        /// check rules.
        /// </summary>
        public static List<TacticalEvent> Analyze(Board board, MoveRecord move, TacticalSnapshot before)
        {
            var events = new List<TacticalEvent>();
            if (board == null || move == null || before == null || !board.UsesCheckRules)
                return events;

            var mover = move.Side;
            var opponent = OpponentOf(mover);
            var moved = board.GetPiece(move.ToX, move.ToY);
            var pieces = board.GetAllPieces().ToArray();

            var checkers = board.GetCheckingPieces(opponent);
            var replies = GetLegalReplies(board, pieces, opponent);

            void Add(TacticalEventType type, IEnumerable<Piece> involved) =>
                events.Add(new TacticalEvent(type, mover, move, involved.Select(p => p.CurrentInfo.Clone()).ToList()));

            // --- Game-ending results ---
            if (replies.Count == 0)
                Add(checkers.Count > 0 ? TacticalEventType.Checkmate : TacticalEventType.Stalemate, checkers);

            // --- Kinds of check ---
            if (checkers.Count >= 2)
                Add(TacticalEventType.DoubleCheck, checkers);
            if (checkers.Count > 0 && !checkers.Contains(moved)
                && checkers.Any(c => IsLineOpenedBy(c, board.GetGeneral(opponent), move.FromX, move.FromY)))
                Add(TacticalEventType.DiscoveredCheck, checkers);
            if (checkers.Count > 0 && replies.Count > 0)
                Add(TacticalEventType.Check, checkers);

            // --- Forking check (抽X) and trapped chariot (打死車): quantified over every reply ---
            if (replies.Count > 0)
            {
                if (checkers.Count > 0)
                {
                    foreach (var type in new[] { PieceType.Chariot, PieceType.Cannon, PieceType.Horse })
                    {
                        var targets = FindForkedTargets(board, pieces, replies, mover, type);
                        if (targets != null)
                        {
                            Add(ForkingEventOf(type), targets);
                            break; // only the most valuable kind (車 > 炮 > 馬)
                        }
                    }
                }

                var trapped = FindTrappedChariots(board, pieces, replies, mover);
                if (trapped.Count > 0)
                    Add(TacticalEventType.TrappedChariot, trapped);
            }

            // --- Captures ---
            if (move.Captured != null)
            {
                var captureType = move.Captured.Type switch
                {
                    PieceType.Chariot => TacticalEventType.CaptureChariot,
                    PieceType.Cannon => TacticalEventType.CaptureCannon,
                    PieceType.Horse => TacticalEventType.CaptureHorse,
                    _ => (TacticalEventType?)null,
                };
                if (captureType.HasValue)
                    events.Add(new TacticalEvent(captureType.Value, mover, move, new List<PieceInfo> { move.Captured.Clone() }));
            }

            // --- Double attack (捉雙) ---
            var newlyHanging = BoardAnalysis.GetHangingPieces(board)
                .Where(p => p.Side == opponent && IsMajor(p.Type) && !before.HangingMajors.Contains(p))
                .ToList();
            if (newlyHanging.Count >= 2)
                Add(TacticalEventType.DoubleAttack, newlyHanging);

            // --- Chariot threat (閃擊) ---
            var threatened = FindNewChariotThreats(board, pieces, mover, before);
            if (threatened.Count > 0)
                Add(TacticalEventType.ChariotThreat, threatened);

            return events;
        }

        /// <summary>
        /// 閃擊 as currently defined by the author (kept in one place because the definition
        /// may be revised): opponent chariots the mover can legally capture now (ignoring
        /// whose turn it is) that it could not legally capture right before the move.
        /// </summary>
        private static List<Piece> FindNewChariotThreats(Board board, Piece[] pieces, PlayerSide mover, TacticalSnapshot before)
        {
            var opponent = OpponentOf(mover);
            var result = new List<Piece>();
            foreach (var chariot in pieces)
            {
                if (chariot.Side != opponent || chariot.Type != PieceType.Chariot || before.CapturableChariots.Contains(chariot))
                    continue;
                if (CanSideCapture(board, pieces, mover, chariot))
                    result.Add(chariot);
            }
            return result;
        }

        /// <summary>
        /// For a forking check: if after every reply the mover can legally capture some
        /// opponent piece of <paramref name="type"/>, the opponent pieces of that type
        /// capturable after at least one reply (as they stand before the reply); otherwise null.
        /// </summary>
        private static List<Piece> FindForkedTargets(
            Board board, Piece[] pieces, List<(Piece piece, int x, int y)> replies, PlayerSide mover, PieceType type)
        {
            var opponent = OpponentOf(mover);
            var targets = new HashSet<Piece>();
            foreach (var (piece, x, y) in replies)
            {
                bool any = board.SimulateMove(piece, x, y, () =>
                {
                    bool found = false;
                    foreach (var target in pieces)
                    {
                        if (target.Side != opponent || target.Type != type || board.IsSimulatedCapture(target))
                            continue;
                        if (CanSideCapture(board, pieces, mover, target))
                        {
                            targets.Add(target);
                            found = true;
                        }
                    }
                    return found;
                }, fallback: false);
                if (!any)
                    return null;
            }
            return pieces.Where(targets.Contains).ToList();
        }

        /// <summary>
        /// 打死車: opponent chariots that, after every legal reply (including the chariot's
        /// own moves, wherever it goes), a mover cannon can legally capture.
        /// </summary>
        private static List<Piece> FindTrappedChariots(
            Board board, Piece[] pieces, List<(Piece piece, int x, int y)> replies, PlayerSide mover)
        {
            var opponent = OpponentOf(mover);
            var result = new List<Piece>();
            foreach (var chariot in pieces)
            {
                if (chariot.Side != opponent || chariot.Type != PieceType.Chariot)
                    continue;

                bool trapped = true;
                foreach (var (piece, x, y) in replies)
                {
                    trapped = board.SimulateMove(piece, x, y, () =>
                        pieces.Any(c => c.Side == mover && c.Type == PieceType.Cannon && !board.IsSimulatedCapture(c)
                            && c.CanMoveTo(board, chariot.X, chariot.Y)), fallback: false);
                    if (!trapped)
                        break;
                }
                if (trapped)
                    result.Add(chariot);
            }
            return result;
        }

        /// <summary>Every legal move of <paramref name="side"/> on the current board.</summary>
        private static List<(Piece piece, int x, int y)> GetLegalReplies(Board board, Piece[] pieces, PlayerSide side)
        {
            var replies = new List<(Piece piece, int x, int y)>();
            foreach (var p in pieces)
            {
                if (p.Side != side)
                    continue;
                foreach (var (x, y) in p.GetLegalMoves(board))
                    replies.Add((p, x, y));
            }
            return replies;
        }

        /// <summary>
        /// Whether some piece of <paramref name="side"/> can legally capture
        /// <paramref name="target"/> where it stands. Safe inside a simulation.
        /// </summary>
        private static bool CanSideCapture(Board board, Piece[] pieces, PlayerSide side, Piece target)
        {
            foreach (var p in pieces)
            {
                if (p.Side == side && !board.IsSimulatedCapture(p) && p.CanMoveTo(board, target.X, target.Y))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Whether <paramref name="checker"/>'s attack on <paramref name="general"/> runs
        /// through (fromX, fromY) - i.e. vacating that square opened the line: strictly
        /// between them on a shared file/rank for a chariot or cannon, or the horse's leg.
        /// </summary>
        private static bool IsLineOpenedBy(Piece checker, Piece general, int fromX, int fromY)
        {
            if (checker == null || general == null)
                return false;

            int cx = checker.X, cy = checker.Y, gx = general.X, gy = general.Y;
            switch (checker.Type)
            {
                case PieceType.Chariot:
                case PieceType.Cannon:
                    if (cx == gx && fromX == cx)
                        return fromY > Math.Min(cy, gy) && fromY < Math.Max(cy, gy);
                    if (cy == gy && fromY == cy)
                        return fromX > Math.Min(cx, gx) && fromX < Math.Max(cx, gx);
                    return false;
                case PieceType.Horse:
                    int dx = gx - cx, dy = gy - cy;
                    if (Math.Abs(dx) == 2 && Math.Abs(dy) == 1)
                        return fromX == cx + dx / 2 && fromY == cy;
                    if (Math.Abs(dx) == 1 && Math.Abs(dy) == 2)
                        return fromX == cx && fromY == cy + dy / 2;
                    return false;
                default:
                    return false;
            }
        }

        private static TacticalEventType ForkingEventOf(PieceType type) => type switch
        {
            PieceType.Chariot => TacticalEventType.ForkingCheckChariot,
            PieceType.Cannon => TacticalEventType.ForkingCheckCannon,
            _ => TacticalEventType.ForkingCheckHorse,
        };

        private static bool IsMajor(PieceType type) => Array.IndexOf(MajorTypes, type) >= 0;

        private static PlayerSide OpponentOf(PlayerSide side) =>
            side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
    }
}
