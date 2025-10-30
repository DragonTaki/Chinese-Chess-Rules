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

using Chinese_Chess_v3.Game.Models;
using Chinese_Chess_v3.Game.UI.Screens.Games.Sidebars.LoggerBoxes;
using Engine.Logging;

namespace Chinese_Chess_v3.Game.Core
{
    public class GameManager
    {
        public LoggerBoxHandler Logger { get; private set; }
        public Board Board { get; private set; }
        public Player Red { get; private set; }
        public Player Black { get; private set; }
        private PlayerSide currentTurn = PlayerSide.Red;
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
            CurrentTurn = PlayerSide.Red;
            selectedPiece = null;
            Red = new Player(PlayerSide.Red, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));
            Black = new Player(PlayerSide.Black, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(5));

            // notify UI that board is ready
            BoardReset?.Invoke();
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);
        }
        public void SetLogger(LoggerBoxHandler loggerHandler)
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
            CurrentTurn = PlayerSide.Red;

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
            CurrentTurn = PlayerSide.Red;

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
            CurrentTurn = PlayerSide.Red;

            // Inform UI
            BoardReset?.Invoke();
        }

        public List<Piece> GetCurrentPieces()
        {
            return Board.GetAllPieces();
        }
        public bool TryMove(int fromX, int fromY, int toX, int toY)
        {
            var piece = Board.GetPiece(fromX, fromY);
            if (piece == null || piece.Side != CurrentTurn)
                return false;

            if (piece.CanMoveTo(Board, toX, toY))
            {
                var targetPiece = Board.GetPiece(toX, toY);
                if (targetPiece != null)
                {
                    Board.RemovePiece(toX, toY);
                    PieceCaptured?.Invoke(targetPiece);
                    PieceRemoved?.Invoke(targetPiece);
                }

                Board.MovePiece(fromX, fromY, toX, toY);
                PieceMoved?.Invoke(piece, toX, toY);
                SwitchTurn();
                return true;
            }

            return false;
        }

        public void HandleClick(int x, int y)
        {
            var clickedPiece = Board.GetPiece(x, y);
            AppLogger.Log(
                $"Current turn: {CurrentTurn}, holding: {(selectedPiece == null ? "null" : selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {(clickedPiece == null ? "null" : clickedPiece.GetType().Name)}", LogLevel.DEBUG);
            Logger.AddMessage($"Current turn: {CurrentTurn}, holding: {(selectedPiece == null ? "null" : selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {(clickedPiece == null ? "null" : clickedPiece.GetType().Name)}");

            // No selected piece, try to select one
            if (selectedPiece == null)
            {
                if (clickedPiece != null && clickedPiece.Side == CurrentTurn)
                {
                    selectedPiece = clickedPiece;
                    AppLogger.Log($"(Action) Selected {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger.AddMessage($"(Action) Selected {clickedPiece.Type} at ({x},{y})");
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
                    Logger.AddMessage($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})");
                    PieceUnselected?.Invoke(selectedPiece);
                    selectedPiece = null;
                }
                else
                {
                    AppLogger.Log($"(Action) Switched to {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger.AddMessage($"(Action) Switched to {clickedPiece.Type} at ({x},{y})");
                    PieceUnselected?.Invoke(selectedPiece);
                    selectedPiece = clickedPiece;
                    PieceSelected?.Invoke(selectedPiece);
                }
                return;
            }

            // Has selected piece, try to move to 2nd selection
            if (selectedPiece.CanMoveTo(Board, x, y))
            {
                // If 2nd selection point has enemy piece
                if (clickedPiece != null && clickedPiece.Side != selectedPiece.Side)
                {
                    Board.RemovePiece(x, y);
                    AppLogger.Log($"(Action) Captured {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger.AddMessage($"(Action) Captured {clickedPiece.Type} at ({x},{y})");
                    PieceCaptured?.Invoke(clickedPiece);
                    PieceRemoved?.Invoke(clickedPiece);
                }

                // move logic
                int fromX = selectedPiece.Position.X;
                int fromY = selectedPiece.Position.Y;
                Board.MovePiece(fromX, fromY, x, y);
                AppLogger.Log($"(Action) Moved {selectedPiece.Type} to ({x},{y})", LogLevel.DEBUG);
                    Logger.AddMessage($"(Action) Moved {selectedPiece.Type} to ({x},{y})");

                // raise moved event AFTER board updated
                PieceMoved?.Invoke(selectedPiece, x, y);

                // unselect and notify
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
                SwitchTurn();
            }
            else
            {
                // If 2nd selection point is empty, unselected
                if (clickedPiece == null)
                {
                    AppLogger.Log($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger.AddMessage($"(Action) Un-selected {selectedPiece.Type} at ({x},{y})");
                }
                // Invalid catch
                else
                {
                    AppLogger.Log($"(Action) Invalid move to ({x},{y})", LogLevel.DEBUG);
                    Logger.AddMessage($"(Action) Invalid move to ({x},{y})");
                }
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
            }
        }

        private void SwitchTurn()
        {
            if (CurrentTurn == PlayerSide.Red)
            {
                Red.Timer.EndStep();
                Black.Timer.StartStep();
                CurrentTurn = PlayerSide.Black;
            }
            else
            {
                Black.Timer.EndStep();
                Red.Timer.StartStep();
                CurrentTurn = PlayerSide.Red;
            }
        }
        
        public void PauseGame() => IsPaused = true;
        public void ResumeGame() => IsPaused = false;
        public void TogglePause() => IsPaused = !IsPaused;
    }
}