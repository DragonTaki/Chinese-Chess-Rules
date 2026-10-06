/* ----- ----- ----- ----- */
// ThreeKingdomsFamily.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// The Three Kingdoms family (三國暗棋, three players on 9×5 points; DARK-CHESS-RULES §1.2, the
    /// author's decisions win over the wiki). Player1 → Player2 → Player3 take turns; a turn is a
    /// flip of any face-down piece or a move of a face-up piece of one's own team. Flipping a piece of
    /// a team nobody claimed gives it to a flipper without a team; once two players have one, the
    /// third gets the remaining team. Captures score (<see cref="ThreeKingdomsTeams.Points"/>).
    /// A player whose team has no piece left is out; a resigned player (棄權) keeps its pieces on the
    /// board (they may still be captured and scored) and its turns are skipped, like a player with
    /// nothing to do. The game ends when fewer than two players are still playing (or nobody can
    /// act; 先得 200 分: when someone reaches 200) and the players are ranked
    /// (<see cref="ThreeKingdomsStandings.Rank"/>). The state lives on the board (<see cref="Board.ThreeKingdoms"/>).
    /// Movement: <see cref="ThreeKingdomsMoves"/>.
    /// </summary>
    internal sealed class ThreeKingdomsFamily : IRulesFamily
    {
        /// <summary>先得 200 分's target.</summary>
        public const int FirstToScore = 200;

        public int PlayerCount => 3;

        public bool UsesCheckRules => false;

        public bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY) =>
            ThreeKingdomsMoves.IsPseudoLegalMove(board, piece, targetX, targetY);

        public List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece) =>
            ThreeKingdomsMoves.GetPseudoLegalMoves(board, piece);

        public bool IsSuicideMove(Board board, Piece piece, int targetX, int targetY) => false;

        /// <summary>A player moves the face-up pieces of the team it claimed.</summary>
        public bool CanAct(Board board, Piece piece, PlayerSide mover) =>
            piece != null && mover != PlayerSide.None && piece.Side == mover && piece.CurrentInfo.IsFaceUp;

        /// <summary>Any face-down piece may be flipped by the player to move.</summary>
        public bool CanFlip(Board board, Piece piece) => piece != null && !piece.CurrentInfo.IsFaceUp;

        /// <summary>
        /// The next player after <paramref name="mover"/> in turn order who is still playing
        /// (<see cref="ThreeKingdomsStandings.IsPlaying"/>) and has an action; <paramref name="mover"/> again when nobody
        /// else has (author decision 9: a player with nothing to do is skipped).
        /// </summary>
        public PlayerSide NextToMove(Board board, PlayerSide mover)
        {
            int start = ThreeKingdomsState.Index(mover);
            for (int step = 1; step <= 3; step++)
            {
                var side = ThreeKingdomsState.SideOf((start - 1 + step) % 3 + 1);
                if (ThreeKingdomsStandings.IsPlaying(board, side) && board.HasAnyAction(side))
                    return side;
            }
            return mover;
        }

        /// <summary>A move; a capture scores the captured piece for the mover and may put its owner out.</summary>
        public ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY)
        {
            var state = StateOf(board);
            int fromX = piece.X;
            int fromY = piece.Y;
            var pieceBefore = piece.CurrentInfo.Clone();
            var target = board.GetPiece(toX, toY);
            var captured = target?.CurrentInfo.Clone();

            if (target != null)
            {
                board.RemovePiece(toX, toY);
                state.Scores[ThreeKingdomsState.Index(mover)] += ThreeKingdomsTeams.Points(target.Type, board.GameRules.HalfCrossWinCondition);
            }
            board.MovePiece(fromX, fromY, toX, toY);
            if (target != null)
                MarkOuts(board, state);

            return new ActionOutcome
            {
                Kind = MoveKind.Move, Mover = mover,
                FromX = fromX, FromY = fromY, ToX = toX, ToY = toY,
                Piece = piece, PieceBefore = pieceBefore,
                CapturedPiece = target, Captured = captured,
            };
        }

        /// <summary>
        /// A flip (翻子, a whole turn, author decision 3). The flipped piece's team becomes the
        /// mover's when neither has been claimed (author decision 1; a team someone else claimed
        /// decides nothing); once two players have a team the third gets the remaining one.
        /// </summary>
        public ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece)
        {
            var state = StateOf(board);
            var pieceBefore = piece.CurrentInfo.Clone();
            board.FlipPiece(piece.X, piece.Y);
            int team = ThreeKingdomsTeams.TeamOf(piece.Type, piece.Color);
            bool decides = state.Teams[ThreeKingdomsState.Index(mover)] == 0 && state.OwnerOf(team) == PlayerSide.None;
            if (decides)
            {
                Claim(board, state, mover, team);
                var unclaimedPlayers = ThreeKingdomsState.Players.Where(p => state.Teams[ThreeKingdomsState.Index(p)] == 0).ToList();
                if (unclaimedPlayers.Count == 1)
                {
                    int last = Enumerable.Range(1, 3).First(t => state.OwnerOf(t) == PlayerSide.None);
                    Claim(board, state, unclaimedPlayers[0], last);
                }
                MarkOuts(board, state);
            }

            return new ActionOutcome
            {
                Kind = MoveKind.Flip, Mover = mover,
                FromX = piece.X, FromY = piece.Y, ToX = piece.X, ToY = piece.Y,
                Piece = piece, PieceBefore = pieceBefore,
                Revealed = piece.CurrentInfo.Clone(),
                DecidesFactions = decides,
            };
        }

        /// <summary>
        /// The game is over when fewer than two players are still playing (author decision 8: a
        /// player whose pieces are all captured is out, a resigned one no longer plays), when nobody
        /// still playing can act, or (先得 200 分) when <paramref name="mover"/> reached
        /// <see cref="FirstToScore"/>. The winner is the first of <see cref="ThreeKingdomsStandings.Rank"/>, the loser the last.
        /// </summary>
        public GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck)
        {
            opponentInCheck = false;
            var state = StateOf(board);

            if (board.GameRules.HalfCrossWinCondition == HalfCrossWinCondition.FirstTo200
                && state.Scores[ThreeKingdomsState.Index(mover)] >= FirstToScore)
                return End(board, GameOverReason.ScoreReached);

            var playing = ThreeKingdomsState.Players.Where(p => ThreeKingdomsStandings.IsPlaying(board, p)).ToList();
            if (playing.Count < 2)
            {
                // Fewer than two players with pieces left, or else the others resigned.
                int withPieces = ThreeKingdomsState.Players.Count(p => !ThreeKingdomsStandings.IsOut(board, state, p));
                return End(board, withPieces < 2 ? GameOverReason.NoPiecesLeft : GameOverReason.Resign);
            }
            if (!playing.Any(board.HasAnyAction))
                return End(board, GameOverReason.Stalemate);
            return null;
        }

        private static GameEnd End(Board board, GameOverReason reason)
        {
            var ranking = ThreeKingdomsStandings.Rank(board);
            return new GameEnd(ranking[0], ranking[ranking.Count - 1], reason, ranking);
        }

        private static ThreeKingdomsState StateOf(Board board) =>
            board.ThreeKingdoms ?? throw new InvalidOperationException("A Three Kingdoms board has no ThreeKingdomsState");

        /// <summary>Gives <paramref name="team"/> to <paramref name="side"/>: every piece of it becomes the player's.</summary>
        private static void Claim(Board board, ThreeKingdomsState state, PlayerSide side, int team)
        {
            state.Teams[ThreeKingdomsState.Index(side)] = team;
            foreach (var p in board.GetAllPieces())
            {
                if (p.Side == PlayerSide.None && ThreeKingdomsTeams.TeamOf(p.Type, p.Color) == team)
                    p.UpdateState(board.Turn, side: side);
            }
        }

        /// <summary>Records the order in which the players that just went out did (for 全滅's ranking).</summary>
        private static void MarkOuts(Board board, ThreeKingdomsState state)
        {
            int order = state.OutOrder.Max() + 1;
            foreach (var side in ThreeKingdomsState.Players)
            {
                int i = ThreeKingdomsState.Index(side);
                if (state.OutOrder[i] == 0 && ThreeKingdomsStandings.IsOut(board, state, side))
                    state.OutOrder[i] = order;
            }
        }
    }
}
