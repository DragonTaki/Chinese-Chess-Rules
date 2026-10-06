/* ----- ----- ----- ----- */
// RulesFamilies.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Families.DarkChess;
using Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms;
using Chinese_Chess_v3.Game.Core.Families.Xiangqi;

namespace Chinese_Chess_v3.Game.Core.Families
{
    /// <summary>The rules family of each board (the families keep no state, so one instance each is shared).</summary>
    public static class RulesFamilies
    {
        public static readonly IRulesFamily Xiangqi = new XiangqiFamily();
        public static readonly IRulesFamily DarkChess = new DarkChessFamily();
        public static readonly IRulesFamily ThreeKingdoms = new ThreeKingdomsFamily();

        /// <summary>The family a board of <paramref name="type"/> plays by.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> is not a playable board type.</exception>
        public static IRulesFamily For(BoardType type) => type switch
        {
            BoardType.Full => Xiangqi,
            BoardType.HalfCenter => DarkChess,
            BoardType.HalfCross => ThreeKingdoms,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No rules family for this board type"),
        };
    }
}
