/* ----- ----- ----- ----- */
// XiangqiFamily.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Notation;
using Chinese_Chess_v3.Game.Core.Pgn;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families.Xiangqi
{
    /// <summary>
    /// The Full-board xiangqi family: standard xiangqi (傳統大盤) and 揭棋 (<see cref="Board.IsJieqi"/>:
    /// 15 pieces per side start face down on their own side's starting squares, a face-down piece
    /// moves as its square's piece and is turned face up by its first move). Check rules apply (a move
    /// may not leave the mover's General attacked or the Generals facing); the side to move with no
    /// legal move loses (checkmate when in check, otherwise stalemate 困斃). Movement: <see cref="XiangqiMoves"/>.
    /// </summary>
    internal sealed class XiangqiFamily : IRulesFamily
    {
        public bool UsesCheckRules => true;

        public bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY) =>
            XiangqiMoves.IsPseudoLegalMove(board, piece, targetX, targetY);

        public List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece) =>
            XiangqiMoves.GetPseudoLegalMoves(board, piece);

        public bool IsSuicideMove(Board board, Piece piece, int targetX, int targetY) => false;

        /// <summary>A player moves its own pieces (揭棋's face-down pieces too: they move as their square's type).</summary>
        public bool CanAct(Board board, Piece piece, PlayerSide mover) => piece != null && piece.Side == mover;

        /// <summary>Flipping is never an action of its own here.</summary>
        public bool CanFlip(Board board, Piece piece) => false;

        public ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY)
        {
            int fromX = piece.X;
            int fromY = piece.Y;
            var pieceBefore = piece.CurrentInfo.Clone();
            var target = board.GetPiece(toX, toY);

            // The notation also depends on the other pieces on the file (前/後), so it is taken
            // before the board changes. On a 揭棋
            // board a face-down piece is written as the piece it moves as (its true type is hidden).
            string notation = board.IsJieqi
                ? ChineseMoveNotation.Format(ConcealedPosition(board), fromX, fromY, toX, toY)
                : ChineseMoveNotation.Format(board, fromX, fromY, toX, toY);
            bool reveals = board.IsJieqi && !piece.CurrentInfo.IsFaceUp;
            string iccs = new IccsMove(fromX, fromY, toX, toY).ToString();

            var captured = target?.CurrentInfo.Clone();
            if (target != null)
                board.RemovePiece(toX, toY);
            board.MovePiece(fromX, fromY, toX, toY);
            // 揭棋: a face-down piece is turned face up as soon as it has moved.
            if (reveals)
                board.FlipPiece(toX, toY);
            // Checked on the board as it now is (a 揭棋 piece already shows its true type).
            bool givesCheck = board.IsSideInCheck(ActionResolver.OpponentOf(piece.Side));

            return new ActionOutcome
            {
                Kind = MoveKind.Move, Mover = mover,
                FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
                Piece = piece, PieceBefore = pieceBefore,
                CapturedPiece = target, Captured = captured,
                Revealed = reveals ? piece.CurrentInfo.Clone() : null,
                Notation = notation, Iccs = iccs, GivesCheck = givesCheck,
            };
        }

        /// <summary>The board's pieces with every face-down piece shown as the piece it moves as (its starting square's type).</summary>
        private static List<PieceInfo> ConcealedPosition(Board board)
        {
            var position = new List<PieceInfo>();
            foreach (var p in board.GetAllPieces())
            {
                var info = p.CurrentInfo;
                if (info.IsFaceUp)
                    position.Add(info);
                else
                    position.Add(new PieceInfo(XiangqiMoves.MovingType(board, p), info.X, info.Y, info.Color, info.Side,
                        isFaceUp: false, isDead: info.IsDead, turnIndex: info.TurnIndex));
            }
            return position;
        }

        public ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece) =>
            throw new System.InvalidOperationException("Flipping is not an action on the Full board");

        /// <summary>
        /// The opponent, about to move, loses on the spot when it has no legal move — checkmate if
        /// in check, otherwise stalemate (困斃, which also covers "every remaining move would face
        /// the Generals").
        /// </summary>
        public GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck)
        {
            var opponent = ActionResolver.OpponentOf(mover);
            opponentInCheck = board.IsSideInCheck(opponent);
            if (!board.HasAnyLegalMove(opponent))
                return new GameEnd(mover, opponent, opponentInCheck ? GameOverReason.Checkmate : GameOverReason.Stalemate);
            return null;
        }
    }
}
