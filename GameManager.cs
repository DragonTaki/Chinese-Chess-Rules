/* ----- ----- ----- ----- */
// GameManager.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/09/30
// Version: v1.5
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Endgames;
using Chinese_Chess_v3.Game.Core.Notation;
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

        /// <summary>
        /// The legal destinations of <see cref="SelectedPiece"/> (empty when nothing is
        /// selected), for the UI's move hints. Computed on each call - read it once per
        /// selection (e.g. in a <see cref="PieceSelected"/> handler), not per frame.
        /// </summary>
        public List<(int x, int y)> SelectedPieceLegalMoves =>
            selectedPiece == null ? new List<(int x, int y)>() : selectedPiece.GetLegalMoves(Board);

        /// <summary>
        /// Hanging pieces of both sides (無根子可被吃, see
        /// <see cref="BoardAnalysis.GetHangingPieces"/>), for the UI's board hints.
        /// Recomputed only after each move and when the board is reset, loaded or cleared;
        /// <see cref="HangingPiecesChanged"/> is raised each time.
        /// </summary>
        public IReadOnlyList<Piece> HangingPieces { get; private set; } = new List<Piece>();

        /// <summary>
        /// Raised after <see cref="HangingPieces"/> is recomputed (after each move and after
        /// a board reset/load/clear, following <see cref="BoardReset"/> and the
        /// <see cref="PieceAdded"/> events), with the new list.
        /// </summary>
        public event Action<IReadOnlyList<Piece>> HangingPiecesChanged;

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

        /// <summary>
        /// Raised once after each move that has at least one tactical event (將軍, 絕殺,
        /// 抽車, 吃車, ... see <see cref="TacticalEventType"/>), with all of that move's
        /// events in <see cref="TacticalEventType"/> order. Only on boards that use check
        /// rules. Raised last, after <see cref="Check"/> / <see cref="GameOver"/>, so the
        /// game state (turn, <see cref="IsInCheck"/>, <see cref="IsGameOver"/>) is final.
        /// Intended for sound / visual effects; each event is also written to the game log.
        /// </summary>
        public event Action<IReadOnlyList<TacticalEvent>> TacticalEvents;

        /// <summary>The most recent move of this game; null before the first move.</summary>
        public MoveRecord LastMove { get; private set; } = null;

        private readonly List<MoveRecord> moves = new List<MoveRecord>();
        private readonly IReadOnlyList<MoveRecord> movesView;

        /// <summary>
        /// Every move of the current game in order (<c>Moves[i].Ply == i + 1</c>; the last
        /// one is <see cref="LastMove"/>). Emptied by every new game setup
        /// (<see cref="ResetBoardToDefault"/>, <see cref="LoadCustomBoard"/>,
        /// <see cref="StartEndgame"/>, <see cref="ClearBoard"/>). The base for PGN export,
        /// undo and replay (docs/PLAN.md). Read-only view of the live list.
        /// </summary>
        public IReadOnlyList<MoveRecord> Moves => movesView;

        /// <summary>The side that made (or makes) the first move of the current game; with <see cref="Moves"/> it fixes the move numbers.</summary>
        public PlayerSide FirstTurn { get; private set; } = PlayerSide.Player1;

        /// <summary>
        /// Raised once per move after it is appended to <see cref="Moves"/> and the board is
        /// updated (after <see cref="PieceMoved"/>), before the turn switch and before
        /// <see cref="Check"/> / <see cref="GameOver"/> / <see cref="TacticalEvents"/>, so a
        /// move list shows the mating move before the result.
        /// </summary>
        public event Action<MoveRecord> MoveRecorded;

        /// <summary>
        /// The endgame puzzle the current game was started from (<see cref="StartEndgame"/>);
        /// null for any other game. Cleared by <see cref="ResetBoardToDefault"/>,
        /// <see cref="LoadCustomBoard"/> and <see cref="ClearBoard"/>. Already set when
        /// <see cref="BoardReset"/> is raised for the puzzle's position.
        /// </summary>
        public EndgamePuzzle CurrentEndgame { get; private set; } = null;

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
            movesView = moves.AsReadOnly();

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

            UpdateHangingPieces();
        }
        public void SetLogger(IGameLog loggerHandler)
        {
            Logger = loggerHandler ?? throw new ArgumentNullException(nameof(loggerHandler));
        }

        public void ResetBoardToDefault()
        {
            // Load default pieces
            SetUpPosition(BoardConfigLoader.Load(), PlayerSide.Player1, null);
        }

        /// <summary>
        /// Starts a game from <paramref name="customInitialPieces"/> with
        /// <paramref name="firstTurn"/> (Player1 or Player2) to move first.
        /// </summary>
        public void LoadCustomBoard(List<PieceInfo> customInitialPieces, PlayerSide firstTurn = PlayerSide.Player1)
        {
            SetUpPosition(customInitialPieces, firstTurn, null);
        }

        /// <summary>
        /// Starts a game from <paramref name="puzzle"/>'s position (its FEN), with the side to
        /// move from the FEN - Black (Player2) may move first - and keeps the puzzle as
        /// <see cref="CurrentEndgame"/>. The puzzle's solution is not played.
        /// </summary>
        /// <exception cref="FormatException">The puzzle's FEN is not valid (puzzles from
        /// <see cref="EndgameLoader"/> have already been checked).</exception>
        public void StartEndgame(EndgamePuzzle puzzle)
        {
            ArgumentNullException.ThrowIfNull(puzzle);
            var (pieces, sideToMove) = XiangqiFen.Parse(puzzle.Fen);
            SetUpPosition(pieces, sideToMove, puzzle);
            AppLogger.Log($"(Endgame) Started {puzzle.FileName}: {puzzle.Title}, {sideToMove} to move", LogLevel.DEBUG);
            Logger?.AddMessage($"(Endgame) {puzzle.Title} ({puzzle.Goal})");
        }

        /// <summary>
        /// Shared new-game setup: places <paramref name="pieces"/> (resetting the board's turn
        /// counter), clears the selection, gives the move to <paramref name="firstTurn"/>,
        /// resets both clocks and starts <paramref name="firstTurn"/>'s step, then informs the
        /// UI (<see cref="BoardReset"/>, <see cref="PieceAdded"/> per piece) and recomputes
        /// the hanging pieces.
        /// </summary>
        private void SetUpPosition(List<PieceInfo> pieces, PlayerSide firstTurn, EndgamePuzzle puzzle)
        {
            if (firstTurn != PlayerSide.Player1 && firstTurn != PlayerSide.Player2)
                throw new ArgumentException($"The first turn must be Player1 or Player2, not {firstTurn}", nameof(firstTurn));

            // Reset board:
            // (A) Clear pieces
            // (B) Reset turn
            // (C) Recreate pieces
            Board.Initialize(pieces);

            // Reset selected piece
            selectedPiece = null;
            CurrentEndgame = puzzle;
            // Reset side
            CurrentTurn = firstTurn;
            FirstTurn = firstTurn;
            ResetTimers(startFirstTurn: true);
            // A custom position may start with the side to move already in check.
            IsInCheck = Board.UsesCheckRules && Board.IsSideInCheck(firstTurn);

            // Inform UI
            BoardReset?.Invoke();

            // Inform pieces added
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);

            UpdateHangingPieces();
        }

        public void ClearBoard()
        {
            // Clear board:
            // (A) Clear pieces
            // (B) Reset turn
            Board.Clear();

            // Reset selected piece
            selectedPiece = null;
            CurrentEndgame = null;
            // Reset side
            CurrentTurn = PlayerSide.Player1;
            FirstTurn = PlayerSide.Player1;
            ResetTimers(startFirstTurn: false);

            // Inform UI
            BoardReset?.Invoke();

            UpdateHangingPieces();
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

            // Pre-move facts for the "newly ..." tactical events, taken on the unchanged board.
            var tacticalBefore = Board.UsesCheckRules ? TacticalAnalysis.TakeSnapshot(Board, piece.Side) : null;

            // The notation also depends on the other pieces on the file (前/後), and whether the
            // move gives check is simulated, so the record is complete before the board changes.
            string notation = ChineseMoveNotation.Format(Board, fromX, fromY, toX, toY);
            string iccs = Board.Type == BoardType.Full ? new IccsMove(fromX, fromY, toX, toY).ToString() : null;
            var opponentSide = OpponentOf(piece.Side);
            bool givesCheck = Board.UsesCheckRules &&
                Board.SimulateMove(piece, toX, toY, () => Board.IsSideInCheck(opponentSide), fallback: false);

            Board.AdvanceTurn();

            // If the destination has an (enemy) piece, capture it first
            var targetPiece = Board.GetPiece(toX, toY);
            int ply = moves.Count + 1;
            // Black-first games (endgames) number like PGN: Black's first move is 1, Red's reply 2.
            int moveNumber = (ply - 1 + (FirstTurn == PlayerSide.Player2 ? 1 : 0)) / 2 + 1;
            LastMove = new MoveRecord(piece.CurrentInfo.Clone(), fromX, fromY, toX, toY, targetPiece?.CurrentInfo.Clone(),
                ply, moveNumber, givesCheck, notation, iccs);
            moves.Add(LastMove);

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

            // Readable move-list line, in addition to the debug lines above.
            if (LastMove.Notation != null)
            {
                string line = FormatMoveLine(LastMove);
                AppLogger.Log(line, LogLevel.DEBUG);
                Logger?.AddMessage(line);
            }
            MoveRecorded?.Invoke(LastMove);

            // unselect and notify
            if (selectedPiece != null)
            {
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
            }

            // Board hints for the new position (also when this move ends the game).
            UpdateHangingPieces();

            // Standard xiangqi: the side about to move is evaluated right away. With no
            // legal move it loses on the spot — checkmate if in check, otherwise stalemate
            // (困斃, which also covers "every remaining move would face the Generals").
            // The turn is not handed over, so the loser's clock never starts.
            if (Board.UsesCheckRules)
            {
                var mover = piece.Side;
                var opponent = OpponentOf(mover);
                bool opponentInCheck = Board.IsSideInCheck(opponent);

                // Evaluated before the game-over / turn-switch bookkeeping (the board is
                // already final), raised after it.
                var tactical = TacticalAnalysis.Analyze(Board, LastMove, tacticalBefore);

                if (!Board.HasAnyLegalMove(opponent))
                {
                    EndGame(mover, opponent, opponentInCheck ? GameOverReason.Checkmate : GameOverReason.Stalemate);
                    RaiseTacticalEvents(tactical);
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
                RaiseTacticalEvents(tactical);
                return;
            }

            SwitchTurn();
        }

        /// <summary>
        /// Writes one game-log line per tactical event (Chinese name, event type, mover,
        /// move and involved pieces) and raises <see cref="TacticalEvents"/> if any.
        /// </summary>
        private void RaiseTacticalEvents(List<TacticalEvent> events)
        {
            if (events.Count == 0)
                return;

            foreach (var e in events)
            {
                var m = e.Move;
                string involved = e.Pieces.Count == 0
                    ? "-"
                    : string.Join(", ", e.Pieces.Select(p => $"{p.Side} {p.Type} ({p.X},{p.Y})"));
                string line = $"(Tactic) {e.ChineseName} [{e.Type}] {e.Mover} {m.Piece.Type} ({m.FromX},{m.FromY})->({m.ToX},{m.ToY}); pieces: {involved}";
                AppLogger.Log(line, LogLevel.DEBUG);
                Logger?.AddMessage(line);
            }
            TacticalEvents?.Invoke(events);
        }

        /// <summary>Recomputes <see cref="HangingPieces"/> and raises <see cref="HangingPiecesChanged"/>.</summary>
        private void UpdateHangingPieces()
        {
            HangingPieces = BoardAnalysis.GetHangingPieces(Board);
            HangingPiecesChanged?.Invoke(HangingPieces);
        }

        /// <summary>
        /// The game-log line of a move: <c>第{MoveNumber}手 紅：{Notation}</c> or
        /// <c>第{MoveNumber}手 黑：{Notation}</c> (e.g. <c>第1手 紅：炮二平五</c>). The side name is
        /// fixed by player (Player1 紅, Player2 黑) like the notation's piece characters.
        /// </summary>
        public static string FormatMoveLine(MoveRecord move) =>
            $"第{move.MoveNumber}手 {(move.Side == PlayerSide.Player1 ? "紅" : "黑")}：{move.Notation}";

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
        /// Clears both clocks for a new game; optionally starts the first step of the side
        /// to move (<see cref="CurrentTurn"/>, set before this is called).
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
            moves.Clear();
            if (startFirstTurn)
                (CurrentTurn == PlayerSide.Player2 ? Player2 : Player1).Timer.StartStep();
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