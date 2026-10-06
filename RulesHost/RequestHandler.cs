/* ----- ----- ----- ----- */
// RequestHandler.cs
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
using System.Text.Json.Nodes;

using Chinese_Chess_v3.Game.Core.Boards;
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
                        : board.UsesDarkChessRules && !piece.CurrentInfo.IsFaceUp ? "FaceDownPiece"
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
                        !board.UsesDarkChessRules ? "CannotFlip"
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
                    ToMove = SideNumber(ActionResolver.OpponentOf(mover)),
                    Pieces = board.GetAllPieces().Select(p => ToDto(p.CurrentInfo)).ToList(),
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
                ["gameOver"] = end == null ? null : ToNode(new GameOverDto { Winner = SideNumber(end.Winner), Reason = end.Reason }),
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
            if (board.UsesDarkChessRules)
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
                GameKind.Traditional => (BoardType.Full, false),
                GameKind.DarkHalf => (BoardType.HalfCenter, true),
                GameKind.OpenHalf => (BoardType.HalfCenter, false),
                _ => throw new BadRequestException($"unsupported kind: {kind}"),
            };

            var rules = request.Rules.HasValue
                ? request.Rules.Value.Deserialize<Rules>(ProtocolJson.Options) ?? new Rules()
                : new Rules();
            if (boardType == BoardType.HalfCenter)
                rules.IsHiddenChess = hidden;

            var position = request.Position ?? throw new BadRequestException("missing position");
            if (position.ToMove != 1 && position.ToMove != 2)
                throw new BadRequestException("position.toMove must be 1 or 2");
            var pieces = position.Pieces ?? throw new BadRequestException("missing position.pieces");

            var board = new Board(boardType, rules);
            var infos = CheckPieces(pieces, board, boardType);
            board.Initialize(infos);
            return (board, position.ToMove == 1 ? PlayerSide.Player1 : PlayerSide.Player2);
        }

        /// <summary>
        /// Checks the pieces (the position comes from the server, but a server bug must not turn into
        /// a wrong ruling): squares on the board and unique; real piece types and colours; no colour
        /// with more pieces of a type than a full set; owners consistent with the colours (one owner
        /// per colour, the two colours owned by different players; on the dark-chess board all owners
        /// may still be undecided); on the Full board every piece face up and one General per colour.
        /// </summary>
        private static List<PieceInfo> CheckPieces(List<PieceDto> pieces, Board board, BoardType boardType)
        {
            var squares = new HashSet<(int, int)>();
            var counts = new Dictionary<(PieceColor, PieceType), int>();
            var owners = new Dictionary<PieceColor, int>();
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
                if (p.Side < 0 || p.Side > 2)
                    throw new BadRequestException($"bad side at ({p.X},{p.Y})");
                if (owners.TryGetValue(p.Color, out int owner) && owner != p.Side)
                    throw new BadRequestException($"{p.Color} pieces have different owners");
                owners[p.Color] = p.Side;
                if (boardType == BoardType.Full && !p.FaceUp)
                    throw new BadRequestException("face-down piece on the Full board");

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
            _ => PlayerSide.None,
        };

        private static int SideNumber(PlayerSide side) => side switch
        {
            PlayerSide.Player1 => 1,
            PlayerSide.Player2 => 2,
            _ => 0,
        };

        private static PieceDto ToDto(PieceInfo info) => new()
        {
            Type = info.Type, Color = info.Color, Side = SideNumber(info.Side), X = info.X, Y = info.Y, FaceUp = info.IsFaceUp,
        };

        private static JsonNode ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, ProtocolJson.Options);

        #endregion
    }
}
