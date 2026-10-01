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
using System.IO;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Endgames;
using Chinese_Chess_v3.Game.Core.Notation;
using Chinese_Chess_v3.Game.Core.Openings;
using Chinese_Chess_v3.Game.Core.Pgn;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;
using Chinese_Chess_v3.Game.Core.Saves;

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
        /// <see cref="StartEndgame"/>, <see cref="StartOpening"/> (before its line is
        /// played), <see cref="ClearBoard"/>). The base for PGN export,
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

        // Parallel to `moves`: the captured Piece object of each move (null for a quiet
        // move), put back on the board by Undo, and both clocks as they were just before the
        // move, restored by Undo.
        private readonly List<Piece> capturedPieces = new List<Piece>();
        private readonly List<(ClockState Player1, ClockState Player2)> clocksBeforeMove = new List<(ClockState, ClockState)>();

        /// <summary>
        /// How many leading moves of <see cref="Moves"/> cannot be undone: the opening line
        /// played by <see cref="StartOpening"/> (its length); 0 for every other game (a normal
        /// game undoes back to its start position, an endgame back to the puzzle's position).
        /// Kept when a saved game is loaded (<c>[PresetPlies]</c>).
        /// </summary>
        public int UndoFloor { get; private set; } = 0;

        /// <summary>How many moves one <see cref="Undo"/> takes back: a round, the last move of each side.</summary>
        public const int UndoRoundPlies = 2;

        /// <summary>
        /// Whether <see cref="Undo"/> can take a round back: there are at least
        /// <see cref="UndoRoundPlies"/> moves above <see cref="UndoFloor"/> (e.g. not with Black
        /// to move after Red's first move). Also true after the game has ended (undoing
        /// reopens it) and while paused.
        /// </summary>
        public bool CanUndo => moves.Count - UndoFloor >= UndoRoundPlies;

        /// <summary>
        /// Raised once per move taken back (twice per <see cref="Undo"/>, newest move first),
        /// last for that move, after the board, move list, turn, check flag, game-over state,
        /// clocks and hanging pieces are all back to the position before the move, with the
        /// record that was taken back (no longer in <see cref="Moves"/>). The board change itself is raised before
        /// it: <see cref="PieceMoved"/> for the piece going back to its from-square and
        /// <see cref="PieceAdded"/> for a captured piece returning.
        /// </summary>
        public event Action<MoveRecord> MoveUndone;

        private bool hasUnsavedChanges = false;

        /// <summary>
        /// Whether the game has changed since it was last started, saved or loaded: set by
        /// every move (<see cref="HandleClick"/>, <see cref="TryMove"/>) and every
        /// <see cref="Undo"/>; cleared by every new game setup (<see cref="ResetBoardToDefault"/>,
        /// <see cref="LoadCustomBoard"/>, <see cref="StartEndgame"/>, <see cref="StartOpening"/>
        /// (after its preset line), <see cref="ClearBoard"/>) and by saving or loading a game.
        /// For the UI's "save before leaving?" prompt.
        /// </summary>
        public bool HasUnsavedChanges
        {
            get => hasUnsavedChanges;
            private set
            {
                if (hasUnsavedChanges != value)
                {
                    hasUnsavedChanges = value;
                    UnsavedChangesChanged?.Invoke(hasUnsavedChanges);
                }
            }
        }

        /// <summary>Raised when <see cref="HasUnsavedChanges"/> changes, with the new value.</summary>
        public event Action<bool> UnsavedChangesChanged;

        /// <summary>
        /// The endgame puzzle the current game was started from (<see cref="StartEndgame"/>);
        /// null for any other game. Cleared by <see cref="ResetBoardToDefault"/>,
        /// <see cref="LoadCustomBoard"/> and <see cref="ClearBoard"/>. Already set when
        /// <see cref="BoardReset"/> is raised for the puzzle's position.
        /// </summary>
        public EndgamePuzzle CurrentEndgame { get; private set; } = null;

        /// <summary>
        /// The opening the current game was started from (<see cref="StartOpening"/>); null
        /// for any other game. Cleared like <see cref="CurrentEndgame"/>. Already set when
        /// <see cref="BoardReset"/> is raised and while the opening line is played.
        /// </summary>
        public OpeningLine CurrentOpening { get; private set; } = null;

        /// <summary>
        /// What kind of game this is: <see cref="GameMode.Endgame"/> after
        /// <see cref="StartEndgame"/>, <see cref="GameMode.Opening"/> after
        /// <see cref="StartOpening"/>, <see cref="GameMode.Normal"/> after any other setup; a
        /// loaded saved game gets the mode it was saved with. Decides the saved game's
        /// <c>[Event]</c> and its save folder / file name.
        /// </summary>
        public GameMode Mode { get; private set; } = GameMode.Normal;

        /// <summary>
        /// The 4-digit Id of the endgame puzzle / opening file the game was started from (also
        /// after loading a saved game of it, when <see cref="CurrentEndgame"/> /
        /// <see cref="CurrentOpening"/> are null); null for a normal game or when unknown.
        /// </summary>
        public string OriginId { get; private set; } = null;

        /// <summary>
        /// The title of the endgame puzzle / opening the game was started from (kept like
        /// <see cref="OriginId"/>); null for a normal game. Names the save file of those modes.
        /// </summary>
        public string OriginTitle { get; private set; } = null;

        /// <summary>
        /// The FEN of the position the current game started from (set by every new game setup;
        /// for an opening the position before its preset line), with <see cref="FirstTurn"/>
        /// to move: with <see cref="Moves"/> it is the whole game, the <c>[FEN]</c> of a saved
        /// game. Null off the Full board and after <see cref="ClearBoard"/>.
        /// </summary>
        public string InitialFen { get; private set; } = null;

        /// <summary>Whether the game can be saved (<see cref="SaveGame"/>): a Full-board game with a known <see cref="InitialFen"/>.</summary>
        public bool CanSave => Board.Type == BoardType.Full && InitialFen != null;

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

        /// <summary>A game with the default <see cref="Rules"/>.</summary>
        public GameManager() : this(null) { }

        /// <summary>
        /// A game played by <paramref name="rules"/> (null: the default <see cref="Rules"/>):
        /// the board's rule toggles and the players' clocks (total / step time, increment,
        /// step timer on/off, count mode) all come from it. The launchers pass the rules
        /// built from the player settings (docs/SETTINGS.md).
        /// </summary>
        public GameManager(Rules rules)
        {
            movesView = moves.AsReadOnly();
            rules ??= new Rules();

            // Initialize the board
            Board = new Board(BoardType.Full, rules);
            Board.Initialize(BoardConfigLoader.Load());
            CurrentTurn = PlayerSide.Player1;
            InitialFen = FormatInitialFen(PlayerSide.Player1);
            selectedPiece = null;
            Player1 = new Player(PlayerSide.Player1, rules.TotalTimeLimit, rules.StepTimeLimit, rules.IncrementPerMove, rules.EnableStepTimer, rules.TimerMode);
            Player2 = new Player(PlayerSide.Player2, rules.TotalTimeLimit, rules.StepTimeLimit, rules.IncrementPerMove, rules.EnableStepTimer, rules.TimerMode);

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
        /// Starts a game from <paramref name="opening"/>: its position (the standard start
        /// position unless the file has a <c>[FEN]</c>), then its line played move by move
        /// through <see cref="TryMove"/>, so <see cref="Moves"/>, <see cref="MoveRecorded"/>
        /// and the game log show it as if it had been played. The side to move afterwards is
        /// <see cref="PgnGameFile.SideToMoveAfterMoves"/>; both clocks are then reset (the
        /// line took no thinking time) and that side's step clock started. Keeps the opening
        /// as <see cref="CurrentOpening"/>.
        /// </summary>
        /// <returns>The number of moves of the line that were played: all of them, unless a
        /// move was not legal (openings from <see cref="OpeningLoader"/> have already been
        /// checked), in which case the line stops there.</returns>
        /// <exception cref="FormatException">The opening's FEN is not valid.</exception>
        public int StartOpening(OpeningLine opening)
        {
            ArgumentNullException.ThrowIfNull(opening);
            var (pieces, sideToMove) = XiangqiFen.Parse(opening.Fen);
            SetUpPosition(pieces, sideToMove, opening);
            Logger?.AddMessage(opening.Ecco != null ? $"(Opening) {opening.Title} ({opening.Ecco})" : $"(Opening) {opening.Title}");

            int played = 0;
            foreach (var move in opening.Moves)
            {
                if (!TryMove(move.FromX, move.FromY, move.ToX, move.ToY))
                {
                    AppLogger.Log($"(Opening) {opening.FileName}: move {played + 1} ({move}) is not legal here; line stopped", LogLevel.WARN);
                    break;
                }
                played++;
            }

            // The opening line is the preset part of the game: it cannot be undone.
            UndoFloor = played;
            if (!IsGameOver)
                RestartClocks();
            // The preset line is part of the new game, not a change to it.
            HasUnsavedChanges = false;
            AppLogger.Log($"(Opening) Started {opening.FileName}: {opening.Title}, {played} move(s) played, {CurrentTurn} to move", LogLevel.DEBUG);
            return played;
        }

        /// <summary>
        /// Shared new-game setup: places <paramref name="pieces"/> (resetting the board's turn
        /// counter), clears the selection, gives the move to <paramref name="firstTurn"/>,
        /// resets both clocks and starts <paramref name="firstTurn"/>'s step, then informs the
        /// UI (<see cref="BoardReset"/>, <see cref="PieceAdded"/> per piece) and recomputes
        /// the hanging pieces.
        /// </summary>
        /// <param name="source">The file the game starts from (an endgame puzzle, an opening, a
        /// saved game), or null; sets <see cref="Mode"/>, <see cref="OriginId"/> and
        /// <see cref="OriginTitle"/>.</param>
        private void SetUpPosition(List<PieceInfo> pieces, PlayerSide firstTurn, PgnGameFile source)
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
            CurrentEndgame = source as EndgamePuzzle;
            CurrentOpening = source as OpeningLine;
            (Mode, OriginId, OriginTitle) = source switch
            {
                EndgamePuzzle puzzle => (GameMode.Endgame, puzzle.Id, puzzle.Title),
                OpeningLine opening => (GameMode.Opening, opening.Id, opening.Title),
                SavedGame saved => (saved.Mode, saved.OriginId, saved.OriginTitle),
                _ => (GameMode.Normal, (string)null, (string)null),
            };
            // Reset side
            CurrentTurn = firstTurn;
            FirstTurn = firstTurn;
            InitialFen = FormatInitialFen(firstTurn);
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

        /// <summary>The FEN of the board's current pieces with <paramref name="sideToMove"/>; null off the Full board or when the pieces cannot be written as FEN.</summary>
        private string FormatInitialFen(PlayerSide sideToMove)
        {
            if (Board.Type != BoardType.Full)
                return null;
            try
            {
                return XiangqiFen.Format(Board, sideToMove);
            }
            catch (ArgumentException ex)
            {
                AppLogger.Log($"(Save) Start position has no FEN: {ex.Message}", LogLevel.WARN);
                return null;
            }
        }

        /// <summary>
        /// The current game as saved-game PGN text (<see cref="SavedGamePgn"/>): start position,
        /// every move (ICCS with the Chinese notation as comments), mode, origin, preset plies,
        /// result. Does not change <see cref="HasUnsavedChanges"/>.
        /// </summary>
        /// <param name="date">The <c>[Date]</c>; null for now.</param>
        /// <exception cref="InvalidOperationException">Not <see cref="CanSave"/>.</exception>
        public string ExportPgn(string redName, string blackName, DateTime? date = null) =>
            SavedGamePgn.Write(this, redName, blackName, date ?? DateTime.Now);

        /// <summary>
        /// Writes the current game (<see cref="ExportPgn"/>) to <paramref name="filePath"/>
        /// (UTF-8, its folder created when missing; an existing file is overwritten) and clears
        /// <see cref="HasUnsavedChanges"/>. Where and under which name is the caller's choice
        /// (the game's rules are in <c>SystemSettings</c> / <c>GameSaveFiles</c>).
        /// </summary>
        /// <returns><paramref name="filePath"/>.</returns>
        /// <exception cref="InvalidOperationException">Not <see cref="CanSave"/>.</exception>
        /// <exception cref="IOException">The file cannot be written (also
        /// <see cref="UnauthorizedAccessException"/>).</exception>
        public string SaveGame(string filePath, string redName, string blackName, DateTime? date = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            string text = ExportPgn(redName, blackName, date);

            string folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(filePath, text);

            HasUnsavedChanges = false;
            AppLogger.Log($"(Save) Saved {Moves.Count} move(s) to {filePath}", LogLevel.DEBUG);
            Logger?.AddMessage($"(Save) {Path.GetFileName(filePath)}");
            return filePath;
        }

        /// <summary>
        /// Starts <paramref name="saved"/> again: sets up its start position (<c>[FEN]</c>) and
        /// replays every move through <see cref="TryMove"/> - so <see cref="Moves"/>,
        /// <see cref="MoveRecorded"/>, the game log, check and a checkmate / stalemate come
        /// back exactly as played. Restores <see cref="Mode"/>, <see cref="OriginId"/>,
        /// <see cref="OriginTitle"/> and the undo floor (<c>[PresetPlies]</c>);
        /// <see cref="CurrentEndgame"/> / <see cref="CurrentOpening"/> stay null (the original
        /// file is not looked up). A game saved after a resignation or a time-up is ended the
        /// same way again (<c>[Result]</c> + <c>[Termination]</c>). Clocks: both start fresh
        /// (zero elapsed) and the side to move's step clock is started - the saved file holds
        /// no clock times. Ends clean (<see cref="HasUnsavedChanges"/> false).
        /// </summary>
        /// <returns>The number of moves replayed: all of them unless one is not legal (files
        /// from <see cref="SavedGameLoader"/> have already been checked), where replay stops.</returns>
        /// <exception cref="FormatException">The saved game's FEN is not valid.</exception>
        public int LoadSavedGame(SavedGame saved)
        {
            ArgumentNullException.ThrowIfNull(saved);
            var (pieces, sideToMove) = XiangqiFen.Parse(saved.Fen);
            SetUpPosition(pieces, sideToMove, saved);
            Logger?.AddMessage($"(Load) {saved.Title}");

            int played = 0;
            foreach (var move in saved.Moves)
            {
                if (!TryMove(move.FromX, move.FromY, move.ToX, move.ToY))
                {
                    AppLogger.Log($"(Load) {saved.FileName}: move {played + 1} ({move}) is not legal here; replay stopped", LogLevel.WARN);
                    break;
                }
                played++;
            }

            UndoFloor = Math.Min(saved.PresetPlies, played);

            // An ending that is not a move (resignation, time-up) is not replayed by the moves.
            if (!IsGameOver && played == saved.Moves.Count && saved.Winner != PlayerSide.None &&
                saved.Termination is GameOverReason.Resign or GameOverReason.TimeUp)
            {
                EndGame(saved.Winner, OpponentOf(saved.Winner), saved.Termination.Value);
            }

            if (!IsGameOver)
                RestartClocks();
            HasUnsavedChanges = false;
            AppLogger.Log($"(Load) Loaded {saved.FileName}: {played} move(s) replayed, {CurrentTurn} to move, over: {IsGameOver}", LogLevel.DEBUG);
            return played;
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
            CurrentOpening = null;
            Mode = GameMode.Normal;
            OriginId = null;
            OriginTitle = null;
            InitialFen = null;
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

            // Both clocks as they are right before the move, for Undo.
            var clocks = (Player1.Timer.GetClockState(), Player2.Timer.GetClockState());

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
            capturedPieces.Add(targetPiece);
            clocksBeforeMove.Add(clocks);
            HasUnsavedChanges = true;

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
        /// The game-log line of a move: <c>第{MoveNumber}回合 紅：{Notation}</c> or
        /// <c>第{MoveNumber}回合 黑：{Notation}</c> (e.g. <c>第1回合 紅：炮二平五</c>). The side name is
        /// fixed by player (Player1 紅, Player2 黑) like the notation's piece characters.
        /// </summary>
        public static string FormatMoveLine(MoveRecord move) =>
            $"第{move.MoveNumber}回合 {(move.Side == PlayerSide.Player1 ? "紅" : "黑")}：{move.Notation}";

        /// <summary>
        /// Takes back one round (悔棋): the last move of each side (<see cref="UndoRoundPlies"/>
        /// moves, newest first, each as <see cref="UndoLastMove"/> describes), if
        /// <see cref="CanUndo"/>. The side that was to move is to move again in the position
        /// before its opponent's last move and its own move before that, with the clocks as
        /// they were then. An ended game is reopened the same way (its last two moves are taken
        /// back, whatever ended it). Allowed while paused (the game stays paused).
        /// </summary>
        /// <returns>The records taken back, newest first; empty when nothing could be undone.</returns>
        public IReadOnlyList<MoveRecord> Undo()
        {
            if (!CanUndo)
                return Array.Empty<MoveRecord>();

            var undone = new List<MoveRecord>(UndoRoundPlies);
            for (int i = 0; i < UndoRoundPlies; i++)
                undone.Add(UndoLastMove());
            return undone;
        }

        /// <summary>
        /// Takes back the single last move (<see cref="Moves"/>' last record; one half of
        /// <see cref="Undo"/>), if there is one above <see cref="UndoFloor"/>: the piece
        /// returns to its from-square and a captured piece to
        /// the to-square (the same piece objects), the board's turn counter steps back, the
        /// move leaves <see cref="Moves"/>, the mover is to move again, <see cref="IsInCheck"/>
        /// is recomputed for the mover, the hanging pieces are recomputed, and an ended game
        /// is reopened (game over cleared, whatever ended it - the undone move, a resignation
        /// or a time-up). The selection is dropped first. Allowed while paused (the game stays
        /// paused).
        /// <para>
        /// Clocks: both clocks go back to their state just before the undone move was made
        /// (elapsed step and total time; the mover's increment for that move is taken back
        /// with it), and the mover's step clock runs again from there (held paused when the
        /// game is paused); the opponent's time spent on the undone position is not charged.
        /// </para>
        /// Events, in order: <see cref="PieceUnselected"/> (if a piece was selected),
        /// <see cref="PieceMoved"/> (piece, fromX, fromY), <see cref="PieceAdded"/> (the
        /// captured piece, if any), <see cref="TurnChanged"/>,
        /// <see cref="HangingPiecesChanged"/>, <see cref="MoveUndone"/>.
        /// </summary>
        /// <returns>The record that was taken back; null when nothing could be undone.</returns>
        private MoveRecord UndoLastMove()
        {
            if (moves.Count <= UndoFloor)
                return null;

            if (selectedPiece != null)
            {
                PieceUnselected?.Invoke(selectedPiece);
                selectedPiece = null;
            }

            int last = moves.Count - 1;
            var record = moves[last];
            var captured = capturedPieces[last];
            var clocks = clocksBeforeMove[last];
            var piece = Board.GetPiece(record.ToX, record.ToY);

            Board.UnmakeMove(piece, record.FromX, record.FromY, record.ToX, record.ToY, captured);
            Board.RetreatTurn();
            moves.RemoveAt(last);
            capturedPieces.RemoveAt(last);
            clocksBeforeMove.RemoveAt(last);
            LastMove = moves.Count > 0 ? moves[moves.Count - 1] : null;
            HasUnsavedChanges = true;

            AppLogger.Log($"(Undo) {piece.Type} back to ({record.FromX},{record.FromY})", LogLevel.DEBUG);
            Logger?.AddMessage(record.Notation != null ? $"(Undo) {FormatMoveLine(record)}" : $"(Undo) {piece.Type} back to ({record.FromX},{record.FromY})");

            PieceMoved?.Invoke(piece, record.FromX, record.FromY);
            if (captured != null)
                PieceAdded?.Invoke(captured);

            // Reopen an ended game.
            IsGameOver = false;
            Winner = PlayerSide.None;
            Result = null;

            var mover = record.Side;
            Player1.Timer.RestoreClockState(clocks.Player1, active: mover == PlayerSide.Player1, paused: IsPaused);
            Player2.Timer.RestoreClockState(clocks.Player2, active: mover == PlayerSide.Player2, paused: IsPaused);

            // Set before the turn switch so TurnChanged handlers already see it.
            IsInCheck = Board.UsesCheckRules && Board.IsSideInCheck(mover);
            CurrentTurn = mover;

            UpdateHangingPieces();
            MoveUndone?.Invoke(record);
            return record;
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
        /// Clears both clocks for a new game; optionally starts the first step of the side
        /// to move (<see cref="CurrentTurn"/>, set before this is called).
        /// Also clears the pause state (Reset() drops a clock's Paused state) and the
        /// game-over state.
        /// </summary>
        /// <summary>
        /// Both clocks back to their start values and the side to move's step clock started,
        /// keeping the moves and the game state (unlike <see cref="ResetTimers"/>). Used after
        /// an opening line has been played onto the board.
        /// </summary>
        private void RestartClocks()
        {
            Player1.Timer.Reset();
            Player2.Timer.Reset();
            (CurrentTurn == PlayerSide.Player2 ? Player2 : Player1).Timer.StartStep();
        }

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
            capturedPieces.Clear();
            clocksBeforeMove.Clear();
            UndoFloor = 0;
            HasUnsavedChanges = false;
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