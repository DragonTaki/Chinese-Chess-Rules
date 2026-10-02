/* ----- ----- ----- ----- */
// Board.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/02
// Version: v2.4
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Boards
{
    public enum BoardType
    {
        Full,        // Full board (大盤) 9x10
        HalfCenter,  // Half board (半盤) 8x4
        HalfCross,   // Three Kingdoms half board (三國半盤) 9x5
    }

    /// <summary>
    /// Represents the Chinese Chess board, managing all chess pieces, their positions, and interactions.
    /// This class provides methods to initialize, place, move, and remove pieces on the board grid
    /// (9x10 for <see cref="BoardType.Full"/>; the size depends on the <see cref="BoardType"/>).
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
        /// Move counter of the game: advanced once per executed move (<see cref="AdvanceTurn"/>),
        /// stored in the pieces' history snapshots as their turn index.
        /// Starts at 0 (before the first move).
        /// </summary>
        public int Turn { get; private set; } = 0;
        public Rules GameRules { get; private set; }

        /// <summary>
        /// Switches the rules this board plays by (e.g. to a loaded saved game's rules);
        /// null for the default <see cref="Rules"/>. Takes effect from the next move check.
        /// </summary>
        public void SetRules(Rules rules)
        {
            GameRules = rules ?? new Rules();
        }

        /// <summary>
        /// Internal list containing all active pieces on the board.
        /// This allows for quick iteration and management without traversing the grid.
        /// </summary>
        private readonly List<Piece> _pieces = new List<Piece>();

        /// <summary>
        /// The pieces the temporary move simulations in progress (<see cref="SimulateMove{T}"/>,
        /// used by <see cref="WouldMoveExposeOwnGeneral"/>) have "captured": still in
        /// <see cref="_pieces"/> (the list is never modified during a simulation, so callers
        /// may be iterating it) but skipped by every lookup. A list, not a single field,
        /// because simulations nest (e.g. <see cref="BoardAnalysis"/> simulates a capture
        /// and then checks the legality of the recapture, which simulates again).
        /// </summary>
        private readonly List<Piece> _simulatedCaptures = new List<Piece>();

        /// <summary>
        /// Whether <paramref name="piece"/> is hidden by a move simulation in progress (it
        /// has been "captured" there). Callers iterating <see cref="GetAllPieces"/> inside
        /// <see cref="SimulateMove{T}"/> must skip such pieces.
        /// </summary>
        internal bool IsSimulatedCapture(Piece piece) => _simulatedCaptures.Contains(piece);

        /// <summary>
        /// Initializes a new instance of the <see cref="Board"/> class.
        /// Sets up an empty grid sized for the board type (no pieces are placed yet, see <see cref="Initialize"/>).
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
        /// Clears the board (and the turn counter), then places a new piece for each entry of
        /// <paramref name="initialPieces"/> (e.g. <see cref="PieceConstants.InitialClassicPieces"/>).
        /// </summary>
        /// <param name="initialPieces">List of chess pieces to be placed</param>
        public void Initialize(List<PieceInfo> initialPieces)
        {
            Clear();

            // Place the given pieces
            foreach (var info in initialPieces)
            {
                var piece = CreatePieceFromInfo(info);  // Create piece instance based on piece info
                Grid[info.X, info.Y] = piece;           // Place the piece on the grid
                _pieces.Add(piece);                      // Add to the active piece list
            }
        }

        public void Clear()
        {
            Array.Clear(Grid, 0, Grid.Length);
            _pieces.Clear();

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
        /// Steps the turn counter back by one (never below 0); the inverse of
        /// <see cref="AdvanceTurn"/>, for undoing a move.
        /// </summary>
        public void RetreatTurn()
        {
            if (Turn > 0)
                Turn--;
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
        /// <exception cref="ArgumentException">Thrown when an unknown piece type is encountered (by <see cref="Piece.Create"/>).</exception>
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
            return _pieces;
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

            // Remove the piece already on the square first (if any)
            var existing = Grid[x, y];
            if (existing != null)
                _pieces.Remove(existing);

            // Create the new PieceInfo
            var info = new PieceInfo(type, x, y, color, side, faceUp);

            // Create the matching Piece instance
            var piece = CreatePieceFromInfo(info);

            // Place it on the grid and in the piece list
            Grid[x, y] = piece;
            _pieces.Add(piece);

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
        /// <returns>True if a piece was moved, false if there was no piece on the source square.</returns>
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
        /// Tries to flip the piece at the given coordinates face up (only a piece with FaceUp = false can be flipped).
        /// </summary>
        /// <param name="x">Board X coordinate</param>
        /// <param name="y">Board Y coordinate</param>
        /// <returns>
        /// null: there is no piece at the coordinates;
        /// false: the piece is already face up, so it cannot be flipped;
        /// true: flipped successfully.
        /// </returns>
        public bool? FlipPiece(int x, int y)
        {
            var piece = GetPiece(x, y);
            if (piece == null)
                return null;

            if (piece.CurrentInfo.IsFaceUp)
                return false; // Already face up, cannot be flipped

            // Flip the piece through its UpdateState
            piece.UpdateState(Turn, isFaceUp: true);

            // The grid already references this Piece, nothing to put back
            return true;
        }

        /// <summary>
        /// Dark chess's faction decision (決定顏色): every piece whose owner is still undecided
        /// (<c>PlayerSide.None</c>) becomes <paramref name="side"/>'s if it is
        /// <paramref name="color"/>, otherwise the other player's. Called once, by the first
        /// flip of the game, with the flipped piece's colour and the flipping player. Each
        /// changed piece gets one history snapshot (so undoing the flip undoes this too, see
        /// <see cref="RevertStates"/>).
        /// </summary>
        /// <param name="color">The colour <paramref name="side"/> plays.</param>
        /// <param name="side">The player who owns <paramref name="color"/>: Player1 or Player2.</param>
        /// <exception cref="ArgumentException"><paramref name="side"/> is not Player1 or Player2.</exception>
        public void AssignFactions(PieceColor color, PlayerSide side)
        {
            if (side != PlayerSide.Player1 && side != PlayerSide.Player2)
                throw new ArgumentException($"A faction belongs to Player1 or Player2, not {side}", nameof(side));

            var other = side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;
            foreach (var p in _pieces)
            {
                if (p.Side == PlayerSide.None)
                    p.UpdateState(Turn, side: p.Color == color ? side : other);
            }
        }

        /// <summary>
        /// Marks a piece as dead (captured or eliminated logically, but not removed from history).
        /// </summary>
        /// <param name="x">The X-coordinate of the piece to mark as dead.</param>
        /// <param name="y">The Y-coordinate of the piece to mark as dead.</param>
        /// <returns>True if the operation succeeded, false if there is no piece at (x, y) or it is already dead.</returns>
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
        /// <returns>True if a piece was removed, false if the square was empty.</returns>
        public bool RemovePiece(int x, int y)
        {
            var piece = Grid[x, y];

            if (piece == null)
                return false;

            piece.UpdateState(Turn, isDead: true);
            _pieces.Remove(piece);
            Grid[x, y] = null;

            return true;
        }

        /// <summary>
        /// Takes back a move made by <see cref="MovePiece"/> (and, for a capture, the
        /// <see cref="RemovePiece"/> before it): <paramref name="piece"/>, now on (toX, toY),
        /// returns to (fromX, fromY) with its previous state, and <paramref name="captured"/>
        /// (the same object that was removed, or null) is put back on (toX, toY), alive again.
        /// Each piece's last history snapshot is dropped (<see cref="Piece.RevertLastState"/>).
        /// The turn counter is not changed (see <see cref="RetreatTurn"/>).
        /// </summary>
        /// <exception cref="InvalidOperationException"><paramref name="piece"/> is not on (toX, toY),
        /// or (fromX, fromY) is occupied.</exception>
        internal void UnmakeMove(Piece piece, int fromX, int fromY, int toX, int toY, Piece captured)
        {
            if (piece == null || Grid[toX, toY] != piece)
                throw new InvalidOperationException($"The piece to take back is not on ({toX},{toY})");
            if (Grid[fromX, fromY] != null)
                throw new InvalidOperationException($"({fromX},{fromY}) is occupied; cannot take the move back");

            piece.RevertLastState();
            Grid[fromX, fromY] = piece;
            Grid[toX, toY] = null;

            if (captured != null)
            {
                captured.RevertLastState();
                Grid[toX, toY] = captured;
                _pieces.Add(captured);
            }
        }

        /// <summary>
        /// Takes back an action recorded as per-piece history growth (a dark-chess flip or
        /// hidden capture, which may move, flip, kill or re-assign several pieces at once):
        /// each listed piece drops its last <c>snapshots</c> history entries
        /// (<see cref="Piece.RevertLastState"/>) and the grid and piece list are rebuilt from
        /// the restored states — a piece alive again stands on its restored square (put back
        /// into the piece list if it had been removed), a piece dead again is taken off. Every
        /// listed piece must have been alive on the board before the action. The turn counter
        /// is not changed (see <see cref="RetreatTurn"/>).
        /// </summary>
        /// <param name="changes">Each piece the action changed (once) and how many history snapshots it added.</param>
        /// <exception cref="InvalidOperationException">A restored square is taken by a piece that is not being restored.</exception>
        internal void RevertStates(IReadOnlyList<(Piece piece, int snapshots)> changes)
        {
            // Lift every changed piece off the grid first, so pieces that swapped or shared
            // squares during the action can all be put back.
            foreach (var (piece, _) in changes)
            {
                if (IsInBoard(piece.X, piece.Y) && Grid[piece.X, piece.Y] == piece)
                    Grid[piece.X, piece.Y] = null;
            }

            foreach (var (piece, snapshots) in changes)
            {
                for (int i = 0; i < snapshots; i++)
                    piece.RevertLastState();
            }

            foreach (var (piece, _) in changes)
            {
                if (piece.CurrentInfo.IsDead)
                {
                    _pieces.Remove(piece);
                    continue;
                }

                if (Grid[piece.X, piece.Y] != null)
                    throw new InvalidOperationException($"({piece.X},{piece.Y}) is occupied; cannot restore {piece.Type}");
                Grid[piece.X, piece.Y] = piece;
                if (!_pieces.Contains(piece))
                    _pieces.Add(piece);
            }
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
        /// Determines whether a coordinate lies within the palace area of a given colour (red's
        /// palace at the bottom, y 7-9; black's at the top, y 0-2). Keyed by colour, not player:
        /// Player1 plays Black in a black-first game.
        /// </summary>
        /// <param name="x">The X-coordinate of the target position.</param>
        /// <param name="y">The Y-coordinate of the target position.</param>
        /// <param name="color">The piece colour to check (Red or Black).</param>
        /// <returns>
        /// <c>true</c> if the position is inside the palace of the given colour,
        /// or if the colour is not Red/Black (treated as unrestricted); otherwise, <c>false</c>.
        /// </returns>
        public bool IsInPalace(PieceColor color, int x, int y)
        {
            if (color == PieceColor.Red)
            {
                return x >= BoardConstants.Full.PalaceXRange.MinX &&
                       x <= BoardConstants.Full.PalaceXRange.MaxX &&
                       y >= BoardConstants.Full.RedPalaceYRange.MinY &&
                       y <= BoardConstants.Full.RedPalaceYRange.MaxY;
            }
            else if (color == PieceColor.Black)
            {
                return x >= BoardConstants.Full.PalaceXRange.MinX &&
                       x <= BoardConstants.Full.PalaceXRange.MaxX &&
                       y >= BoardConstants.Full.BlackPalaceYRange.MinY &&
                       y <= BoardConstants.Full.BlackPalaceYRange.MaxY;
            }
            else
            {
                // For the other colours, assume no restriction
                return true;
            }
        }

        /// <summary>
        /// Determines whether the given row is across the river from the perspective of
        /// <paramref name="color"/> (red's own half is the bottom, y 5-9; black's the top,
        /// y 0-4); true for any other colour. Keyed by colour, not player.
        /// </summary>
        public bool IsPassRiver(PieceColor color, int y)
        {
            if (color == PieceColor.Red)
                return y < BoardConstants.Full.RiverLineYRedSide;
            else if (color == PieceColor.Black)
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
            var firstGenerals = QueryPieces(type: PieceType.General, side: PlayerSide.Player1);
            var secondGenerals = QueryPieces(type: PieceType.General,  side: PlayerSide.Player2);

            if (firstGenerals.Count != 1 || secondGenerals.Count != 1)
                return false;

            (int x, int y) first = (firstGenerals[0].X, firstGenerals[0].Y);
            (int x, int y) second = (secondGenerals[0].X, secondGenerals[0].Y);

            // Capturing a General leaves no pair to face.
            if ((toX, toY) == first || (toX, toY) == second)
                return false;

            // A moving General is at its destination afterwards.
            if ((fromX, fromY) == first)
                first = (toX, toY);
            else if ((fromX, fromY) == second)
                second = (toX, toY);

            if (first.x != second.x)
                return false;

            int x = first.x;
            int yMin = Math.Min(first.y, second.y) + 1;
            int yMax = Math.Max(first.y, second.y);

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
        /// Checks whether a General of <paramref name="side"/> standing on (targetX, targetY) would
        /// be legal regarding the face-to-face rule (王見王). The check scans vertically from that
        /// square in both directions (so it does not depend on which colour - top or bottom -
        /// <paramref name="side"/> plays) and stops at the first piece found each way.
        /// <paramref name="side"/>'s own General is not a blocker: it is the piece that moves (its
        /// square is empty afterwards), e.g. when it steps back along an open file.
        /// </summary>
        /// <param name="side">The side of the moving piece.</param>
        /// <param name="targetX">The X-coordinate of the target position.</param>
        /// <param name="targetY">The Y-coordinate of the target position.</param>
        /// <returns>True if the move does NOT cause generals to face each other; otherwise, false.</returns>
        /// <exception cref="ArgumentException"><paramref name="side"/> is not Player1 or Player2.</exception>
        public bool IsGeneralTargetLegal(PlayerSide side, int targetX, int targetY)
        {
            if (side != PlayerSide.Player1 && side != PlayerSide.Player2)
                throw new ArgumentException($"Only Player1 and Player2 have a General on the Full board, not {side}", nameof(side));

            // Up (Y--) and down (Y++): the opposing General is on whichever side of the board
            // its colour plays.
            foreach (int step in new[] { -1, 1 })
            {
                int y = targetY + step;

                while (y >= 0 && y < Rows)
                {
                    var piece = Grid[targetX, y];
                    if (piece != null && !(piece.Type == PieceType.General && piece.Side == side))
                    {
                        if (piece.Type == PieceType.General)
                            return false;  // Hit the opposing General -> not legal
                        break;  // Hit any other piece -> it blocks the line this way; stop scanning
                    }

                    y += step;
                }
            }

            return true;  // No opposing General found along the line -> legal
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
            foreach (var p in _pieces)
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
            var first = GetGeneral(PlayerSide.Player1);
            var second = GetGeneral(PlayerSide.Player2);
            if (first == null || second == null || first.X != second.X)
                return false;

            int yMin = Math.Min(first.Y, second.Y) + 1;
            int yMax = Math.Max(first.Y, second.Y);
            for (int y = yMin; y < yMax; y++)
            {
                if (Grid[first.X, y] != null)
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

            foreach (var p in _pieces)
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
        /// (<see cref="_simulatedCaptures"/>), then everything is restored — so every piece's
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
                _simulatedCaptures.Add(captured);
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
                    _simulatedCaptures.RemoveAt(_simulatedCaptures.LastIndexOf(captured));
            }
        }

        /// <summary>
        /// Whether this board plays by the dark-chess turn rules (台灣暗棋, HalfCenter): a turn is
        /// either flipping any face-down piece (翻子) or moving one's own face-up piece; a
        /// face-down piece is never moved (or selected) as itself, and the first flip decides
        /// which player owns which colour. HalfCross (三國暗棋) is a separate rule system still
        /// being specified (docs/DARK-CHESS-RULES.md §1.2), so it is not included.
        /// </summary>
        public bool UsesDarkChessRules => Type == BoardType.HalfCenter;

        /// <summary>
        /// Whether <paramref name="side"/> has at least one legal move (see
        /// <see cref="Piece.GetLegalMoves"/>). Stops at the first one found. On a
        /// <see cref="UsesDarkChessRules"/> board a face-down piece has no moves (it can only
        /// be flipped, see <see cref="HasAnyAction"/>), and a face-up piece nobody owns yet
        /// (明棋半盤 before the first move decides the factions) may be moved by either player.
        /// </summary>
        public bool HasAnyLegalMove(PlayerSide side)
        {
            foreach (var p in _pieces)
            {
                if (p.Side != side && !(UsesDarkChessRules && p.Side == PlayerSide.None))
                    continue;
                if (UsesDarkChessRules && !p.CurrentInfo.IsFaceUp)
                    continue;
                foreach (var (x, y) in p.GetPseudoLegalMoves(this))
                {
                    if (!UsesCheckRules || !WouldMoveExposeOwnGeneral(p, x, y))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Whether <paramref name="side"/> can act at all: a legal move (see
        /// <see cref="HasAnyLegalMove"/>), or, on a <see cref="UsesDarkChessRules"/> board,
        /// flipping a face-down piece — any face-down piece may be flipped by either player,
        /// so a board that still has one always offers an action (e.g. the fully face-down
        /// start position, where nobody owns a piece yet).
        /// </summary>
        public bool HasAnyAction(PlayerSide side)
        {
            if (UsesDarkChessRules)
            {
                foreach (var p in _pieces)
                {
                    if (!IsSimulatedCapture(p) && !p.CurrentInfo.IsFaceUp)
                        return true;
                }
            }
            return HasAnyLegalMove(side);
        }

        public List<Piece> QueryPieces(
            PieceType? type = null,
            PlayerSide? side = null,
            bool? isAlive = null,
            bool? isFaceUp = null)
        {
            return _pieces.Where(p =>
                !IsSimulatedCapture(p) &&
                (!type.HasValue || p.Type == type.Value) &&
                (!side.HasValue || p.Side == side.Value) &&
                (!isAlive.HasValue || (isAlive.Value ? !p.CurrentInfo.IsDead : p.CurrentInfo.IsDead)) &&
                (!isFaceUp.HasValue || p.CurrentInfo.IsFaceUp == isFaceUp.Value)
            ).ToList();
        }
    }
}
