/* ----- ----- ----- ----- */
// ThreeKingdomsTeamSplit.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms
{
    /// <summary>
    /// 三國's team split (自訂分隊, author 2026-10-06): the team (1..3) of every colour and piece type,
    /// each set separately (紅俥 and 黑俥 may be in different teams). Every team needs at least one
    /// piece (<see cref="IsValid"/>), and a team's threshold is the number of pieces it starts with
    /// (<see cref="PieceCount"/>; the default split's 帥將兵卒 has 12, the other two 10 — the
    /// author's 12 / 10). Immutable: <see cref="With"/> makes a changed copy. In the rules host's JSON
    /// an object by colour then type, e.g. <c>{"Red": {"General": 1, ...}, "Black": {...}}</c>.
    /// </summary>
    [JsonConverter(typeof(ThreeKingdomsTeamSplitJsonConverter))]
    public sealed class ThreeKingdomsTeamSplit : IEquatable<ThreeKingdomsTeamSplit>
    {
        /// <summary>The two colours, Red first.</summary>
        public static readonly PieceColor[] Colors = { PieceColor.Red, PieceColor.Black };

        /// <summary>The seven piece types in set order (General first).</summary>
        public static readonly PieceType[] Types = PieceConstants.HalfCenterPieceSet.Select(t => t.type).ToArray();

        // Index: colour (Red 0, Black 1) × 7 + the type's place in Types.
        private readonly int[] _teams;

        private ThreeKingdomsTeamSplit(int[] teams) => _teams = teams;

        /// <summary>
        /// The default split (DARK-CHESS-RULES §1.2): ① 帥將兵卒 (both Generals and all Soldiers),
        /// ② 仕相俥傌炮 (red's other pieces), ③ 士象車馬包 (black's other pieces).
        /// </summary>
        public static ThreeKingdomsTeamSplit Standard { get; } = new(Colors.SelectMany(color => Types.Select(type =>
            type is PieceType.General or PieceType.Soldier ? 1 : color == PieceColor.Red ? 2 : 3)).ToArray());

        /// <summary>The team (1..3) of <paramref name="color"/>'s pieces of <paramref name="type"/>.</summary>
        /// <exception cref="ArgumentException">Not a colour or type of a 三國 set.</exception>
        public int TeamOf(PieceColor color, PieceType type) => _teams[Index(color, type)];

        /// <summary>A copy with <paramref name="color"/>'s <paramref name="type"/> in <paramref name="team"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="team"/> is not 1..3.</exception>
        public ThreeKingdomsTeamSplit With(PieceColor color, PieceType type, int team)
        {
            if (team < 1 || team > 3)
                throw new ArgumentOutOfRangeException(nameof(team), team, "A team is 1..3");
            var teams = (int[])_teams.Clone();
            teams[Index(color, type)] = team;
            return new ThreeKingdomsTeamSplit(teams);
        }

        /// <summary>How many pieces <paramref name="team"/> starts with (its threshold).</summary>
        public int PieceCount(int team) =>
            Colors.Sum(color => PieceConstants.HalfCenterPieceSet.Where(t => TeamOf(color, t.type) == team).Sum(t => t.count));

        /// <summary>Whether every team has at least one piece (author 2026-10-06).</summary>
        public bool IsValid => PieceCount(1) > 0 && PieceCount(2) > 0 && PieceCount(3) > 0;

        /// <summary>The colours and types of <paramref name="team"/>'s pieces, by type (General first), red before black.</summary>
        public IEnumerable<(PieceColor color, PieceType type)> PiecesOf(int team) =>
            Types.SelectMany(type => Colors.Select(color => (color, type))).Where(p => TeamOf(p.color, p.type) == team);

        private static int Index(PieceColor color, PieceType type)
        {
            int c = Array.IndexOf(Colors, color);
            int t = Array.IndexOf(Types, type);
            if (c < 0 || t < 0)
                throw new ArgumentException($"No 三國 team for {color} {type}");
            return c * Types.Length + t;
        }

        public bool Equals(ThreeKingdomsTeamSplit other) => other != null && _teams.SequenceEqual(other._teams);
        public override bool Equals(object obj) => Equals(obj as ThreeKingdomsTeamSplit);
        public override int GetHashCode() => _teams.Aggregate(17, (h, t) => h * 31 + t);

        /// <summary>The split as colour → type → team (the JSON shape).</summary>
        public Dictionary<PieceColor, Dictionary<PieceType, int>> ToDictionary() =>
            Colors.ToDictionary(color => color, color => Types.ToDictionary(type => type, type => TeamOf(color, type)));

        /// <summary>
        /// The split of <paramref name="teams"/> (colour → type → team); every colour and type must be
        /// given, each team 1..3.
        /// </summary>
        /// <exception cref="ArgumentException">A colour or type is missing, or a team is not 1..3.</exception>
        public static ThreeKingdomsTeamSplit FromDictionary(IReadOnlyDictionary<PieceColor, Dictionary<PieceType, int>> teams)
        {
            var result = new int[Colors.Length * Types.Length];
            foreach (var color in Colors)
            {
                if (teams == null || !teams.TryGetValue(color, out var byType) || byType == null)
                    throw new ArgumentException($"The team split has no {color} pieces");
                if (byType.Count != Types.Length)
                    throw new ArgumentException($"The team split needs exactly the {Types.Length} {color} piece types");
                foreach (var type in Types)
                {
                    if (!byType.TryGetValue(type, out int team) || team < 1 || team > 3)
                        throw new ArgumentException($"The team of {color} {type} must be 1..3");
                    result[Index(color, type)] = team;
                }
            }
            if (teams.Count != Colors.Length)
                throw new ArgumentException("The team split has only Red and Black");
            return new ThreeKingdomsTeamSplit(result);
        }
    }

    /// <summary>The JSON of a <see cref="ThreeKingdomsTeamSplit"/>: colour → type → team.</summary>
    public sealed class ThreeKingdomsTeamSplitJsonConverter : JsonConverter<ThreeKingdomsTeamSplit>
    {
        public override ThreeKingdomsTeamSplit Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var teams = JsonSerializer.Deserialize<Dictionary<PieceColor, Dictionary<PieceType, int>>>(ref reader, options);
            try
            {
                return ThreeKingdomsTeamSplit.FromDictionary(teams);
            }
            catch (ArgumentException ex)
            {
                throw new JsonException(ex.Message);
            }
        }

        public override void Write(Utf8JsonWriter writer, ThreeKingdomsTeamSplit value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToDictionary(), options);
    }
}
