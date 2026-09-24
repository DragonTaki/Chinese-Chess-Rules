/* ----- ----- ----- ----- */
// PlayerSide.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/14
// Update Date: 2025/05/14
// Version: v1.0
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core.Players
{
    public enum PlayerSide
    {
        None,
        Red,
        Black,

        /// <summary>
        /// The third faction in HalfCross (三國半盤) — confirmed by the
        /// author to be a real, independently hostile side, not an alliance
        /// with Red or Black. Its own pieces are individually colored Red
        /// or Black (see PieceInfo.Color, decoupled from ownership) because
        /// a physical board only has two piece colors to work with; on
        /// screen they can eventually get a third, genuinely distinct
        /// color/pattern instead — see Rules.HalfCrossTeamSetup.
        /// </summary>
        Yellow,

        Neutral,
    }
}