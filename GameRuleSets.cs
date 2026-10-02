/* ----- ----- ----- ----- */
// GameRuleSets.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/02
// Update Date: 2026/10/02
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// One <see cref="Rules"/> object per <see cref="GameKind"/>: the rules a new game of that
    /// kind starts with (<see cref="GameManager.DefaultRuleSets"/>). Each kind's object is its
    /// own (editing one kind's rules never changes another's).
    /// </summary>
    public sealed class GameRuleSets
    {
        private readonly Dictionary<GameKind, Rules> _sets = new();

        /// <summary>Every kind with the default <see cref="Rules"/>.</summary>
        public GameRuleSets() : this(null) { }

        /// <summary>
        /// Every kind with its own copy of <paramref name="rules"/> (null: the default
        /// <see cref="Rules"/>); <paramref name="rules"/> itself is not kept.
        /// </summary>
        public GameRuleSets(Rules rules)
        {
            foreach (var kind in Enum.GetValues<GameKind>())
                _sets[kind] = rules?.Clone() ?? new Rules();
        }

        /// <summary>The rules new games of <paramref name="kind"/> start with (the stored object itself, not a copy).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a <see cref="GameKind"/>.</exception>
        public Rules this[GameKind kind] =>
            _sets.TryGetValue(kind, out var rules)
                ? rules
                : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown game kind");

        /// <summary>
        /// The kind whose rules a game set up on <paramref name="boardType"/> without a kind of
        /// its own uses (a custom position, a cleared board): Full = <see cref="GameKind.Traditional"/>,
        /// HalfCenter = <see cref="GameKind.DarkHalf"/>, HalfCross = <see cref="GameKind.ThreeKingdoms"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="boardType"/> is not a <see cref="Boards.BoardType"/>.</exception>
        public static GameKind KindFor(Boards.BoardType boardType) => boardType switch
        {
            Boards.BoardType.Full => GameKind.Traditional,
            Boards.BoardType.HalfCenter => GameKind.DarkHalf,
            Boards.BoardType.HalfCross => GameKind.ThreeKingdoms,
            _ => throw new ArgumentOutOfRangeException(nameof(boardType), boardType, "Unknown board type"),
        };
    }
}
