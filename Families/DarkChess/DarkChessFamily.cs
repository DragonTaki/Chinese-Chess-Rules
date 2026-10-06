/* ----- ----- ----- ----- */
// DarkChessFamily.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Notation;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families.DarkChess
{
    /// <summary>
    /// The half-board dark chess family (台灣暗棋 8×4): 暗棋半盤 (every piece starts face down) and
    /// 明棋半盤 (face up). A turn is a flip or a move of one's own face-up piece; the first action
    /// decides who plays which colour; hidden capture (暗吃) and 自殺 are rule switches. A player with
    /// no piece left, or with nothing to do on its turn, loses. Movement: <see cref="DarkChessMoves"/>.
    /// </summary>
    internal sealed class DarkChessFamily : IRulesFamily
    {
        public int PlayerCount => 2;

        public PlayerSide NextToMove(Board board, PlayerSide mover) => ActionResolver.OpponentOf(mover);

        public bool UsesCheckRules => false;

        public bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY) =>
            DarkChessMoves.IsPseudoLegalMove(board, piece, targetX, targetY);

        public List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece) =>
            DarkChessMoves.GetPseudoLegalMoves(board, piece);

        public bool IsSuicideMove(Board board, Piece piece, int targetX, int targetY) =>
            DarkChessMoves.IsSuicideMove(board, piece, targetX, targetY);

        /// <summary>
        /// A player moves its own face-up pieces — a face-down piece is only ever flipped, so neither
        /// its side nor its type may decide anything. A face-up piece nobody owns yet (明棋半盤 before
        /// the first move) is anyone's: moving it decides the factions.
        /// </summary>
        public bool CanAct(Board board, Piece piece, PlayerSide mover) =>
            piece != null && (piece.Side == mover || piece.Side == PlayerSide.None) && piece.CurrentInfo.IsFaceUp;

        /// <summary>Any face-down piece may be flipped by the side to move.</summary>
        public bool CanFlip(Board board, Piece piece) => piece != null && !piece.CurrentInfo.IsFaceUp;

        /// <summary>
        /// A move onto a face-down piece is a hidden capture (暗吃, <see cref="ApplyHiddenCapture"/>);
        /// a move onto a stronger face-up enemy is a 自殺; a move of a piece nobody owns yet (明棋半盤's
        /// first move) first gives the mover that piece's colour (<see cref="Board.AssignFactions"/>).
        /// </summary>
        public ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY)
        {
            var target = board.GetPiece(toX, toY);
            if (target != null && !target.CurrentInfo.IsFaceUp)
                return ApplyHiddenCapture(board, mover, piece, target);
            if (DarkChessMoves.IsSuicideMove(board, piece, toX, toY))
                return ApplySuicide(board, mover, piece, toX, toY);

            int fromX = piece.X;
            int fromY = piece.Y;
            var pieceBefore = piece.CurrentInfo.Clone();
            bool decidesFactions = piece.Side == PlayerSide.None;
            // No notation on this board (ChineseMoveNotation is Full-board only).
            string notation = ChineseMoveNotation.Format(board, fromX, fromY, toX, toY);
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
                Notation = notation,
            };
        }

        /// <summary>
        /// A flip (翻子): the piece turns face up and — on the game's first flip (nobody owns a colour
        /// yet) — the mover gets the flipped piece's colour and the other player the other one.
        /// </summary>
        public ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece)
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
        /// A move onto the face-down <paramref name="target"/> (暗吃, <see cref="Rules.CanCaptureHiddenPiece"/>):
        /// the target is turned face up, then
        /// <list type="bullet">
        /// <item>the mover's own piece: the mover stays on its from-square (<see cref="MoveKind.HiddenOwnPiece"/>);</item>
        /// <item>an enemy piece the mover may capture by the normal rules (re-checked now that it is
        /// face up — rank order and the Soldier/General pair; a Cannon's jump capture ignores rank):
        /// a normal capture (<see cref="MoveKind.HiddenCapture"/>);</item>
        /// <item>any other enemy piece: <see cref="Rules.IsCaptureHiddenPieceStrongerSuicide"/> on, the
        /// mover dies (<see cref="MoveKind.HiddenStrongerSuicide"/>); off, it returns to its from-square
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
            else if (DarkChessMoves.IsPseudoLegalMove(board, piece, toX, toY) && !DarkChessMoves.IsSuicideMove(board, piece, toX, toY))
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

        /// <summary>A 自殺 move: the mover dies and the face-up enemy piece it moved onto stays (<see cref="MoveKind.Suicide"/>).</summary>
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
        /// Once the factions are decided: a player with no piece left (face up or face down) loses
        /// (<see cref="GameOverReason.NoPiecesLeft"/>) — the opponent first, then the mover (whose
        /// last piece can die in a hidden capture); otherwise the opponent loses if it has no action
        /// on its turn (<see cref="GameOverReason.Stalemate"/>). Draw rules are not decided yet.
        /// </summary>
        public GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck)
        {
            opponentInCheck = false;
            var opponent = ActionResolver.OpponentOf(mover);

            // Nobody owns a piece before the factions are decided (every action decides them,
            // so this only guards against a custom position).
            if (!ActionResolver.FactionsDecided(board))
                return null;

            if (board.QueryPieces(side: opponent).Count == 0)
                return new GameEnd(mover, opponent, GameOverReason.NoPiecesLeft);
            if (board.QueryPieces(side: mover).Count == 0)
                return new GameEnd(opponent, mover, GameOverReason.NoPiecesLeft);
            if (!board.HasAnyAction(opponent))
                return new GameEnd(mover, opponent, GameOverReason.Stalemate);
            return null;
        }
    }
}
