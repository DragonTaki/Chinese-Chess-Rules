/* ----- ----- ----- ----- */
// ActionResolver.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Notation;
using Chinese_Chess_v3.Game.Core.Pgn;
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
    /// whether a side may move a piece or flip one, what a move or flip does (captures, the
    /// dark-chess outcomes, deciding the factions) and whether the game is over afterwards. It
    /// changes only the board: clocks, undo history, events, hints and tactical announcements are
    /// the caller's business.
    /// </summary>
    public static class ActionResolver
    {
        /// <summary>The other player of a two-player game.</summary>
        public static PlayerSide OpponentOf(PlayerSide side) =>
            side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;

        /// <summary>
        /// Whether <paramref name="mover"/> may select (and move) <paramref name="piece"/>: it is
        /// that side's, and on a <see cref="Board.UsesDarkChessRules"/> board face up — a
        /// face-down piece is only ever flipped, so neither its side nor its type may decide
        /// anything (揭棋's face-down pieces on the Full board do move, as their square's type).
        /// A face-up piece nobody owns yet (明棋半盤 before the first move) is anyone's: moving it
        /// decides the factions.
        /// </summary>
        public static bool CanAct(Board board, Piece piece, PlayerSide mover) =>
            piece != null
            && (piece.Side == mover || (board.UsesDarkChessRules && piece.Side == PlayerSide.None))
            && (!board.UsesDarkChessRules || piece.CurrentInfo.IsFaceUp);

        /// <summary>Whether <paramref name="mover"/> may move the piece on (fromX, fromY) to (toX, toY).</summary>
        public static bool IsLegalMove(Board board, PlayerSide mover, int fromX, int fromY, int toX, int toY)
        {
            var piece = board.GetPiece(fromX, fromY);
            return CanAct(board, piece, mover) && piece.CanMoveTo(board, toX, toY);
        }

        /// <summary>Whether the piece on (x, y) may be flipped: a face-down piece on a <see cref="Board.UsesDarkChessRules"/> board.</summary>
        public static bool IsLegalFlip(Board board, int x, int y)
        {
            var piece = board.GetPiece(x, y);
            return board.UsesDarkChessRules && piece != null && !piece.CurrentInfo.IsFaceUp;
        }

        /// <summary>
        /// Applies an already-validated move (<see cref="IsLegalMove"/>) of <paramref name="piece"/>
        /// to (toX, toY) for <paramref name="mover"/>. On the dark-chess board a move onto a
        /// face-down piece is a hidden capture (暗吃, see <see cref="ApplyHiddenCapture"/>) and a
        /// move onto a stronger face-up enemy is a 自殺; a move of a piece nobody owns yet
        /// (明棋半盤's first move) first gives the mover that piece's colour
        /// (<see cref="Board.AssignFactions"/>). The board's turn counter is not advanced (the
        /// caller does it first when it keeps history).
        /// </summary>
        public static ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY)
        {
            var target = board.GetPiece(toX, toY);
            if (board.UsesDarkChessRules && target != null && !target.CurrentInfo.IsFaceUp)
                return ApplyHiddenCapture(board, mover, piece, target);
            if (board.UsesDarkChessRules && piece.IsSuicideMove(board, toX, toY))
                return ApplySuicide(board, mover, piece, toX, toY);

            int fromX = piece.X;
            int fromY = piece.Y;
            var pieceBefore = piece.CurrentInfo.Clone();
            bool decidesFactions = board.UsesDarkChessRules && piece.Side == PlayerSide.None;

            // The notation also depends on the other pieces on the file (前/後), and whether the
            // move gives check is simulated, so both are taken before the board changes.
            string notation = ChineseMoveNotation.Format(board, fromX, fromY, toX, toY);
            string iccs = board.Type == BoardType.Full ? new IccsMove(fromX, fromY, toX, toY).ToString() : null;
            var opponentSide = OpponentOf(piece.Side);
            bool givesCheck = board.UsesCheckRules &&
                board.SimulateMove(piece, toX, toY, () => board.IsSideInCheck(opponentSide), fallback: false);

            var captured = target?.CurrentInfo.Clone();

            if (decidesFactions)
                board.AssignFactions(piece.Color, mover);
            if (target != null)
                board.RemovePiece(toX, toY);
            board.MovePiece(fromX, fromY, toX, toY);

            return new ActionOutcome
            {
                Kind = MoveKind.Move, Mover = mover,
                FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
                Piece = piece, PieceBefore = pieceBefore,
                CapturedPiece = target, Captured = captured,
                DecidesFactions = decidesFactions,
                Notation = notation, Iccs = iccs, GivesCheck = givesCheck,
            };
        }

        /// <summary>
        /// Applies a flip (翻子, dark chess) of the face-down <paramref name="piece"/> for
        /// <paramref name="mover"/>: it turns face up and — on the game's first flip (nobody owns
        /// a colour yet) — the mover gets the flipped piece's colour and the other player the
        /// other one (<see cref="Board.AssignFactions"/>).
        /// </summary>
        public static ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece)
        {
            var pieceBefore = piece.CurrentInfo.Clone();
            bool decidesFactions = piece.Side == PlayerSide.None;

            board.FlipPiece(piece.X, piece.Y);
            if (decidesFactions)
                board.AssignFactions(piece.Color, mover);

            return new ActionOutcome
            {
                Kind = MoveKind.Flip, Mover = mover,
                FromX = piece.X, FromY = piece.Y, ToX = piece.X, ToY = piece.Y,
                Piece = piece, PieceBefore = pieceBefore,
                Revealed = piece.CurrentInfo.Clone(),
                DecidesFactions = decidesFactions,
            };
        }

        /// <summary>
        /// A move of <paramref name="piece"/> onto the face-down <paramref name="target"/> (暗吃,
        /// <see cref="Rules.CanCaptureHiddenPiece"/>): the target is turned face up, then
        /// <list type="bullet">
        /// <item>the mover's own piece: the mover stays on its from-square
        /// (<see cref="MoveKind.HiddenOwnPiece"/>);</item>
        /// <item>an enemy piece the mover may capture by the normal rules (re-checked now that
        /// it is face up — rank order and the Soldier/General pair; a Cannon's jump capture
        /// ignores rank): a normal capture (<see cref="MoveKind.HiddenCapture"/>);</item>
        /// <item>any other enemy piece (a stronger one, or a Soldier when the mover is a General):
        /// <see cref="Rules.IsCaptureHiddenPieceStrongerSuicide"/> on, the mover dies
        /// (<see cref="MoveKind.HiddenStrongerSuicide"/>); off, it returns to its from-square
        /// (<see cref="MoveKind.HiddenStrongerReturn"/>). The target stays either way.</item>
        /// </list>
        /// </summary>
        private static ActionOutcome ApplyHiddenCapture(Board board, PlayerSide mover, Piece piece, Piece target)
        {
            int fromX = piece.X;
            int fromY = piece.Y;
            int toX = target.X;
            int toY = target.Y;
            var pieceBefore = piece.CurrentInfo.Clone();

            board.FlipPiece(toX, toY);
            var revealed = target.CurrentInfo.Clone();

            MoveKind kind;
            Piece captured = null;
            bool moverDied = false;
            if (target.Side == piece.Side)
            {
                kind = MoveKind.HiddenOwnPiece;
            }
            // With 自殺 on, a move onto a stronger enemy is legal too, so it is checked apart.
            else if (piece.IsPseudoLegalMove(board, toX, toY) && !piece.IsSuicideMove(board, toX, toY))
            {
                kind = MoveKind.HiddenCapture;
                captured = target;
                board.RemovePiece(toX, toY);
                board.MovePiece(fromX, fromY, toX, toY);
            }
            else if (board.GameRules.IsCaptureHiddenPieceStrongerSuicide)
            {
                kind = MoveKind.HiddenStrongerSuicide;
                moverDied = true;
                board.RemovePiece(fromX, fromY);
            }
            else
            {
                kind = MoveKind.HiddenStrongerReturn;
            }

            return new ActionOutcome
            {
                Kind = kind, Mover = mover,
                FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
                Piece = piece, PieceBefore = pieceBefore,
                CapturedPiece = captured, Captured = captured != null ? revealed : null,
                Revealed = revealed, MoverDied = moverDied,
            };
        }

        /// <summary>
        /// A 自殺 move (<see cref="Rules.CanSuicide"/>, <see cref="Piece.IsSuicideMove"/>):
        /// <paramref name="piece"/> dies (taken off the board) and the face-up enemy piece on
        /// (toX, toY) stays (<see cref="MoveKind.Suicide"/>).
        /// </summary>
        private static ActionOutcome ApplySuicide(Board board, PlayerSide mover, Piece piece, int toX, int toY)
        {
            int fromX = piece.X;
            int fromY = piece.Y;
            var pieceBefore = piece.CurrentInfo.Clone();
            var target = board.GetPiece(toX, toY).CurrentInfo.Clone();

            board.RemovePiece(fromX, fromY);

            return new ActionOutcome
            {
                Kind = MoveKind.Suicide, Mover = mover,
                FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
                Piece = piece, PieceBefore = pieceBefore,
                Revealed = target, MoverDied = true,
            };
        }

        /// <summary>
        /// Whether the game is over after <paramref name="mover"/>'s action, evaluated before the
        /// turn is handed over (so the loser's clock never starts).
        /// <para>
        /// Check-rules board (standard xiangqi): the opponent, about to move, loses on the spot when
        /// it has no legal move — checkmate if in check, otherwise stalemate (困斃, which also covers
        /// "every remaining move would face the Generals").
        /// </para>
        /// <para>
        /// Dark-chess board, once the factions are decided: a player with no piece left (face up or
        /// face down) loses (<see cref="GameOverReason.NoPiecesLeft"/>) — the opponent first, then
        /// the mover (whose last piece can die in a hidden capture); otherwise the opponent loses if
        /// it has no action on its turn (no legal move and nothing to flip,
        /// <see cref="Board.HasAnyAction"/>; <see cref="GameOverReason.Stalemate"/>). Draw rules are
        /// not decided yet, so none is applied.
        /// </para>
        /// </summary>
        /// <param name="opponentInCheck">Check-rules board: whether the opponent is now in check; false otherwise.</param>
        /// <returns>How the game ended; null while it goes on.</returns>
        public static GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck)
        {
            var opponent = OpponentOf(mover);
            opponentInCheck = false;

            if (board.UsesCheckRules)
            {
                opponentInCheck = board.IsSideInCheck(opponent);
                if (!board.HasAnyLegalMove(opponent))
                    return new GameEnd(mover, opponent, opponentInCheck ? GameOverReason.Checkmate : GameOverReason.Stalemate);
                return null;
            }

            if (!board.UsesDarkChessRules)
                return null;

            // Nobody owns a piece before the factions are decided (every action decides them,
            // so this only guards against a custom position).
            if (!FactionsDecided(board))
                return null;

            if (board.QueryPieces(side: opponent).Count == 0)
                return new GameEnd(mover, opponent, GameOverReason.NoPiecesLeft);
            if (board.QueryPieces(side: mover).Count == 0)
                return new GameEnd(opponent, mover, GameOverReason.NoPiecesLeft);
            if (!board.HasAnyAction(opponent))
                return new GameEnd(mover, opponent, GameOverReason.Stalemate);
            return null;
        }

        /// <summary>Whether any piece on the board is owned by a player (the dark-chess factions are decided).</summary>
        public static bool FactionsDecided(Board board)
        {
            foreach (var p in board.GetAllPieces())
            {
                if (p.Side == PlayerSide.Player1 || p.Side == PlayerSide.Player2)
                    return true;
            }
            return false;
        }
    }
}
