/* ----- ----- ----- ----- */
// ThreeKingdomsFamily.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// The Three Kingdoms family (三國暗棋, three players on 9×5 points; DARK-CHESS-RULES §1.2 and the
    /// author's decisions). Movement: <see cref="ThreeKingdomsMoves"/>.
    /// </summary>
    internal sealed class ThreeKingdomsFamily : IRulesFamily
    {
        public bool UsesCheckRules => false;

        public bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY) =>
            ThreeKingdomsMoves.IsPseudoLegalMove(board, piece, targetX, targetY);

        public List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece) =>
            ThreeKingdomsMoves.GetPseudoLegalMoves(board, piece);

        public bool IsSuicideMove(Board board, Piece piece, int targetX, int targetY) => false;

        /// <summary>A player moves the face-up pieces of the faction it claimed.</summary>
        public bool CanAct(Board board, Piece piece, PlayerSide mover) =>
            piece != null && mover != PlayerSide.None && piece.Side == mover && piece.CurrentInfo.IsFaceUp;

        /// <summary>Any face-down piece may be flipped by the player to move.</summary>
        public bool CanFlip(Board board, Piece piece) => piece != null && !piece.CurrentInfo.IsFaceUp;

        public ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY) =>
            throw new NotSupportedException("Three Kingdoms actions are not implemented yet");

        public ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece) =>
            throw new NotSupportedException("Three Kingdoms actions are not implemented yet");

        public GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck) =>
            throw new NotSupportedException("Three Kingdoms actions are not implemented yet");
    }
}
