/* ----- ----- ----- ----- */
// Board.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/05/06
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Constants.UI;
using Chinese_Chess_v3.Game.Constants.Game;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Represents the Chinese Chess board, managing all chess pieces, their positions, and interactions.
    /// This class provides methods to initialize, place, move, and remove pieces on the 9x10 board grid.
    /// </summary>
    public class Board
    {
        /// <summary>
        /// Two-dimensional array that represents the chessboard grid.
        /// Each cell stores a reference to the <see cref="Piece"/> currently occupying that position.
        /// </summary>
        public Piece[,] Grid { get; }

        /// <summary>
        /// Internal list containing all active pieces on the board.
        /// This allows for quick iteration and management without traversing the grid.
        /// </summary>
        private List<Piece> pieces = new List<Piece>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// Sets up the 9x10 grid layout and provides reference coordinate documentation.
        /// </summary>
        public Board()
        {
            Grid = new Piece[9, 10];
            /*
                    X →           Black Side
                 (0,Y)(1,Y)(2,Y)(3,Y)(4,Y)(5,Y)(6,Y)(7,Y)(8,Y)
             (X, 0)  +---+---+---+---+---+---+---+---+---+ ← Y=0
                     |   |   |   |   |   |   |   |   |   |
             (X, 1)  +---+---+---+---+---+---+---+---+---+ ← Y=1
                     |   |   |   |   |   |   |   |   |   |
             (X, 2)  +---+---+---+---+---+---+---+---+---+ ← Y=2
                     |   |   |   |   |   |   |   |   |   |
             (X, 3)  +---+---+---+---+---+---+---+---+---+ ← Y=3
                     |   |   |   |   |   |   |   |   |   |
             (X, 4)  +---+---+---+---+---+---+---+---+---+ ← Y=4
                     |         T h e   R i v e r         |
             (X, 5)  +---+---+---+---+---+---+---+---+---+ ← Y=5
                     |   |   |   |   |   |   |   |   |   |
             (X, 6)  +---+---+---+---+---+---+---+---+---+ ← Y=6
                     |   |   |   |   |   |   |   |   |   |
             (X, 7)  +---+---+---+---+---+---+---+---+---+ ← Y=7
                     |   |   |   |   |   |   |   |   |   |
             (X, 8)  +---+---+---+---+---+---+---+---+---+ ← Y=8
                     |   |   |   |   |   |   |   |   |   |
             (X, 9)  +---+---+---+---+---+---+---+---+---+ ← Y=9
                 (0,Y)(1,Y)(2,Y)(3,Y)(4,Y)(5,Y)(6,Y)(7,Y)(8,Y)
                    X →             Red Side
            */
            // Left to right (x-axis): 0~8; Top to bottom (y-axis): 0~9
            // Red area (y-axis): 0~4; Black area (y-axis): 5~9
        }

        /// <summary>
        /// Initializes the board to its starting state by placing all pieces
        /// according to the predefined positions in <see cref="PieceConstants.InitialPieces"/>.
        /// </summary>
        /// <param name="initialPieces">List of chess pieces to be placed</param>
        public void Initialize(List<PieceInfo> initialPieces)
        {
            pieces.Clear();  // Clear existing pieces before reinitialization
            Array.Clear(Grid, 0, Grid.Length);  // Clear the board

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
            // 清空棋盤格子
            Array.Clear(Grid, 0, Grid.Length);
            // 清空棋子列表
            pieces.Clear();
        }
        
        /// <summary>
        /// Creates a piece instance based on the provided <see cref="PieceInfo"/> configuration.
        /// </summary>
        /// <param name="info">The <see cref="PieceInfo"/> object containing type, position, and side data.</param>
        /// <returns>A newly created <see cref="Piece"/> instance corresponding to the given piece type.</returns>
        /// <exception cref="Exception">Thrown when an unknown piece type is encountered.</exception>
        private Piece CreatePieceFromInfo(PieceInfo info)
        {
            PlayerSide side = info.IsRed ? PlayerSide.Red : PlayerSide.Black;

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
            return x >= boardX && x <= boardX + BoardConstants.Columns * gridSize &&
                   y >= boardY && y <= boardY + BoardConstants.Rows * gridSize;
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

            gridX = Math.Clamp(gridX, 0, BoardConstants.Columns - 1);
            gridY = Math.Clamp(gridY, 0, BoardConstants.Rows - 1);
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
            if (x >= 0 && x < BoardConstants.Columns && y >= 0 && y < BoardConstants.Rows)
            {
                return Grid[x, y];
            }
            return null;
        }

        /// <summary>
        /// Places a piece at its current (X, Y) coordinates on the board grid.
        /// </summary>
        /// <param name="piece">The <see cref="Piece"/> instance to place on the grid.</param>
        public void PlacePiece(Piece piece)
        {
            Grid[piece.X, piece.Y] = piece;
        }

        /// <summary>
        /// Moves a piece from one coordinate to another within the board grid.
        /// Updates both grid references and the piece’s internal coordinates.
        /// </summary>
        /// <param name="fromX">The source X-coordinate.</param>
        /// <param name="fromY">The source Y-coordinate.</param>
        /// <param name="toX">The destination X-coordinate.</param>
        /// <param name="toY">The destination Y-coordinate.</param>
        public void MovePiece(int fromX, int fromY, int toX, int toY)
        {
            var piece = Grid[fromX, fromY];
            Grid[toX, toY] = piece;
            Grid[fromX, fromY] = null;
            if (piece != null)
            {
                piece.X = toX;
                piece.Y = toY;
            }
        }

        /// <summary>
        /// Removes a piece from both the board grid and the internal piece list.
        /// </summary>
        /// <param name="x">The X-coordinate of the piece to remove.</param>
        /// <param name="y">The Y-coordinate of the piece to remove.</param>
        public void RemovePiece(int x, int y)
        {
            var piece = Grid[x, y];
            if (piece != null)
            {
                pieces.Remove(piece);
            }
            Grid[x, y] = null;
        }
    }
}
