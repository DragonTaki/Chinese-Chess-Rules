/* ----- ----- ----- ----- */
// Piece.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/05/07
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;
using System.Drawing;

using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Represents the abstract base class for all chess pieces in the Chinese Chess game.
    /// Each derived piece type (e.g., General, Advisor, Horse) implements its own movement rules.
    /// </summary>
    public abstract class Piece
    {
        /// <summary>
        /// Gets the specific type of this piece (e.g., General, Soldier, Chariot).
        /// </summary>
        public PieceType Type { get; }

        /// <summary>
        /// Gets the player side to which this piece belongs (Red or Black).
        /// </summary>
        public PlayerSide Side { get; }

        /// <summary>
        /// Gets or sets the current X-coordinate (column index) of this piece on the board.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the current Y-coordinate (row index) of this piece on the board.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Gets the current position of the piece as a <see cref="Point"/> structure.
        /// </summary>
        public Point Position => new Point(X, Y);

        /// <summary>
        /// Initializes a new instance of the <see cref="Piece"/> class with specified properties.
        /// </summary>
        /// <param name="type">The type of this piece.</param>
        /// <param name="x">The initial X-coordinate position.</param>
        /// <param name="y">The initial Y-coordinate position.</param>
        /// <param name="side">The side (Red or Black) this piece belongs to.</param>
        protected Piece(PieceType type, int x, int y, PlayerSide side)
        {
            Type = type;
            Side = side;
            X = x;
            Y = y;
        }

        /// <summary>
        /// Determines whether a piece is allowed to move within a specific region on the board.
        /// For most pieces, this method always returns <c>true</c>, but some (like General or Advisor)
        /// override it to restrict their movement zone.
        /// </summary>
        /// <param name="x">The X-coordinate to test.</param>
        /// <param name="y">The Y-coordinate to test.</param>
        /// <returns><c>true</c> if the target position is within the legal zone for this piece; otherwise, <c>false</c>.</returns>
        public virtual bool IsInLegalZone(int x, int y) => true;  // Default true for all pieces unless overridden

        /// <summary>
        /// Determines whether moving this piece to the target coordinate is valid according to its movement rules.
        /// Must be implemented by derived classes to define their own logic.
        /// </summary>
        /// <param name="targetX">The target X-coordinate.</param>
        /// <param name="targetY">The target Y-coordinate.</param>
        /// <param name="board">The current game board context.</param>
        /// <returns><c>true</c> if the move is valid according to piece-specific rules; otherwise, <c>false</c>.</returns>
        public abstract bool IsValidMove(int targetX, int targetY, Board board);

        /// <summary>
        /// Determines whether the piece can legally move to the specified target position.
        /// This method acts as the main entry point for movement validation and typically
        /// calls <see cref="IsValidMove(int, int, Board)"/>.
        /// </summary>
        /// <param name="targetX">The destination X-coordinate.</param>
        /// <param name="targetY">The destination Y-coordinate.</param>
        /// <param name="board">The current board instance.</param>
        /// <returns><c>true</c> if the piece can move to the target cell; otherwise, <c>false</c>.</returns>
        public bool CanMoveTo(int targetX, int targetY, Board board)
        {
            return IsValidMove(targetX, targetY, board);
        }

        /// <summary>
        /// Returns all possible legal move destinations for this piece from a given position.
        /// This method is abstract and implemented individually by each specific piece type.
        /// </summary>
        /// <param name="x">The current X-coordinate.</param>
        /// <param name="y">The current Y-coordinate.</param>
        /// <param name="board">The game board instance.</param>
        /// <returns>A list of coordinate tuples (x, y) representing all valid destinations.</returns>
        public abstract List<(int x, int y)> GetLegalMoves(int x, int y, Board board);

        /// <summary>
        /// Determines whether the destination cell is a legal landing position for this piece.
        /// A cell is considered legal if it is empty or occupied by an opponent’s piece.
        /// </summary>
        /// <param name="targetX">The target X-coordinate of the destination cell.</param>
        /// <param name="targetY">The target Y-coordinate of the destination cell.</param>
        /// <param name="board">The current board context used to check occupancy.</param>
        /// <returns><c>true</c> if the target cell is empty or occupied by an opponent; otherwise, <c>false</c>.</returns>
        protected bool IsDestinationLegal(int targetX, int targetY, Board board)
        {
            // Retrieve the piece currently occupying the target position
            Piece targetPiece = board.Grid[targetX, targetY];

            // Legal if destination is empty or occupied by an enemy piece
            return targetPiece == null || targetPiece.Side != this.Side;
        }
    }
}
