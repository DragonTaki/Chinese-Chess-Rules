/* ----- ----- ----- ----- */
// TacticalEvent.cs
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
    /// Named tactical situations a single move can create (for sound / visual cues).
    /// Detected by <see cref="TacticalAnalysis.Analyze"/>. The declaration order is the
    /// order in which the events of one move are reported.
    /// </summary>
    public enum TacticalEventType
    {
        /// <summary>絕殺: the move checks and the opponent has no legal reply.</summary>
        Checkmate,

        /// <summary>困斃: the opponent is not in check and has no legal reply.</summary>
        Stalemate,

        /// <summary>雙將: two or more mover pieces give check.</summary>
        DoubleCheck,

        /// <summary>
        /// 閃將: the moved piece itself does not give check; a friendly piece does, because
        /// the moved piece left its line (chariot/cannon file or rank, or a horse leg).
        /// </summary>
        DiscoveredCheck,

        /// <summary>將軍: the move checks and the game goes on (not reported with <see cref="Checkmate"/>).</summary>
        Check,

        /// <summary>抽車: a check after which every legal reply still lets the mover capture a chariot.</summary>
        ForkingCheckChariot,

        /// <summary>抽炮: as <see cref="ForkingCheckChariot"/> for a cannon (only when no chariot qualifies).</summary>
        ForkingCheckCannon,

        /// <summary>抽馬: as <see cref="ForkingCheckChariot"/> for a horse (only when no chariot/cannon qualifies).</summary>
        ForkingCheckHorse,

        /// <summary>打死車: after every legal reply a mover cannon can still capture the chariot.</summary>
        TrappedChariot,

        /// <summary>吃車: the move captured a chariot.</summary>
        CaptureChariot,

        /// <summary>吃炮: the move captured a cannon.</summary>
        CaptureCannon,

        /// <summary>吃馬: the move captured a horse.</summary>
        CaptureHorse,

        /// <summary>捉雙: two or more opponent chariots/horses/cannons became hanging.</summary>
        DoubleAttack,

        /// <summary>
        /// 閃擊 (author's working name, definition may change): an opponent chariot the
        /// mover could not legally capture before the move can be legally captured now.
        /// </summary>
        ChariotThreat,
    }

    /// <summary>
    /// One tactical event of a move (see <see cref="TacticalEventType"/>), as plain data.
    /// </summary>
    /// <param name="Type">What happened.</param>
    /// <param name="Mover">The side that made the move.</param>
    /// <param name="Move">The move itself.</param>
    /// <param name="Pieces">
    /// The pieces involved, as snapshots of the position right after the move:
    /// checks - the checking pieces; forking checks - the opponent pieces of that type the
    /// mover can capture after at least one reply; 打死車 - the trapped chariot(s);
    /// captures - the captured piece; 捉雙 - the newly hanging pieces; 閃擊 - the newly
    /// threatened chariot(s); 困斃 - empty.
    /// </param>
    public sealed record TacticalEvent(
        TacticalEventType Type,
        PlayerSide Mover,
        MoveRecord Move,
        IReadOnlyList<PieceInfo> Pieces)
    {
        /// <summary>The Chinese name of <see cref="Type"/> (e.g. 抽車), for the game log.</summary>
        public string ChineseName => GetChineseName(Type);

        public static string GetChineseName(TacticalEventType type) => type switch
        {
            TacticalEventType.Checkmate => "絕殺",
            TacticalEventType.Stalemate => "困斃",
            TacticalEventType.DoubleCheck => "雙將",
            TacticalEventType.DiscoveredCheck => "閃將",
            TacticalEventType.Check => "將軍",
            TacticalEventType.ForkingCheckChariot => "抽車",
            TacticalEventType.ForkingCheckCannon => "抽炮",
            TacticalEventType.ForkingCheckHorse => "抽馬",
            TacticalEventType.TrappedChariot => "打死車",
            TacticalEventType.CaptureChariot => "吃車",
            TacticalEventType.CaptureCannon => "吃炮",
            TacticalEventType.CaptureHorse => "吃馬",
            TacticalEventType.DoubleAttack => "捉雙",
            TacticalEventType.ChariotThreat => "閃擊",
            _ => type.ToString(),
        };
    }
}
