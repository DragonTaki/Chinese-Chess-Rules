/* ----- ----- ----- ----- */
// Board.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/29
// Version: v2.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Constants.UI;
using Chinese_Chess_v3.Game.Constants.Game;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Models;
using System.Linq;

namespace Chinese_Chess_v3.Game.Core
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
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// Sets up the 9x10 grid layout and provides reference coordinate documentation.
        /// </summary>
        public Board(BoardType type = BoardType.Full)
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
            PlayerSide side = info.Side;

            // Determine which piece class to instantiate based on type
            return info.Type switch
            {
                PieceType.General   => new General(info.X, info.Y, side),
                PieceType.Advisor   => new Advisor(info.X, info.Y, side),
                PieceType.Elephant  => new Elephant(info.X, info.Y, side),
                PieceType.Horse     => new Horse(info.X, info.Y, side),
                PieceType.Chariot   => new Chariot(info.X, info.Y, side),
                PieceType.Cannon    => new Cannon(info.X, info.Y, side),
                PieceType.Soldier   => new Soldier(info.X, info.Y, side),
                _ => throw new Exception("Unknown piece type"),  // Defensive check
            };
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
        /// Determine whether the pixel coordinates are within the board range
        /// </summary>
        public bool IsWithinBoard(float x, float y)
        {
            float boardX = UILayoutConstants.Board.Grid.Position.X;
            float boardY = UILayoutConstants.Board.Grid.Position.Y;
            float gridSize = UILayoutConstants.Board.Grid.CellSize;
            return x >= boardX && x <= boardX + Columns * gridSize &&
                   y >= boardY && y <= boardY + Rows * gridSize;
        }

        /// <summary>
        /// Convert pixel coordinates within the board area to board coordinates
        /// </summary>
        /// <param name="pixelX">Mouse X coordinate</param>
        /// <param name="pixelY">Mouse Y coordinate</param>
        /// <param name="gridX">Grid coordinate X</param>
        /// <param name="gridY">Grid coordinate Y</param>
        public void PixelToGrid(float pixelX, float pixelY, out int gridX, out int gridY)
        {
            gridX = (int)((pixelX - UILayoutConstants.Board.Grid.Position.X) / UILayoutConstants.Board.Grid.CellSize + 0.5f);
            gridY = (int)((pixelY - UILayoutConstants.Board.Grid.Position.Y) / UILayoutConstants.Board.Grid.CellSize + 0.5f);

            gridX = Math.Clamp(gridX, 0, Columns - 1);
            gridY = Math.Clamp(gridY, 0, Rows - 1);
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
        public bool IsInPalace(int x, int y, PlayerSide side)
        {
            if (side == PlayerSide.Red)
            {
                return x >= BoardConstants.Full.PalaceXRange.MinX &&
                       x <= BoardConstants.Full.PalaceXRange.MaxX &&
                       y >= BoardConstants.Full.RedPalaceYRange.MinY &&
                       y <= BoardConstants.Full.RedPalaceYRange.MaxY;
            }
            else if (side == PlayerSide.Black)
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
            if (side == PlayerSide.Red)
                return y < BoardConstants.Full.RiverLineYRedSide;
            else if (side == PlayerSide.Black)
                return y > BoardConstants.Full.RiverLineYBlackSide;
            else
                return true;
        }

        // 先建立 GetPlayerPieces 框架，後續可加入篩選條件
        public List<Piece> GetPlayerPieces(PlayerSide side, bool onlyAlive = false, bool onlyDead = false)
        {
            return pieces.Where(p => p.Side == side).ToList();
        }
    }
}
