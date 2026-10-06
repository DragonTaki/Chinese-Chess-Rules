/* ----- ----- ----- ----- */
// Protocol.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using Chinese_Chess_v3.Game.Core.Pieces;

namespace Chinese_Chess_v3.Game.Core.RulesHost
{
    /// <summary>One request line (see docs/RULES-HOST.md): the fields every operation may use.</summary>
    public sealed class Request
    {
        public string Id { get; set; }
        public string Op { get; set; }
        public GameKind? Kind { get; set; }
        public JsonElement? Rules { get; set; }
        public PositionDto Position { get; set; }
        public HistoryDto History { get; set; }
        public ActionDto Action { get; set; }
        public int[] From { get; set; }
    }

    /// <summary>
    /// A position: whose turn it is (1 or 2; 三國 1..3), every piece on the board and, for 三國 only,
    /// the game's state beyond the pieces (<see cref="ThreeKingdomsDto"/>; absent otherwise).
    /// </summary>
    public sealed class PositionDto
    {
        public int ToMove { get; set; }
        public List<PieceDto> Pieces { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ThreeKingdomsDto ThreeKingdoms { get; set; }
    }

    /// <summary>
    /// 三國's state (<c>ThreeKingdomsState</c>), each array by player (Player1 first, three entries):
    /// the team each claimed (0 = none yet, 1..3), the points, whether it resigned (棄權), whether its
    /// clock ran out (it left), the order it went out in (0 = still in, 1 = first out...) and when it
    /// last scored (0 = never; 1 = the game's first score...: equal scores go to whoever was first).
    /// </summary>
    public sealed class ThreeKingdomsDto
    {
        public int[] Teams { get; set; }
        public int[] Scores { get; set; }
        public bool[] Resigned { get; set; }
        public bool[] TimedOut { get; set; }
        public int[] OutOrder { get; set; }
        public int[] ScoreOrder { get; set; }
    }

    /// <summary>One piece: type, colour, owner (0 = nobody yet, 1 / 2 = player; 三國 1..3), square and whether it is face up.</summary>
    public sealed class PieceDto
    {
        public PieceType Type { get; set; }
        public PieceColor Color { get; set; }
        public int Side { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool FaceUp { get; set; } = true;
    }

    /// <summary>The history the repetition / no-progress rules need (not used until those rules exist).</summary>
    public sealed class HistoryDto
    {
        public int PliesSinceCapture { get; set; }
        public List<MoveDto> RecentMoves { get; set; }
    }

    /// <summary>A move by squares.</summary>
    public sealed class MoveDto
    {
        public int[] From { get; set; }
        public int[] To { get; set; }
    }

    /// <summary>The action of a validate request: a move (from, to) or a flip (at).</summary>
    public sealed class ActionDto
    {
        public string Type { get; set; }
        public int[] From { get; set; }
        public int[] To { get; set; }
        public int[] At { get; set; }
    }

    /// <summary>A move's record in a validate response.</summary>
    public sealed class RecordDto
    {
        public MoveKind Kind { get; set; }
        public string Notation { get; set; }
        public PieceDto Captured { get; set; }
        public PieceDto Revealed { get; set; }
    }

    /// <summary>How the game ended: the winner (1 / 2; 三國 1..3; 0 for a draw), why and, 三國 only, every player's place (first first).</summary>
    public sealed class GameOverDto
    {
        public int Winner { get; set; }
        public GameOverReason Reason { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int[] Ranking { get; set; }
    }

    /// <summary>The JSON settings of the protocol: camelCase field names, enums by their C# name (Chariot, Red, Checkmate), unknown fields rejected.</summary>
    public static class ProtocolJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            // Chinese notation as is, not \uXXXX (the lines only go to the server, never into HTML).
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false), },
        };
    }
}
