/* ----- ----- ----- ----- */
// ActionResolver.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.1
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Families;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// What one action did to the board (<see cref="ActionResolver.ApplyMove"/> /
    /// <see cref="ActionResolver.ApplyFlip"/>): enough to record it, to raise the board events
    /// and to take it back.
    /// </summary>
    public sealed class ActionOutcome
    {
        /// <summary>What kind of action it was (a move, a flip, a hidden-capture outcome, 自殺).</summary>
        public MoveKind Kind { get; init; }

        /// <summary>The side that acted.</summary>
        public PlayerSide Mover { get; init; }

        public int FromX { get; init; }
        public int FromY { get; init; }
        public int ToX { get; init; }
        public int ToY { get; init; }

        /// <summary>The acting piece (moved, flipped, or the one that died).</summary>
        public Piece Piece { get; init; }

        /// <summary>The acting piece as it was before the action.</summary>
        public PieceInfo PieceBefore { get; init; }

        /// <summary>
        /// The piece object taken off the board by a capture (a normal capture's target, a hidden
        /// capture's revealed target) - put back by undo; null otherwise. A mover that dies
        /// (<see cref="MoveKind.HiddenStrongerSuicide"/>, <see cref="MoveKind.Suicide"/>) is not
        /// listed here (see <see cref="MoverDied"/>).
        /// </summary>
        public Piece CapturedPiece { get; init; }

        /// <summary>The captured piece as it was before being taken off (for the move record); null when nothing was captured.</summary>
        public PieceInfo Captured { get; init; }

        /// <summary>Dark chess: the piece the action showed (the flipped piece, a hidden capture's target, the piece a 自殺 collided with); null for a normal move.</summary>
        public PieceInfo Revealed { get; init; }

        /// <summary>Whether the acting piece died (<see cref="MoveKind.HiddenStrongerSuicide"/>, <see cref="MoveKind.Suicide"/>).</summary>
        public bool MoverDied { get; init; }

        /// <summary>Whether the action gave the players their colours (the first flip, or 明棋半盤's first move).</summary>
        public bool DecidesFactions { get; init; }

        /// <summary>The Chinese notation of a normal Full-board move (炮二平五); null otherwise.</summary>
        public string Notation { get; init; }

        /// <summary>The ICCS coordinates of a Full-board move (h2e2); null otherwise.</summary>
        public string Iccs { get; init; }

        /// <summary>Whether a normal move on a check-rules board puts the opponent in check.</summary>
        public bool GivesCheck { get; init; }
    }

    /// <summary>How a game ended after an action: the winner, the loser and why.</summary>
    public sealed record GameEnd(PlayerSide Winner, PlayerSide Loser, GameOverReason Reason);

    /// <summary>
    /// The rules of acting on a board, shared by every program that plays or checks a game - the
    /// client's <see cref="GameManager"/> and the server's rules host - so there is only one copy:
    /// whether a side may move a piece or flip one, what a move or flip does and whether the game
    /// is over afterwards. Each question goes to the board's rules family (<see cref="Board.Family"/>:
    /// Full-board xiangqi, half-board dark chess, Three Kingdoms). It changes only the board:
    /// clocks, undo history, events, hints and tactical announcements are the caller's business.
    /// </summary>
    public static class ActionResolver
    {
        /// <summary>The other player of a two-player game.</summary>
        public static PlayerSide OpponentOf(PlayerSide side) =>
            side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;

        /// <summary>Whether <paramref name="mover"/> may select (and move) <paramref name="piece"/> (<see cref="IRulesFamily.CanAct"/>).</summary>
        public static bool CanAct(Board board, Piece piece, PlayerSide mover) => board.Family.CanAct(board, piece, mover);

        /// <summary>Whether <paramref name="mover"/> may move the piece on (fromX, fromY) to (toX, toY).</summary>
        public static bool IsLegalMove(Board board, PlayerSide mover, int fromX, int fromY, int toX, int toY)
        {
            var piece = board.GetPiece(fromX, fromY);
            return CanAct(board, piece, mover) && piece.CanMoveTo(board, toX, toY);
        }

        /// <summary>Whether the piece on (x, y) may be flipped as an action (<see cref="IRulesFamily.CanFlip"/>).</summary>
        public static bool IsLegalFlip(Board board, int x, int y) => board.Family.CanFlip(board, board.GetPiece(x, y));

        /// <summary>
        /// Applies an already-validated move (<see cref="IsLegalMove"/>) of <paramref name="piece"/>
        /// to (toX, toY) for <paramref name="mover"/> (<see cref="IRulesFamily.ApplyMove"/>). The
        /// board's turn counter is not advanced (the caller does it first when it keeps history).
        /// </summary>
        public static ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY) =>
            board.Family.ApplyMove(board, mover, piece, toX, toY);

        /// <summary>Applies a flip of the face-down <paramref name="piece"/> for <paramref name="mover"/> (<see cref="IRulesFamily.ApplyFlip"/>).</summary>
        public static ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece) =>
            board.Family.ApplyFlip(board, mover, piece);

        /// <summary>
        /// Whether the game is over after <paramref name="mover"/>'s action, evaluated before the
        /// turn is handed over (so the loser's clock never starts; <see cref="IRulesFamily.EvaluateEnd"/>).
        /// </summary>
        /// <param name="opponentInCheck">Check-rules board: whether the opponent is now in check; false otherwise.</param>
        /// <returns>How the game ended; null while it goes on.</returns>
        public static GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck) =>
            board.Family.EvaluateEnd(board, mover, out opponentInCheck);

        /// <summary>Whether any piece on the board is owned by a player (the dark-chess factions are decided).</summary>
        public static bool FactionsDecided(Board board)
        {
            foreach (var p in board.GetAllPieces())
            {
                if (p.Side != PlayerSide.None)
                    return true;
            }
            return false;
        }
    }
}
