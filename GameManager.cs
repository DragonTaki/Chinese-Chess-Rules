/* ----- ----- ----- ----- */
// GameManager.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/31
// Version: v1.2
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

using Engine.Logging;

namespace Chinese_Chess_v3.Game.Core
{
    public class GameManager
    {
        public IGameLog Logger { get; private set; }
        public Board Board { get; private set; }
        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        private PlayerSide currentTurn = PlayerSide.Player1;
        public PlayerSide CurrentTurn
        {
            get => currentTurn;
            private set
            {
                if (currentTurn != value)
                {
                    currentTurn = value;
                    TurnChanged?.Invoke(currentTurn);
                }
            }
        }
        public event Action<PlayerSide> TurnChanged;
        

#nullable enable
        private Piece? selectedPiece;
        public Piece? SelectedPiece => selectedPiece;
#nullable disable

        private bool isPaused = false;

        public bool IsPaused
        {
            get => isPaused;
            private set
            {
                if (isPaused != value)
                {
                    isPaused = value;
                    PausedChanged?.Invoke(isPaused);
                }
            }
        }

        public event Action<bool> PausedChanged;

#nullable enable
        // events for UI bridge
        public event Action<Piece>? PieceSelected;
        public event Action<Piece>? PieceUnselected;
        public event Action<Piece, int, int>? PieceMoved; // piece, toX, toY
        public event Action<Piece>? PieceCaptured;
        public event Action<Piece>? PieceAdded;
        public event Action<Piece>? PieceRemoved;
        public event Action? BoardReset;
#nullable disable

        public GameManager()
        {
            // Initialize the board
            Board = new Board();
            Board.Initialize(BoardConfigLoader.Load());
            CurrentTurn = PlayerSide.Player1;
            selectedPiece = null;
            Player1 = new Player(PlayerSide.Player1, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5), null, true);
            Player2 = new Player(PlayerSide.Player2, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5), null, true);

            // Player1 moves first, so their step timer needs to actually be
            // running from the start — SwitchTurn() only starts Player1's
            // timer again *after* Player2's first move, leaving Player1's
            // opening move untimed otherwise.
            Player1.Timer.StartStep();

