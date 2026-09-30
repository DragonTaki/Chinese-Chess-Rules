/* ----- ----- ----- ----- */
// GameOverInfo.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// One executed move, recorded as plain data (no live piece references). The entries of
    /// <see cref="GameManager.Moves"/> are the game's move list - the base for PGN export,
    /// undo and replay: with the start position, <see cref="Piece"/> (from-square),
    /// the to-square and <see cref="Captured"/> a move can be replayed or taken back
    /// without the live board.
    /// </summary>
    public sealed class MoveRecord
    {
        /// <summary>The moving piece's state before the move (type, side, from-square).</summary>
        public PieceInfo Piece { get; }
        public int FromX { get; }
        public int FromY { get; }
        public int ToX { get; }
        public int ToY { get; }

        /// <summary>The piece captured on the destination, or null for a quiet move.</summary>
        public PieceInfo Captured { get; }

        /// <summary>The moving side (<c>Piece.Side</c>).</summary>
        public PlayerSide Side => Piece.Side;

        /// <summary>The moving piece's type (<c>Piece.Type</c>).</summary>
        public PieceType Type => Piece.Type;

        /// <summary>The moving piece's display color (<c>Piece.Color</c>).</summary>
        public PieceColor Color => Piece.Color;

        /// <summary>
        /// 1-based half-move index in the game (1 = the game's first move), so this record
        /// is <c>GameManager.Moves[Ply - 1]</c>. 0 for a record made outside a game.
        /// </summary>
        public int Ply { get; }

        /// <summary>
        /// Move number as in PGN / 第N手: a Red move and the Black reply share a number. When
        /// Black moves first (endgame), that first move is number 1 and the next Red move 2.
        /// 0 for a record made outside a game.
        /// </summary>
        public int MoveNumber { get; }

        /// <summary>Whether the move leaves the opponent in check (also true for a mating move). Only on boards that use check rules.</summary>
        public bool GivesCheck { get; }

        /// <summary>Standard Chinese notation (e.g. 炮二平五, see <c>ChineseMoveNotation</c>); null off the Full board.</summary>
        public string Notation { get; }

        /// <summary>ICCS coordinates, lower case without a dash (e.g. <c>h2e2</c>, see <c>IccsMove</c>); null off the Full board.</summary>
        public string Iccs { get; }

        public MoveRecord(
            PieceInfo piece, int fromX, int fromY, int toX, int toY, PieceInfo captured,
            int ply = 0, int moveNumber = 0, bool givesCheck = false, string notation = null, string iccs = null)
        {
            Piece = piece;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            Captured = captured;
            Ply = ply;
            MoveNumber = moveNumber;
            GivesCheck = givesCheck;
            Notation = notation;
            Iccs = iccs;
        }
    }

    /// <summary>
    /// Everything <see cref="GameManager.GameOver"/> reports about how a game ended.
    /// </summary>
    /// <remarks>
    /// Checkmate/stalemate results also carry the final position, the mating move and
    /// the pieces giving check, so a later classifier can recognise named mating patterns
    /// (側面虎, 雙車錯, 馬後炮, 困斃, ... — see docs/PLAN.md) and trigger special effects.
    /// No pattern detection is done here.
    /// </remarks>
    public sealed class GameOverInfo
    {
        public PlayerSide Winner { get; }
        public PlayerSide Loser { get; }
        public GameOverReason Reason { get; }

        /// <summary>Snapshot (cloned <see cref="PieceInfo"/>s) of every piece on the board when the game ended.</summary>
        public IReadOnlyList<PieceInfo> FinalBoard { get; }

        /// <summary>The last move played (the mating move for checkmate/stalemate); null if no move was made.</summary>
        public MoveRecord LastMove { get; }

        /// <summary>
        /// For <see cref="GameOverReason.Checkmate"/>: the winner's pieces attacking the
        /// loser's General in the final position. Empty for every other reason.
        /// </summary>
        public IReadOnlyList<PieceInfo> CheckingPieces { get; }

        public GameOverInfo(
            PlayerSide winner,
            PlayerSide loser,
            GameOverReason reason,
            IReadOnlyList<PieceInfo> finalBoard,
            MoveRecord lastMove,
            IReadOnlyList<PieceInfo> checkingPieces)
        {
            Winner = winner;
            Loser = loser;
            Reason = reason;
            FinalBoard = finalBoard;
            LastMove = lastMove;
            CheckingPieces = checkingPieces;
        }
    }
}
