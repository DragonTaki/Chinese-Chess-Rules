/* ----- ----- ----- ----- */
// PlayerSide.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/14
// Update Date: 2026/09/24
// Version: v2.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core.Players
{
    /// <summary>
    /// Identifies which player/faction a piece or turn belongs to — deliberately
    /// player-number-based, not color-based (was <c>Red</c>/<c>Black</c>/<c>Yellow</c>
    /// until the author pointed out that naming ownership after a color is
    /// confusing once custom piece colors/patterns are configurable — a
    /// player's number never changes even if the color they're displayed as
    /// does. Visual color lives entirely in <c>Pieces.PieceColor</c>, which
    /// was already decoupled from this for exactly this reason.
    /// </summary>
    public enum PlayerSide
    {
        None,

        /// <summary>The Full board's traditional first side — displayed as red by default.</summary>
        Player1,

        /// <summary>The Full board's traditional second side — displayed as black by default.</summary>
        Player2,

        /// <summary>
        /// HalfCross's (三國半盤) third faction — confirmed by the author to
        /// be a real, independently hostile side, not an alliance with
        /// Player1 or Player2. Its own pieces are individually colored red
        /// or black (see <c>PieceInfo.Color</c>) because a physical board
        /// only has two piece colors to work with; on screen it can
        /// eventually get its own distinct default color/pattern instead —
        /// see <c>Rules.HalfCrossTeamSetup</c>.
        /// </summary>
        Player3,

        Neutral,
    }
}
