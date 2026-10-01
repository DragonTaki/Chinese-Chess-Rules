/* ----- ----- ----- ----- */
// Board.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/09/30
// Version: v2.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Pieces.PieceTypes;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Boards
{
    public enum BoardType
    {
        Full,        // 大盤 9x10
        HalfCenter,  // 半盤 8x4
        HalfCross,   // 半盤三國 9x5
    }

    /// <summary>
    /// Represents the Chinese Chess board, managing all chess pieces, their positions, and interactions.
    /// This class provides methods to initialize, place, move, and remove pieces on the 9x10 board grid.
    /// </summary>
    public class Board
    {
        public BoardType Type { get; private set; }
        /// <summary>
        /// Two-dimensional array that represents the chessboard grid.
        /// Each cell stores a reference to the <see cref="Piece"/> currently occupying that position.
        /// </summary>
        public Piece[,] Grid { get; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }

        /// <summary>
        /// Tracks the current turn number of the game.
        /// Starts at 0 (before the first move).
        /// </summary>
        public int Turn { get; private set; } = 0;
        public Rules GameRules { get; }

        /// <summary>
        /// Internal list containing all active pieces on the board.
        /// This allows for quick iteration and management without traversing the grid.
        /// </summary>
        private List<Piece> pieces = new List<Piece>();

        /// <summary>
        /// The pieces the temporary move simulations in progress (<see cref="SimulateMove{T}"/>,
        /// used by <see cref="WouldMoveExposeOwnGeneral"/>) have "captured": still in
        /// <see cref="pieces"/> (the list is never modified during a simulation, so callers
        /// may be iterating it) but skipped by every lookup. A list, not a single field,
        /// because simulations nest (e.g. <see cref="BoardAnalysis"/> simulates a capture
        /// and then checks the legality of the recapture, which simulates again).
        /// </summary>
        private readonly List<Piece> simulatedCaptures = new List<Piece>();

        /// <summary>
        /// Whether <paramref name="piece"/> is hidden by a move simulation in progress (it
        /// has been "captured" there). Callers iterating <see cref="GetAllPieces"/> inside
        /// <see cref="SimulateMove{T}"/> must skip such pieces.
        /// </summary>
        internal bool IsSimulatedCapture(Piece piece) => simulatedCaptures.Contains(piece);

        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// Sets up the 9x10 grid layout and provides reference coordinate documentation.
        /// </summary>
        /// <param name="type">Board type (grid size).</param>
        /// <param name="rules">The rules this board plays by; null for the default <see cref="Rules"/>.</param>
        public Board(BoardType type = BoardType.Full, Rules rules = null)
        {
            Type = type;

            switch (Type)
            {
                case BoardType.Full:
                    Columns = BoardConstants.Full.Columns;
                    Rows = BoardConstants.Full.Rows;
                    break;

                case BoardType.HalfCenter:
                    Columns = BoardConstants.HalfCenter.Columns;
                    Rows = BoardConstants.HalfCenter.Rows;
                    break;

                case BoardType.HalfCross:
                    Columns = BoardConstants.HalfCross.Columns;
                    Rows = BoardConstants.HalfCross.Rows;
                    break;

                default:
                    Columns = BoardConstants.Full.Columns;
                    Rows = BoardConstants.Full.Rows;
                    break;
            }

            Grid = new Piece[Columns, Rows];
            GameRules = rules ?? new Rules();
        }

        /// <summary>
        /// Initializes the board to its starting state by placing all pieces
        /// according to the predefined positions in <see cref="PieceConstants.InitialPieces"/>.
        /// </summary>
        /// <param name="initialPieces">List of chess pieces to be placed</param>
        public void Initialize(List<PieceInfo> initialPieces)
        {
            Clear();

            // Load preset positions from PieceConstants
            foreach (var info in initialPieces)
            {
                var piece = CreatePieceFromInfo(info);  // Create piece instance based on piece info
                Grid[info.X, info.Y] = piece;           // Place the piece on the grid
                pieces.Add(piece);                      // Add to the active piece list
            }
        }

        public void Clear()
        {
            Array.Clear(Grid, 0, Grid.Length);
            pieces.Clear();

            ResetTurn();
        }

        /// <summary>
        /// Advances the turn counter by one step.
        /// </summary>
        public void AdvanceTurn()
        {
            Turn++;
        }

        /// <summary>
        /// Resets the turn counter back to 0.
        /// </summary>
        public void ResetTurn()
        {
            Turn = 0;
        }
        
        /// <summary>
        /// Creates a piece instance based on the provided <see cref="PieceInfo"/> configuration.
        /// </summary>
        /// <param name="info">The <see cref="PieceInfo"/> object containing type, position, and side data.</param>
        /// <returns>A newly created <see cref="Piece"/> instance corresponding to the given piece type.</returns>
        /// <exception cref="Exception">Thrown when an unknown piece type is encountered.</exception>
        private Piece CreatePieceFromInfo(PieceInfo info)
        {
            // Single source of truth for the type↔class mapping lives on
            // Piece itself — see Piece.Create, also used by 揭棋's
            // hidden-piece movement delegation.
            return Piece.Create(info);
        }

        /// <summary>
        /// Gets a list of all active pieces currently on the board.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of <see cref="Piece"/> objects representing all existing pieces.</returns>
        public List<Piece> GetAllPieces()
        {
            return pieces;
        }

        /// <summary>
        /// Retrieves the piece located at the specified board coordinates.
        /// </summary>
        /// <param name="x">The X-coordinate (column index) of the target cell.</param>
        /// <param name="y">The Y-coordinate (row index) of the target cell.</param>
        /// <returns>
        /// The <see cref="Piece"/> located at (x, y), or <c>null</c> if the position is out of bounds or empty.
        /// </returns>
        public Piece GetPiece(int x, int y)
        {
            if (x >= 0 && x < Columns && y >= 0 && y < Rows)
            {
                return Grid[x, y];
            }
            return null;
        }

        /// <summary>
        /// Places a piece at the specified position with given properties.
        /// </summary>
        /// <param name="x">X coordinate on the board</param>
        /// <param name="y">Y coordinate on the board</param>
        /// <param name="type">Piece type</param>
        /// <param name="side">Owning player side</param>
        /// <param name="color">Piece color</param>
        /// <param name="faceUp">Whether the piece is facing up</param>
        /// <returns>The created <see cref="Piece"/> instance</returns>
        public Piece PlacePiece(int x, int y, PieceType type, PlayerSide side, PieceColor color, bool faceUp = true)
        {
            if (!IsInBoard(x, y))
                throw new ArgumentOutOfRangeException(nameof(x), "Position is out of board bounds.");

            // 先移除原來的棋子（如果有）
            var existing = Grid[x, y];
            if (existing != null)
                pieces.Remove(existing);

            // 建立新的 PieceInfo
            var info = new PieceInfo(type, x, y, color, side, faceUp);

            // 建立對應 Piece 實例
            var piece = CreatePieceFromInfo(info);

            // 放置到棋盤格與列表
            Grid[x, y] = piece;
            pieces.Add(piece);

            return piece;
        }

        /// <summary>
        /// Moves a piece from one coordinate to another within the board grid.
        /// Updates both grid references and the piece’s internal coordinates.
        /// </summary>
        /// <param name="fromX">The source X-coordinate.</param>
        /// <param name="fromY">The source Y-coordinate.</param>
        /// <param name="toX">The destination X-coordinate.</param>
        /// <param name="toY">The destination Y-coordinate.</param>
        public bool MovePiece(int fromX, int fromY, int toX, int toY)
        {
            var piece = Grid[fromX, fromY];
            if (piece == null)
                return false;

            piece.UpdateState(Turn, x: toX, y: toY);
            Grid[toX, toY] = piece;
            Grid[fromX, fromY] = null;

            return true;
        }

        /// <summary>
        /// 嘗試翻開指定座標的棋子（僅翻開 FaceUp = false 的棋子）。
        /// </summary>
        /// <param name="x">棋盤 X 座標</param>
        /// <param name="y">棋盤 Y 座標</param>
        /// <returns>
        /// null：該座標沒有棋子
        /// false：棋子已翻開，無法翻開
        /// true：成功翻開
        /// </returns>
        public bool? FlapPiece(int x, int y)
        {
            var piece = GetPiece(x, y);
            if (piece == null)
                return null;

            if (piece.CurrentInfo.IsFaceUp)
                return false; // 已翻開，不能翻開

            // 使用 Piece 的 UpdateState 翻開棋子
            piece.UpdateState(Turn, isFaceUp: true);

            // Grid 已經引用該 Piece，無需額外放回
            return true;
        }

        /// <summary>
        /// Marks a piece as dead (captured or eliminated logically, but not removed from history).
        /// </summary>
        /// <param name="piece">The piece to mark as dead.</param>
        /// <returns>True if the operation succeeded, false if piece is null or already dead.</returns>
        public bool MarkPieceDead(int x, int y)
        {
            var piece = GetPiece(x, y);
            if (piece == null || piece.CurrentInfo.IsDead)
                return false;

            piece.UpdateState(Turn, isDead: true);

            return true;
        }

        /// <summary>
        /// Removes a piece from both the board grid and the internal piece list.
        /// </summary>
        /// <param name="x">The X-coordinate of the piece to remove.</param>
        /// <param name="y">The Y-coordinate of the piece to remove.</param>
        public bool RemovePiece(int x, int y)
        {
            var piece = Grid[x, y];

            if (piece == null)
                return false;

            piece.UpdateState(Turn, isDead: true);
            pieces.Remove(piece);
            Grid[x, y] = null;

            return true;
        }

        /// <summary>
        /// Determines whether the specified board coordinates lie within the valid playable area of the board.
        /// </summary>
        /// <param name="x">The X-coordinate (column index).</param>
        /// <param name="y">The Y-coordinate (row index).</param>
        /// <returns><c>true</c> if the coordinate is within the board; otherwise, <c>false</c>.</returns>
        public bool IsInBoard(int x, int y)
        {
            return x >= 0 && x <= Columns - 1 &&
                y >= 0 && y <= Rows - 1;
        }

        /// <summary>
        /// Determines whether a coordinate lies within the palace area for a given side.
        /// </summary>
        /// <param name="x">The X-coordinate of the target position.</param>
        /// <param name="y">The Y-coordinate of the target position.</param>
        /// <param name="side">The player side to check (Red or Black).</param>
        /// <returns>
        /// <c>true</c> if the position is inside the palace for the given side,
        /// or if the side is not recognized (treated as unrestricted); otherwise, <c>false</c>.
        /// </returns>
        public bool IsInPalace(PlayerSide side, int x, int y)
        {
            if (side == PlayerSide.Player1)
            {
                return x >= BoardConstants.Full.PalaceXRange.MinX &&
                       x <= BoardConstants.Full.PalaceXRange.MaxX &&
                       y >= BoardConstants.Full.RedPalaceYRange.MinY &&
                       y <= BoardConstants.Full.RedPalaceYRange.MaxY;
            }
            else if (side == PlayerSide.Player2)
            {
                return x >= BoardConstants.Full.PalaceXRange.MinX &&
                       x <= BoardConstants.Full.PalaceXRange.MaxX &&
                       y >= BoardConstants.Full.BlackPalaceYRange.MinY &&
                       y <= BoardConstants.Full.BlackPalaceYRange.MaxY;
            }
            else
            {
                // For non-standard or neutral sides, assume no restriction
                return true;
            }
        }

        /// <summary>
        /// Determines whether the given coordinate has crossed the river (from player's perspective).
        /// </summary>
        public bool IsPassRiver(PlayerSide side, int y)
        {
            if (side == PlayerSide.Player1)
                return y < BoardConstants.Full.RiverLineYRedSide;
            else if (side == PlayerSide.Player2)
                return y > BoardConstants.Full.RiverLineYBlackSide;
            else
                return true;
        }

        public bool? IsLocationSamePlayerSide(PlayerSide side, int x, int y)
        {
            var piece = GetPiece(x, y);
            if (piece == null)
                return null;

            if (side == piece.Side)
                return true;  // Same side

            return false;
        }

        /// <summary>
        /// Checks whether moving the piece at (fromX, fromY) to (toX, toY) would leave the
        /// two Generals facing each other on one column with nothing between them (王見王).
        /// Returns true if the move would cause face-to-face (illegal unless
        /// <see cref="Rules.CanGeneralSeeGeneral"/>), false otherwise.
        /// </summary>
        /// <remarks>
        /// The board after the move is evaluated without mutating it: the source square is
        /// treated as empty and the destination as occupied. Only a piece that is the last
        /// blocker between the Generals and actually leaves the segment between them
        /// exposes them — a piece behind a General, or one that stays between them, does
        /// not. If either side does not have exactly one General (e.g. one was captured, or
        /// a custom layout), there is nothing to face, so this returns false instead of
        /// throwing.
        /// </remarks>
        public bool IsGeneralFaceToFaceAfterMove(int fromX, int fromY, int toX, int toY)
        {
            var redGenerals = QueryPieces(type: PieceType.General, side: PlayerSide.Player1);
            var blackGenerals = QueryPieces(type: PieceType.General,  side: PlayerSide.Player2);

            if (redGenerals.Count != 1 || blackGenerals.Count != 1)
                return false;

            (int x, int y) red = (redGenerals[0].X, redGenerals[0].Y);
            (int x, int y) black = (blackGenerals[0].X, blackGenerals[0].Y);

            // Capturing a General leaves no pair to face.
            if ((toX, toY) == red || (toX, toY) == black)
                return false;

            // A moving General is at its destination afterwards.
            if ((fromX, fromY) == red)
                red = (toX, toY);
            else if ((fromX, fromY) == black)
                black = (toX, toY);

            if (red.x != black.x)
                return false;

            int x = red.x;
            int yMin = Math.Min(red.y, black.y) + 1;
            int yMax = Math.Max(red.y, black.y);

            for (int y = yMin; y < yMax; y++)
            {
                bool occupied = (x == toX && y == toY)
                    || ((x != fromX || y != fromY) && Grid[x, y] != null);
                if (occupied)
                    return false;  // At least one blocker remains
            }

            return true;  // Nothing between → face to face
        }

        /// <summary>
        /// Checks if moving a piece at (targetX, targetY) is legal regarding the face-to-face rule.
        /// The check scans vertically in the direction of the opponent's side.
        /// </summary>
        /// <param name="side">The side of the moving piece.</param>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <returns>True if the move does NOT cause generals to face each other; otherwise, false.</returns>
        public bool IsGeneralTargetLegal(PlayerSide side, int targetX, int targetY)
        {
            // Determine scanning direction based on side
            int step = side switch
            {
                PlayerSide.Player1   => -1,  // Red scans upwards (Y--)
                PlayerSide.Player2 =>  1,  // Black scans downwards (Y++)
                _ => throw new Exception("Unknown side type")
            };

            int y = targetY + step;

            while (y >= 0 && y < Rows)
            {
                var piece = Grid[targetX, y];
                if (piece != null)
                {
                    if (piece.Type == PieceType.General && piece.Side != side)
                        return false;  // 遇到對方將帥 → 不合法
                    else
                        return true;  // 遇到其他棋子 → 不阻擋王見王規則，停止掃描
                }

                y += step;
            }

            return true;  // 沿線沒遇到對方將帥 → 合法
        }

        /// <summary>
        /// Whether this board plays by xiangqi check rules (self-check and facing Generals
        /// make a move illegal; no legal move loses). Only the Full board (standard
        /// xiangqi, and 揭棋 which shares it); the dark-chess boards have no such rule.
        /// </summary>
        public bool UsesCheckRules => Type == BoardType.Full;

        /// <summary>
        /// The General of <paramref name="side"/>, or null unless that side has exactly one
        /// (none on a custom layout, or more than one on a malformed one).
        /// </summary>
        public Piece GetGeneral(PlayerSide side)
        {
            Piece found = null;
            foreach (var p in pieces)
            {
                if (IsSimulatedCapture(p) || p.Type != PieceType.General || p.Side != side)
                    continue;
                if (found != null)
                    return null;
                found = p;
            }
            return found;
        }

        /// <summary>
        /// Whether the two Generals currently stand on one column with nothing between
        /// them (王見王). False if either side lacks exactly one General.
        /// </summary>
        public bool AreGeneralsFacing()
        {
            var red = GetGeneral(PlayerSide.Player1);
            var black = GetGeneral(PlayerSide.Player2);
            if (red == null || black == null || red.X != black.X)
                return false;

            int yMin = Math.Min(red.Y, black.Y) + 1;
            int yMax = Math.Max(red.Y, black.Y);
            for (int y = yMin; y < yMax; y++)
            {
                if (Grid[red.X, y] != null)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Whether <paramref name="side"/>'s General is attacked: some opposing piece could
        /// capture it by its own movement rules (<see cref="Piece.IsPseudoLegalMove"/>).
        /// A pinned attacker still gives check, as in standard xiangqi. False if the side
        /// has no (single) General.
        /// </summary>
        public bool IsSideInCheck(PlayerSide side) => GetCheckingPieces(side).Count > 0;

        /// <summary>
        /// The opposing pieces currently attacking <paramref name="side"/>'s General (empty
        /// when not in check). Used for the game-over record of a checkmate.
        /// </summary>
        public List<Piece> GetCheckingPieces(PlayerSide side)
        {
            var result = new List<Piece>();
            var general = GetGeneral(side);
            if (general == null)
                return result;

            foreach (var p in pieces)
            {
                if (!IsSimulatedCapture(p) && p.Side != side && p.IsPseudoLegalMove(this, general.X, general.Y))
                    result.Add(p);
            }
            return result;
        }

        /// <summary>
        /// Whether moving <paramref name="piece"/> to (toX, toY) would leave its own General
        /// attacked, or leave the two Generals facing each other (unless
        /// <see cref="Rules.CanGeneralSeeGeneral"/>). Assumes the move is otherwise legal
        /// for the piece's movement rules.
        /// </summary>
        /// <remarks>
        /// Evaluated on the board after the move, via <see cref="SimulateMove{T}"/>.
        /// </remarks>
        public bool WouldMoveExposeOwnGeneral(Piece piece, int toX, int toY)
        {
            return SimulateMove(piece, toX, toY, () =>
            {
                if (!GameRules.CanGeneralSeeGeneral && AreGeneralsFacing())
                    return true;
                return IsSideInCheck(piece.Side);
            }, fallback: false);
        }

        /// <summary>
        /// Evaluates <paramref name="evaluate"/> on the board as it would be after moving
        /// <paramref name="piece"/> to (toX, toY), then restores the board. Returns
        /// <paramref name="fallback"/> without evaluating if the destination is off the board
        /// or the piece is not on its own square. Does not check the move's legality.
        /// </summary>
        /// <remarks>
        /// The move is applied temporarily to the grid and the moving piece's coordinates
        /// (without a history snapshot), and a captured piece is hidden from every lookup
        /// (<see cref="simulatedCaptures"/>), then everything is restored — so every piece's
        /// own rule code sees a consistent board. The piece list itself is not modified,
        /// so this is safe to call while iterating <see cref="GetAllPieces"/> (skip pieces
        /// for which <see cref="IsSimulatedCapture"/> is true). Simulations may nest.
        /// </remarks>
        internal T SimulateMove<T>(Piece piece, int toX, int toY, Func<T> evaluate, T fallback)
        {
            int fromX = piece.X;
            int fromY = piece.Y;
            if (!IsInBoard(toX, toY) || Grid[fromX, fromY] != piece)
                return fallback;

            var captured = Grid[toX, toY];
            var originalInfo = piece.CurrentInfo;

            if (captured != null)
                simulatedCaptures.Add(captured);
            Grid[fromX, fromY] = null;
            Grid[toX, toY] = piece;
            piece.SetInfoWithoutHistory(new PieceInfo(
                originalInfo.Type, toX, toY, originalInfo.Color, originalInfo.Side,
                originalInfo.IsFaceUp, originalInfo.IsDead, originalInfo.TurnIndex));

            try
            {
                return evaluate();
            }
            finally
            {
                piece.SetInfoWithoutHistory(originalInfo);
                Grid[fromX, fromY] = piece;
                Grid[toX, toY] = captured;
                if (captured != null)
                    simulatedCaptures.RemoveAt(simulatedCaptures.LastIndexOf(captured));
            }
        }

        /// <summary>
        /// Whether <paramref name="side"/> has at least one legal move (see
        /// <see cref="Piece.GetLegalMoves"/>). Stops at the first one found.
        /// </summary>
        public bool HasAnyLegalMove(PlayerSide side)
        {
            foreach (var p in pieces)
            {
                if (p.Side != side)
                    continue;
                foreach (var (x, y) in p.GetPseudoLegalMoves(this))
                {
                    if (!UsesCheckRules || !WouldMoveExposeOwnGeneral(p, x, y))
                        return true;
                }
            }
            return false;
        }

        public List<Piece> QueryPieces(
            PieceType? type = null,
            PlayerSide? side = null,
            bool? isAlive = null,
            bool? isFaceUp = null)
        {
            return pieces.Where(p =>
                !IsSimulatedCapture(p) &&
                (!type.HasValue || p.Type == type.Value) &&
                (!side.HasValue || p.Side == side.Value) &&
                (!isAlive.HasValue || (isAlive.Value ? !p.CurrentInfo.IsDead : p.CurrentInfo.IsDead)) &&
                (!isFaceUp.HasValue || p.CurrentInfo.IsFaceUp == isFaceUp.Value)
            ).ToList();
        }
    }
}
