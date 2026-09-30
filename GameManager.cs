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

        /// <summary>
        /// True once the game has ended (see <see cref="GameOver"/>); board input is
        /// ignored and both clocks are stopped until a new game is set up.
        /// </summary>
        public bool IsGameOver { get; private set; } = false;

        /// <summary>The winning side of the ended game; <c>PlayerSide.None</c> while playing.</summary>
        public PlayerSide Winner { get; private set; } = PlayerSide.None;

        /// <summary>How the ended game ended (see <see cref="GameOverInfo"/>); null while playing.</summary>
        public GameOverInfo Result { get; private set; } = null;

        /// <summary>
        /// Raised once when the game ends, with winner, loser, reason and (for
        /// checkmate/stalemate) the final position data. For the UI to show the result.
        /// </summary>
        public event Action<GameOverInfo> GameOver;

        /// <summary>
        /// True while the side to move (<see cref="CurrentTurn"/>) is in check (將軍).
        /// Only set on boards that use check rules (<see cref="Board.UsesCheckRules"/>).
        /// </summary>
        public bool IsInCheck { get; private set; } = false;

        /// <summary>
        /// Raised after a move that puts the opponent in check but does not end the game,
        /// with the side now in check (= the new <see cref="CurrentTurn"/>). A move that
        /// checkmates raises <see cref="GameOver"/> instead.
        /// </summary>
        public event Action<PlayerSide> Check;

        /// <summary>The most recent move of this game; null before the first move.</summary>
        public MoveRecord LastMove { get; private set; } = null;

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

            // The player whose clock runs out loses.
            Player1.Timer.TimeUp += () => OnTimeUp(Player1);
            Player2.Timer.TimeUp += () => OnTimeUp(Player2);

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
            if (IsPaused || IsGameOver)
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
            if (IsPaused || IsGameOver)
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
            LastMove = new MoveRecord(piece.CurrentInfo.Clone(), fromX, fromY, toX, toY, targetPiece?.CurrentInfo.Clone());

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

            // Standard xiangqi: the side about to move is evaluated right away. With no
            // legal move it loses on the spot — checkmate if in check, otherwise stalemate
            // (困斃, which also covers "every remaining move would face the Generals").
            // The turn is not handed over, so the loser's clock never starts.
            if (Board.UsesCheckRules)
            {
                var mover = piece.Side;
                var opponent = OpponentOf(mover);
                bool opponentInCheck = Board.IsSideInCheck(opponent);

                if (!Board.HasAnyLegalMove(opponent))
                {
                    EndGame(mover, opponent, opponentInCheck ? GameOverReason.Checkmate : GameOverReason.Stalemate);
                    return;
                }

                // Set before the turn switch so TurnChanged handlers already see it.
                IsInCheck = opponentInCheck;
                SwitchTurn();
                if (opponentInCheck)
                {
                    AppLogger.Log($"(Check) {opponent} is in check", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Check) {opponent} is in check");
                    Check?.Invoke(opponent);
                }
                return;
            }

            SwitchTurn();
        }

        private static PlayerSide OpponentOf(PlayerSide side) =>
            side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;

        /// <summary>
        /// <paramref name="side"/> resigns and loses immediately (also allowed while
        /// paused). Returns false if the game is already over or the side is not one of
        /// the two players.
        /// </summary>
        public bool Resign(PlayerSide side)
        {
            if (IsGameOver || (side != PlayerSide.Player1 && side != PlayerSide.Player2))
                return false;

            // Leave the pause state first so the ended game is not also "paused".
            if (IsPaused)
                ResumeGame();

            EndGame(OpponentOf(side), side, GameOverReason.Resign);
            return true;
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
        /// Also clears the pause state (Reset() drops a clock's Paused state) and the
        /// game-over state.
        /// </summary>
        private void ResetTimers(bool startFirstTurn)
        {
            Player1.Timer.Reset();
            Player2.Timer.Reset();
            IsPaused = false;
            IsGameOver = false;
            Winner = PlayerSide.None;
            Result = null;
            IsInCheck = false;
            LastMove = null;
            if (startFirstTurn)
                Player1.Timer.StartStep();
        }

        /// <summary>
        /// A clock ran out (PlayerTimer has already set itself Terminated). With
        /// <c>Rules.EndGameWhenTimesUp</c> (default) its owner loses; otherwise the game
        /// goes on with that clock stopped.
        /// </summary>
        private void OnTimeUp(Player loser)
        {
            if (IsGameOver)
                return;

            if (!Board.GameRules.EndGameWhenTimesUp)
            {
                AppLogger.Log($"(Timer) {loser.Side} ran out of time (EndGameWhenTimesUp is off)", LogLevel.DEBUG);
                Logger?.AddMessage($"(Timer) {loser.Side} ran out of time");
                return;
            }

            var winner = loser == Player1 ? Player2.Side : Player1.Side;
            EndGame(winner, loser.Side, GameOverReason.TimeUp);
        }

        /// <summary>
        /// Ends the game: stops both clocks, drops the selection, blocks further input
        /// and raises <see cref="GameOver"/> with a snapshot of the final position (and,
        /// for checkmate, the pieces giving check).
        /// </summary>
        private void EndGame(PlayerSide winner, PlayerSide loser, GameOverReason reason)
        {
            if (IsGameOver)
                return;

            var finalBoard = new List<PieceInfo>();
            foreach (var p in Board.GetAllPieces())
                finalBoard.Add(p.CurrentInfo.Clone());

            var checking = new List<PieceInfo>();
            if (reason == GameOverReason.Checkmate)
            {
                foreach (var p in Board.GetCheckingPieces(loser))
                    checking.Add(p.CurrentInfo.Clone());
            }

            IsGameOver = true;
            Winner = winner;
            // Nobody is "to move" any more; a checkmate is reported through Result.Reason.
            IsInCheck = false;
            Result = new GameOverInfo(winner, loser, reason, finalBoard, LastMove, checking);

            Player1.Timer.End();
            Player2.Timer.End();

            if (selectedPiece != null)
            {
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
            }

            AppLogger.Log($"(Game over) {winner} wins ({reason})", LogLevel.DEBUG);
            Logger?.AddMessage($"(Game over) {winner} wins ({reason})");
            GameOver?.Invoke(Result);
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
            if (IsPaused || IsGameOver)
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