/* ----- ----- ----- ----- */
// GameOverInfo.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// One executed move, recorded as plain data (no live piece references).
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

        public MoveRecord(PieceInfo piece, int fromX, int fromY, int toX, int toY, PieceInfo captured)
        {
            Piece = piece;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            Captured = captured;
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