            // notify UI that board is ready
            BoardReset?.Invoke();
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);
        }
        public void SetLogger(IGameLog loggerHandler)
        {
            Logger = loggerHandler ?? throw new ArgumentNullException(nameof(loggerHandler));
        }

        public void ResetBoardToDefault()
        {
            // Load default pieces
            var defaultPieces = BoardConfigLoader.Load();

            // Reset board:
            // (A) Clear pieces
            // (B) Reset turn
            // (C) Recreate pieces
            Board.Initialize(defaultPieces);

            // Reset selected piece
            selectedPiece = null;
            // Reset side
            CurrentTurn = PlayerSide.Player1;
            ResetTimers(startFirstTurn: true);

            // Inform UI
            BoardReset?.Invoke();

            // Inform pieces added
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);
        }

        public void LoadCustomBoard(List<PieceInfo> customInitialPieces)
        {
            // Reset board:
            // (A) Clear pieces
            // (B) Reset turn
            // (C) Recreate pieces
            Board.Initialize(customInitialPieces);

            // Reset selected piece
            selectedPiece = null;
            // Reset side
            CurrentTurn = PlayerSide.Player1;
            ResetTimers(startFirstTurn: true);

            // Inform UI
            BoardReset?.Invoke();

            // Inform pieces added
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);
        }

        public void ClearBoard()
        {
            // Clear board:
            // (A) Clear pieces
            // (B) Reset turn
            Board.Clear();

            // Reset selected piece
            selectedPiece = null;
            // Reset side
            CurrentTurn = PlayerSide.Player1;
            ResetTimers(startFirstTurn: false);

            // Inform UI
            BoardReset?.Invoke();
        }

        public List<Piece> GetCurrentPieces()
        {
            return Board.GetAllPieces();
        }
        /// <summary>
        /// Moves the piece at (fromX, fromY) to (toX, toY) if it belongs to the side to
        /// move and the move is legal. Same effect as selecting and clicking through
        /// <see cref="HandleClick"/> (capture, log, events, selection cleared, turn switch).
        /// </summary>
        public bool TryMove(int fromX, int fromY, int toX, int toY)
        {
            if (IsPaused)
                return false;

            var piece = Board.GetPiece(fromX, fromY);
            if (piece == null || piece.Side != CurrentTurn)
                return false;

            if (!piece.CanMoveTo(Board, toX, toY))
                return false;

            ExecuteMove(piece, toX, toY);
            return true;
        }

        public void HandleClick(int x, int y)
        {
            // A move while paused would end the paused (not active) step and start the
            // other clock, leaving the paused clock stuck until a later Resume.
            if (IsPaused)
                return;

            var clickedPiece = Board.GetPiece(x, y);
            AppLogger.Log(
                $"Current turn: {CurrentTurn}, holding: {(selectedPiece == null ? "null" : selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {(clickedPiece == null ? "null" : clickedPiece.GetType().Name)}", LogLevel.DEBUG);
            Logger?.AddMessage($"Current turn: {CurrentTurn}, holding: {(selectedPiece == null ? "null" : selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {(clickedPiece == null ? "null" : clickedPiece.GetType().Name)}");

            // No selected piece, try to select one
            if (selectedPiece == null)
            {
                if (clickedPiece != null && clickedPiece.Side == CurrentTurn)
                {
                    selectedPiece = clickedPiece;
                    AppLogger.Log($"(Action) Selected {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Selected {clickedPiece.Type} at ({x},{y})");
                    PieceSelected?.Invoke(selectedPiece);
                }
                return;
            }

            // Has selected piece, but 2nd selection is own side
            if (clickedPiece != null && clickedPiece.Side == selectedPiece.Side)
            {
                if (clickedPiece == selectedPiece)
                {
                    AppLogger.Log($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})");
                    PieceUnselected?.Invoke(selectedPiece);
                    selectedPiece = null;
                }
                else
                {
                    AppLogger.Log($"(Action) Switched to {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Switched to {clickedPiece.Type} at ({x},{y})");
                    PieceUnselected?.Invoke(selectedPiece);
                    selectedPiece = clickedPiece;
                    PieceSelected?.Invoke(selectedPiece);
                }
                return;
            }

            // Has selected piece, try to move to 2nd selection
            if (selectedPiece.CanMoveTo(Board, x, y))
            {
                ExecuteMove(selectedPiece, x, y);
            }
            else
            {
                // If 2nd selection point is empty, unselected
                if (clickedPiece == null)
                {
                    AppLogger.Log($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})");
                }
                // Invalid catch
                else
                {
                    AppLogger.Log($"(Action) Invalid move to ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Invalid move to ({x},{y})");
                }
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
            }
        }

        /// <summary>
        /// Applies an already-validated move: advances the board's turn counter (so the
        /// pieces' history snapshots carry the move number), captures whatever stands on
        /// the destination, moves the piece, clears the selection and switches the turn.
        /// Shared by <see cref="HandleClick"/> and <see cref="TryMove"/>.
        /// </summary>
        private void ExecuteMove(Piece piece, int toX, int toY)
        {
            int fromX = piece.X;
            int fromY = piece.Y;

            Board.AdvanceTurn();

            // If the destination has an (enemy) piece, capture it first
            var targetPiece = Board.GetPiece(toX, toY);
            if (targetPiece != null)
            {
                Board.RemovePiece(toX, toY);
                AppLogger.Log($"(Action) Captured {targetPiece.Type} at ({toX},{toY})", LogLevel.DEBUG);
                Logger?.AddMessage($"(Action) Captured {targetPiece.Type} at ({toX},{toY})");
                PieceCaptured?.Invoke(targetPiece);
                PieceRemoved?.Invoke(targetPiece);
            }

            // move logic
            Board.MovePiece(fromX, fromY, toX, toY);
            AppLogger.Log($"(Action) Moved {piece.Type} to ({toX},{toY})", LogLevel.DEBUG);
            Logger?.AddMessage($"(Action) Moved {piece.Type} to ({toX},{toY})");

            // raise moved event AFTER board updated
            PieceMoved?.Invoke(piece, toX, toY);

            // unselect and notify
            if (selectedPiece != null)
            {
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
            }

            SwitchTurn();
        }

        private void SwitchTurn()
        {
            if (CurrentTurn == PlayerSide.Player1)
            {
                Player1.Timer.EndStep();
                Player2.Timer.StartStep();
                CurrentTurn = PlayerSide.Player2;
            }
            else
            {
                Player2.Timer.EndStep();
                Player1.Timer.StartStep();
                CurrentTurn = PlayerSide.Player1;
            }
        }
        
        /// <summary>
        /// Clears both clocks for a new game; optionally starts Player1's first step.
        /// Also clears the pause state, since Reset() drops a clock's Paused state.
        /// </summary>
        private void ResetTimers(bool startFirstTurn)
        {
            Player1.Timer.Reset();
            Player2.Timer.Reset();
            IsPaused = false;
            if (startFirstTurn)
                Player1.Timer.StartStep();
        }

        /// <summary>
        /// Advances both players' clocks; call once per frame. Only the side whose
        /// step is active actually accumulates time (see PlayerTimer.Update).
        /// </summary>
        public void UpdateTimers()
        {
            Player1.Timer.Update();
            Player2.Timer.Update();
        }

        /// <summary>
        /// Pauses the game: stops the running clock (PlayerTimer.Pause only affects the
        /// side whose step is active) and ignores board input until resumed.
        /// </summary>
        public void PauseGame()
        {
            if (IsPaused)
                return;

            Player1.Timer.Pause();
            Player2.Timer.Pause();
            IsPaused = true;
        }

        /// <summary>
        /// Resumes a paused game; the paused clock continues without counting the time
        /// spent paused (PlayerTimer.Resume restamps its reference time).
        /// </summary>
        public void ResumeGame()
        {
            if (!IsPaused)
                return;

            Player1.Timer.Resume();
            Player2.Timer.Resume();
            IsPaused = false;
        }

        public void TogglePause()
        {
            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }
}