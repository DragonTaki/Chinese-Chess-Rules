/* ----- ----- ----- ----- */
// RequestHandler.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/06
// Update Date: 2026/10/06
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.RulesHost
{
    /// <summary>
    /// Answers one request line with one response line (docs/RULES-HOST.md). Keeps no state: every
    /// request carries the whole position, so requests are independent and can run in parallel.
    /// Never throws: a request that cannot be answered gets <c>ok: false</c> and the reason.
    /// </summary>
    public static class RequestHandler
    {
        /// <summary>A request that cannot be answered (bad format, unsupported kind, inconsistent position).</summary>
        private sealed class BadRequestException : Exception
        {
            public BadRequestException(string message) : base(message) { }
        }

        /// <summary>The response line to <paramref name="line"/>.</summary>
        public static string Handle(string line)
        {
            string id = null;
            try
            {
                var request = JsonSerializer.Deserialize<Request>(line, ProtocolJson.Options)
                    ?? throw new BadRequestException("empty request");
                id = request.Id;
                if (string.IsNullOrEmpty(id))
                    throw new BadRequestException("missing id");

                JsonObject result = request.Op switch
                {
                    "ping" => new JsonObject(),
                    "validate" => Validate(request),
                    "legalMoves" => LegalMoves(request),
                    _ => throw new BadRequestException($"unknown op: {request.Op}"),
                };
                result["id"] = id;
                result["ok"] = true;
                return Order(result).ToJsonString(ProtocolJson.Options);
            }
            catch (Exception ex) when (ex is BadRequestException || ex is JsonException || ex is NotSupportedException)
            {
                return Error(id, ex.Message);
            }
            catch (Exception ex)
            {
                // A bug, not a bad request: still answered, so the server is never left waiting.
                CoreLog.Log($"(RulesHost) Internal error on request {id}: {ex}", CoreLogLevel.Error);
                return Error(id, "internal error: " + ex.Message);
            }
        }

        private static string Error(string id, string message) =>
            new JsonObject { ["id"] = id, ["ok"] = false, ["error"] = message }.ToJsonString(ProtocolJson.Options);

        /// <summary>Puts id and ok first (easier to read in logs).</summary>
        private static JsonObject Order(JsonObject result)
        {
            var ordered = new JsonObject { ["id"] = result["id"]?.DeepClone(), ["ok"] = result["ok"]?.DeepClone() };
            foreach (var (key, value) in result)
            {
                if (key != "id" && key != "ok")
                    ordered[key] = value?.DeepClone();
            }
            return ordered;
        }

        #region Operations

        private static JsonObject Validate(Request request)
        {
            var (board, mover) = SetUp(request);
            ValidateHistory(request.History, board);
            var action = request.Action ?? throw new BadRequestException("missing action");

            ActionOutcome outcome;
            switch (action.Type)
            {
                case "move":
                {
                    var (fromX, fromY) = Square(action.From, board, "action.from");
                    var (toX, toY) = Square(action.To, board, "action.to");
                    var piece = board.GetPiece(fromX, fromY);
                    string reason =
                        piece == null ? "NoPieceThere"
                        : !board.IsJieqi && !piece.CurrentInfo.IsFaceUp ? "FaceDownPiece"
                        : !ActionResolver.CanAct(board, piece, mover) ? "NotYourPiece"
                        : !piece.CanMoveTo(board, toX, toY) ? "IllegalMove"
                        : null;
                    if (reason != null)
                        return Illegal(reason);
                    outcome = ActionResolver.ApplyMove(board, mover, piece, toX, toY);
                    break;
                }
                case "flip":
                {
                    var (x, y) = Square(action.At, board, "action.at");
                    var piece = board.GetPiece(x, y);
                    string reason =
                        board.Type == BoardType.Full ? "CannotFlip"
                        : piece == null ? "NoPieceThere"
                        : piece.CurrentInfo.IsFaceUp ? "AlreadyFaceUp"
                        : null;
                    if (reason != null)
                        return Illegal(reason);
                    outcome = ActionResolver.ApplyFlip(board, mover, piece);
                    break;
                }
                default:
                    throw new BadRequestException($"unknown action type: {action.Type}");
            }

            var end = ActionResolver.EvaluateEnd(board, mover, out bool opponentInCheck);
            return new JsonObject
            {
                ["legal"] = true,
                ["position"] = ToNode(new PositionDto
                {
                    // Whose turn it is once the game goes on (三國 skips players out, resigned or without an action).
                    ToMove = SideNumber(end == null ? ActionResolver.NextToMove(board, mover) : ActionResolver.OpponentOf(mover)),
                    Pieces = board.GetAllPieces().Select(p => ToDto(p.CurrentInfo)).ToList(),
                    ThreeKingdoms = board.ThreeKingdoms == null ? null : ToDto(board.ThreeKingdoms),
                }),
                ["record"] = ToNode(new RecordDto
                {
                    Kind = outcome.Kind,
                    Notation = outcome.Notation,
                    Captured = outcome.Captured == null ? null : ToDto(outcome.Captured),
                    Revealed = outcome.Revealed == null ? null : ToDto(outcome.Revealed),
                }),
                ["factionsDecided"] = outcome.DecidesFactions,
                ["check"] = opponentInCheck,
                ["gameOver"] = end == null ? null : ToNode(new GameOverDto
                {
                    Winner = SideNumber(end.Winner), Reason = end.Reason,
                    Ranking = end.Ranking?.Select(SideNumber).ToArray(),
                }),
            };
        }

        private static JsonObject Illegal(string reason) => new() { ["legal"] = false, ["reason"] = reason };

        private static JsonObject LegalMoves(Request request)
        {
            var (board, mover) = SetUp(request);
            (int x, int y)? only = request.From == null ? null : Square(request.From, board, "from");

            var moves = new JsonArray();
            foreach (var piece in board.GetAllPieces())
            {
                if (only.HasValue && (piece.X, piece.Y) != only.Value)
                    continue;
                if (!ActionResolver.CanAct(board, piece, mover))
                    continue;
                foreach (var (x, y) in piece.GetLegalMoves(board))
                    moves.Add(new JsonObject { ["from"] = new JsonArray(piece.X, piece.Y), ["to"] = new JsonArray(x, y) });
            }

            var flips = new JsonArray();
            if (board.Type != BoardType.Full)
            {
                foreach (var piece in board.GetAllPieces())
                {
                    if (only.HasValue && (piece.X, piece.Y) != only.Value)
                        continue;
                    if (!piece.CurrentInfo.IsFaceUp)
                        flips.Add(new JsonArray(piece.X, piece.Y));
                }
            }

            return new JsonObject { ["moves"] = moves, ["flips"] = flips };
        }

        #endregion

        #region Setting up and checking the input

        /// <summary>The board of the request's kind, rules and position, and the side to move.</summary>
        private static (Board board, PlayerSide mover) SetUp(Request request)
        {
            var kind = request.Kind ?? throw new BadRequestException("missing kind");
            var (boardType, hidden) = kind switch
            {
                GameKind.Traditional or GameKind.Flip => (BoardType.Full, false),
                GameKind.DarkHalf => (BoardType.HalfCenter, true),
                GameKind.OpenHalf => (BoardType.HalfCenter, false),
                GameKind.ThreeKingdoms => (BoardType.HalfCross, true),
                _ => throw new BadRequestException($"unsupported kind: {kind}"),
            };
            bool jieqi = kind == GameKind.Flip;

            var rules = request.Rules.HasValue
                ? request.Rules.Value.Deserialize<Rules>(ProtocolJson.Options) ?? new Rules()
                : new Rules();
            if (boardType == BoardType.HalfCenter)
                rules.IsHiddenChess = hidden;

            var position = request.Position ?? throw new BadRequestException("missing position");
            bool threeKingdoms = boardType == BoardType.HalfCross;
            int players = threeKingdoms ? 3 : 2;
            if (position.ToMove < 1 || position.ToMove > players)
                throw new BadRequestException($"position.toMove must be 1..{players}");
            var pieces = position.Pieces ?? throw new BadRequestException("missing position.pieces");
            if (!threeKingdoms && position.ThreeKingdoms != null)
                throw new BadRequestException("position.threeKingdoms is only for ThreeKingdoms");

            var board = new Board(boardType, rules, isJieqi: jieqi);
            var infos = CheckPieces(pieces, board, boardType, jieqi);
            board.Initialize(infos);
            var mover = SideOf(position.ToMove);
            if (threeKingdoms)
            {
                board.ThreeKingdoms = CheckThreeKingdoms(position.ThreeKingdoms, infos, rules);
                if (!ThreeKingdomsStandings.IsPlaying(board, mover))
                    throw new BadRequestException("position.toMove is out or resigned");
            }
            return (board, mover);
        }

        /// <summary>
        /// 三國's state, checked against the pieces: three entries per array; teams 0..3, each claimed
        /// by one player at most, never just one player without a team (the third gets the last team);
        /// every piece of a claimed team owned by its player and every other piece by nobody; scores
        /// not negative; out orders 0..3.
        /// </summary>
        private static ThreeKingdomsState CheckThreeKingdoms(ThreeKingdomsDto dto, List<PieceInfo> pieces, Rules rules)
        {
            if (dto == null)
                throw new BadRequestException("missing position.threeKingdoms");
            if (dto.Teams?.Length != 3 || dto.Scores?.Length != 3 || dto.Resigned?.Length != 3 || dto.OutOrder?.Length != 3)
                throw new BadRequestException("position.threeKingdoms arrays need 3 entries");

            var state = new ThreeKingdomsState();
            for (int i = 0; i < 3; i++)
            {
                if (dto.Teams[i] < 0 || dto.Teams[i] > 3)
                    throw new BadRequestException("position.threeKingdoms.teams must be 0..3");
                if (dto.Teams[i] != 0 && Array.IndexOf(dto.Teams, dto.Teams[i]) != i)
                    throw new BadRequestException("a team is claimed twice");
                if (dto.Scores[i] < 0)
                    throw new BadRequestException("position.threeKingdoms.scores cannot be negative");
                if (dto.OutOrder[i] < 0 || dto.OutOrder[i] > 3)
                    throw new BadRequestException("position.threeKingdoms.outOrder must be 0..3");
                state.Teams[i + 1] = dto.Teams[i];
                state.Scores[i + 1] = dto.Scores[i];
                state.Resigned[i + 1] = dto.Resigned[i];
                state.OutOrder[i + 1] = dto.OutOrder[i];
            }
            if (dto.Teams.Count(t => t == 0) == 1)
                throw new BadRequestException("two players have teams but the third has none");

            foreach (var p in pieces)
            {
                int team = ThreeKingdomsTeams.TeamOf(p.Type, p.Color);
                if (p.Side != state.OwnerOf(team))
                    throw new BadRequestException($"the piece on ({p.X},{p.Y}) does not belong to its team's owner");
            }
            return state;
        }

        /// <summary>
        /// Checks the pieces (the position comes from the server, but a server bug must not turn into
        /// a wrong ruling): squares on the board and unique; real piece types and colours; no colour
        /// with more pieces of a type than a full set; owners consistent with the colours (one owner
        /// per colour, the two colours owned by different players; on the dark-chess board all owners
        /// may still be undecided); on the Full board one General per colour, face up, and other pieces
        /// face down only in 揭棋.
        /// </summary>
        private static List<PieceInfo> CheckPieces(List<PieceDto> pieces, Board board, BoardType boardType, bool jieqi)
        {
            var squares = new HashSet<(int, int)>();
            var counts = new Dictionary<(PieceColor, PieceType), int>();
            var owners = new Dictionary<PieceColor, int>();
            // 三國's owners follow the teams, not the colours (CheckThreeKingdoms).
            bool byColour = boardType != BoardType.HalfCross;
            int maxSide = byColour ? 2 : 3;
            var infos = new List<PieceInfo>(pieces.Count);

            foreach (var p in pieces)
            {
                if (p == null)
                    throw new BadRequestException("null piece");
                if (p.X < 0 || p.X >= board.Columns || p.Y < 0 || p.Y >= board.Rows)
                    throw new BadRequestException($"piece off the board at ({p.X},{p.Y})");
                if (!squares.Add((p.X, p.Y)))
                    throw new BadRequestException($"two pieces on ({p.X},{p.Y})");
                if (p.Color != PieceColor.Red && p.Color != PieceColor.Black)
                    throw new BadRequestException($"bad piece colour at ({p.X},{p.Y})");
                int max = PieceConstants.HalfCenterPieceSet.Where(t => t.type == p.Type).Select(t => t.count).FirstOrDefault();
                if (max == 0)
                    throw new BadRequestException($"bad piece type at ({p.X},{p.Y})");
                counts[(p.Color, p.Type)] = counts.GetValueOrDefault((p.Color, p.Type)) + 1;
                if (counts[(p.Color, p.Type)] > max)
                    throw new BadRequestException($"too many {p.Color} {p.Type}");
                if (p.Side < 0 || p.Side > maxSide)
                    throw new BadRequestException($"bad side at ({p.X},{p.Y})");
                if (byColour)
                {
                    if (owners.TryGetValue(p.Color, out int owner) && owner != p.Side)
                        throw new BadRequestException($"{p.Color} pieces have different owners");
                    owners[p.Color] = p.Side;
                }
                // Face-down pieces exist on the Full board only in 揭棋, and never a General.
                if (boardType == BoardType.Full && !p.FaceUp && (!jieqi || p.Type == PieceType.General))
                    throw new BadRequestException("face-down piece not allowed here");

                infos.Add(new PieceInfo(p.Type, p.X, p.Y, p.Color, SideOf(p.Side), isFaceUp: p.FaceUp));
            }

            bool undecided = owners.Values.All(s => s == 0);
            if (boardType == BoardType.Full || !undecided)
            {
                if (owners.Values.Any(s => s == 0))
                    throw new BadRequestException("a piece has no owner");
                if (owners.Count == 2 && owners[PieceColor.Red] == owners[PieceColor.Black])
                    throw new BadRequestException("both colours have the same owner");
            }
            if (boardType == BoardType.Full)
            {
                foreach (var color in new[] { PieceColor.Red, PieceColor.Black })
                {
                    if (counts.GetValueOrDefault((color, PieceType.General)) != 1)
                        throw new BadRequestException($"the Full board needs one {color} General");
                }
            }
            return infos;
        }

        /// <summary>Checks the history's shape (the rules that use it are not implemented yet).</summary>
        private static void ValidateHistory(HistoryDto history, Board board)
        {
            if (history == null)
                return;
            if (history.PliesSinceCapture < 0)
                throw new BadRequestException("history.pliesSinceCapture cannot be negative");
            foreach (var move in history.RecentMoves ?? new List<MoveDto>())
            {
                Square(move?.From, board, "history.recentMoves.from");
                Square(move?.To, board, "history.recentMoves.to");
            }
        }

        private static (int x, int y) Square(int[] square, Board board, string field)
        {
            if (square == null || square.Length != 2)
                throw new BadRequestException($"{field} must be [x, y]");
            if (square[0] < 0 || square[0] >= board.Columns || square[1] < 0 || square[1] >= board.Rows)
                throw new BadRequestException($"{field} is off the board");
            return (square[0], square[1]);
        }

        #endregion

        #region Conversions

        private static PlayerSide SideOf(int side) => side switch
        {
            1 => PlayerSide.Player1,
            2 => PlayerSide.Player2,
            3 => PlayerSide.Player3,
            _ => PlayerSide.None,
        };

        private static int SideNumber(PlayerSide side) => side switch
        {
            PlayerSide.Player1 => 1,
            PlayerSide.Player2 => 2,
            PlayerSide.Player3 => 3,
            _ => 0,
        };

        private static ThreeKingdomsDto ToDto(ThreeKingdomsState state) => new()
        {
            Teams = state.Teams[1..],
            Scores = state.Scores[1..],
            Resigned = state.Resigned[1..],
            OutOrder = state.OutOrder[1..],
        };

        private static PieceDto ToDto(PieceInfo info) => new()
        {
            Type = info.Type, Color = info.Color, Side = SideNumber(info.Side), X = info.X, Y = info.Y, FaceUp = info.IsFaceUp,
        };

        private static JsonNode ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, ProtocolJson.Options);

        #endregion
    }
}
