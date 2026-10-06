/* ----- ----- ----- ----- */
// IRulesFamily.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Families
{
    /// <summary>
    /// One rule system ("family") of the five game kinds (author 2026-10-06): the Full-board
    /// xiangqi family (傳統, 揭棋), the half-board dark chess family (暗棋半盤, 明棋半盤) and Three
    /// Kingdoms (三國). Everything that differs between the families lives behind this interface,
    /// so adding or changing one family never touches another; the board, the pieces and
    /// <see cref="ActionResolver"/> only ask the board's family (<see cref="Board.Family"/>).
    /// </summary>
    public interface IRulesFamily
    {
        /// <summary>How many players the family's games have (2; 三國 3).</summary>
        int PlayerCount { get; }

        /// <summary>
        /// Who moves after <paramref name="mover"/>'s action (called only while the game goes on):
        /// the opponent in a two-player game; 三國 skips players that are out, resigned or have no action.
        /// </summary>
        PlayerSide NextToMove(Board board, PlayerSide mover);

        /// <summary>Whether xiangqi check rules apply (self-check and facing Generals are illegal, no legal move loses).</summary>
        bool UsesCheckRules { get; }

        /// <summary>Whether <paramref name="piece"/> may move to (targetX, targetY) by its own movement rules (ignoring self-check).</summary>
        bool IsPseudoLegalMove(Board board, Piece piece, int targetX, int targetY);

        /// <summary>Every square <paramref name="piece"/> may move to by its own movement rules (ignoring self-check).</summary>
        List<(int x, int y)> GetPseudoLegalMoves(Board board, Piece piece);

        /// <summary>Whether moving <paramref name="piece"/> onto (targetX, targetY) is a 自殺 (the mover dies, the target stays).</summary>
        bool IsSuicideMove(Board board, Piece piece, int targetX, int targetY);

        /// <summary>Whether <paramref name="mover"/> may select and move <paramref name="piece"/>.</summary>
        bool CanAct(Board board, Piece piece, PlayerSide mover);

        /// <summary>Whether <paramref name="piece"/> may be flipped as an action (a face-down piece on a board where flipping is a turn).</summary>
        bool CanFlip(Board board, Piece piece);

        /// <summary>Applies an already-validated move; the board's turn counter is the caller's business.</summary>
        ActionOutcome ApplyMove(Board board, PlayerSide mover, Piece piece, int toX, int toY);

        /// <summary>Applies an already-validated flip.</summary>
        ActionOutcome ApplyFlip(Board board, PlayerSide mover, Piece piece);

        /// <summary>Whether the game is over after <paramref name="mover"/>'s action (null while it goes on).</summary>
        /// <param name="opponentInCheck">Check-rules boards: whether the opponent is now in check; false otherwise.</param>
        GameEnd EvaluateEnd(Board board, PlayerSide mover, out bool opponentInCheck);
    }
}
