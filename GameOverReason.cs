/* ----- ----- ----- ----- */
// GameOverReason.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/06
// Version: v1.3
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Why <see cref="GameManager.GameOver"/> was raised.
    /// </summary>
    public enum GameOverReason
    {
        /// <summary>The loser is in check and has no legal move (將死).</summary>
        Checkmate,

        /// <summary>
        /// The loser is not in check but has no legal move (困斃) — in xiangqi the
        /// stalemated side loses. Includes the case where every remaining move would
        /// leave the Generals facing each other. On the dark-chess board (HalfCenter, which has
        /// no check): the loser has pieces left but no action on its turn — no legal move and
        /// no face-down piece to flip (無法行動判負).
        /// </summary>
        Stalemate,

        /// <summary>The loser's total or step time ran out (see Rules.EndGameWhenTimesUp).</summary>
        TimeUp,

        /// <summary>The loser resigned (see <see cref="GameManager.Resign"/>).</summary>
        Resign,

        /// <summary>
        /// Dark chess (HalfCenter): the loser has no piece left on the board, face up or face
        /// down (all captured, or the last one died in a hidden capture, 吃光判負).
        /// </summary>
        NoPiecesLeft,

        /// <summary>三國 先得 200 分 (<see cref="HalfCrossWinCondition.FirstTo200"/>): the winner reached the points first.</summary>
        ScoreReached,
    }
}
