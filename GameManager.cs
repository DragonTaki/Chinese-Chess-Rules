/* ----- ----- ----- ----- */
// GameManager.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/02
// Version: v1.7
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
using Engine.Randomization;

namespace Chinese_Chess_v3.Game.Core
{
    public class GameManager
    {
        public IGameLog Logger { get; private set; }

        /// <summary>
        /// The board of the current game. Replaced by a new <see cref="Boards.Board"/> instance
        /// when a game is set up on a different <see cref="BoardType"/> (see
        /// <see cref="LoadCustomBoard"/>), so read it from here each time instead of keeping it.
        /// </summary>
        public Board Board { get; private set; }
        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }
        private PlayerSide _currentTurn = PlayerSide.Player1;
        public PlayerSide CurrentTurn
        {
            get => _currentTurn;
            private set
            {
                if (_currentTurn != value)
                {
                    _currentTurn = value;
                    TurnChanged?.Invoke(_currentTurn);
                }
            }
        }
        public event Action<PlayerSide> TurnChanged;

#nullable enable
        private Piece? _selectedPiece;
        public Piece? SelectedPiece => _selectedPiece;
#nullable disable

        /// <summary>
        /// The legal destinations of <see cref="SelectedPiece"/> (empty when nothing is
        /// selected), for the UI's move hints. Computed on each call - read it once per
        /// selection (e.g. in a <see cref="PieceSelected"/> handler), not per frame.
        /// </summary>
        public List<(int x, int y)> SelectedPieceLegalMoves =>
            _selectedPiece == null ? new List<(int x, int y)>() : _selectedPiece.GetLegalMoves(Board);

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

        private bool _isPaused = false;

        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    PausedChanged?.Invoke(_isPaused);
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

        /// <summary>
        /// True while <see cref="LoadSavedGame"/> replays a saved game: a <see cref="GameOver"/>
        /// raised then is the saved game's old ending coming back, not a game just ended, so
        /// the UI does not announce it.
        /// </summary>
        public bool IsReplaying { get; private set; } = false;

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

        private readonly List<MoveRecord> _moves = new List<MoveRecord>();
        private readonly IReadOnlyList<MoveRecord> _movesView;

        /// <summary>
        /// Every move of the current game in order (<c>Moves[i].Ply == i + 1</c>; the last
        /// one is <see cref="LastMove"/>). Emptied by every new game setup
        /// (<see cref="ResetBoardToDefault"/>, <see cref="LoadCustomBoard"/>,
        /// <see cref="StartEndgame"/>, <see cref="StartOpening"/> (before its line is
        /// played), <see cref="ClearBoard"/>). The base for PGN export,
        /// undo and replay (docs/PLAN.md). Read-only view of the live list.
        /// </summary>
        public IReadOnlyList<MoveRecord> Moves => _movesView;

        /// <summary>
        /// The colour <see cref="PlayerSide.Player1"/> plays on a board without dark-chess rules
        /// (see <see cref="ColorOf"/>): on the Full board the colour that moves first in the start
        /// position (FEN <c>w</c> Red, <c>b</c> Black); Red on any other such board. Player2 plays
        /// the other colour. Set by every new game setup.
        /// </summary>
        private PieceColor _player1Color = PieceColor.Red;

        /// <summary>
        /// 己方: the side the player plays in the current game, shown on the left of the info board
        /// (and, later, at the bottom of the board). A new local Full-board game: Player1 (the
        /// first mover); an endgame puzzle: Player1, the side that solves it (the side to move at
        /// the start); an opening: its <see cref="OpeningLine.PlayerSide"/>; a saved game: its
        /// <c>[PlayerSide]</c>; a custom position: the side chosen for it
        /// (<see cref="LoadCustomBoard"/>); a half board: Player1, who moves first. A restart
        /// keeps it. Written to saved games as the player's number
        /// (<c>[PlayerSide "1"]</c>/<c>"2"</c>).
        /// </summary>
        public PlayerSide LocalSide { get; private set; } = PlayerSide.Player1;

        /// <summary>
        /// Raised once per move after it is appended to <see cref="Moves"/> and the board is
        /// updated (after <see cref="PieceMoved"/>), before the turn switch and before
        /// <see cref="Check"/> / <see cref="GameOver"/> / <see cref="TacticalEvents"/>, so a
        /// move list shows the mating move before the result.
        /// </summary>
        public event Action<MoveRecord> MoveRecorded;

        // Parallel to `_moves`: the captured Piece object of each move (null for a quiet
        // move), put back on the board by Undo, and both clocks as they were just before the
        // move, restored by Undo (null when unknown: moves replayed from a saved game).
        private readonly List<Piece> _capturedPieces = new List<Piece>();
        private readonly List<(ClockState Player1, ClockState Player2)?> _clocksBeforeMove = new List<(ClockState, ClockState)?>();
        // Also parallel to `_moves`: for a dark-chess flip or hidden capture, every piece the
        // action changed and how many history snapshots it added (taken back by
        // Board.RevertStates); null for an ordinary move (taken back by Board.UnmakeMove).
        private readonly List<List<(Piece piece, int snapshots)>> _stateChanges = new List<List<(Piece piece, int snapshots)>>();

        /// <summary>
        /// The rules new games start with, one set per <see cref="GameKind"/> (the constructor's;
        /// the launchers pass the player settings' rule sets, and the settings menu edits these
        /// objects in place). Never played by directly: each game takes its own copy of its
        /// kind's rules when it starts (see <see cref="Rules"/>), so changing them only affects
        /// the next game started, never the one in progress.
        /// </summary>
        public GameRuleSets DefaultRuleSets { get; }

        /// <summary>The rules new games of <paramref name="kind"/> start with (<see cref="DefaultRuleSets"/>; the stored object, not a copy).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a <see cref="GameKind"/>.</exception>
        public Rules DefaultRulesFor(GameKind kind) => DefaultRuleSets[kind];

        /// <summary>
        /// The rules the current game is played by (the board's <see cref="Board.GameRules"/>,
        /// also the clocks' limits), fixed when the game starts: a copy of its kind's
        /// <see cref="DefaultRulesFor"/> as they were then (endgames, openings and saved games
        /// are <see cref="GameKind.Traditional"/>), except for a loaded saved game, whose file's
        /// time control / rules are put over that copy (<see cref="SavedGame.RulesFor"/>), and a
        /// <see cref="Restart"/>, which keeps the restarted game's rules. A different object from
        /// every <see cref="DefaultRuleSets"/> entry (later settings changes do not reach it).
        /// </summary>
        public Rules Rules => Board.GameRules;

        /// <summary>
        /// How many leading moves of <see cref="Moves"/> cannot be undone: the opening line
        /// played by <see cref="StartOpening"/> (its length); 0 for every other game (a normal
        /// game undoes back to its start position, an endgame back to the puzzle's position).
        /// Kept when a saved game is loaded (<c>[PresetPlies]</c>).
        /// </summary>
        public int UndoFloor { get; private set; } = 0;

        // Local players' names, indexed Player1..Player3 (null or empty: unnamed).
        private readonly string[] _playerNames = new string[3];

        /// <summary>
        /// Sets the local players' names (設定 → 玩家一／二／三名稱; a network game would use the
        /// accounts instead). Null or empty leaves a player unnamed (see <see cref="NameOf"/>).
        /// </summary>
        public void SetPlayerNames(string player1, string player2, string player3)
        {
            _playerNames[0] = player1;
            _playerNames[1] = player2;
            _playerNames[2] = player3;
        }

        /// <summary>
        /// The name set for <paramref name="side"/> (<see cref="SetPlayerNames"/>), or null when
        /// it has none (unnamed, or not a player: the caller shows a default).
        /// </summary>
        public string NameOf(PlayerSide side)
        {
            int index = side switch
            {
                PlayerSide.Player1 => 0,
                PlayerSide.Player2 => 1,
                PlayerSide.Player3 => 2,
                _ => -1,
            };
            return index >= 0 && !string.IsNullOrEmpty(_playerNames[index]) ? _playerNames[index] : null;
        }

        /// <summary>
        /// Whether any move has been played in this game beyond its preset start (the
        /// <see cref="UndoFloor"/> moves of an opening line). False for a fresh game, endgame or
        /// opening; true once a player has moved (a loaded save counts its played moves).
        /// </summary>
        public bool HasPlayedMoves => _moves.Count > UndoFloor;

        /// <summary>How many moves one <see cref="Undo"/> takes back: a round, the last move of each side.</summary>
        public const int UndoRoundPlies = 2;

        /// <summary>
        /// Whether <see cref="Undo"/> can take a round back: there are at least
        /// <see cref="UndoRoundPlies"/> moves above <see cref="UndoFloor"/> (e.g. not with Black
        /// to move after Red's first move). Also true after the game has ended (undoing
        /// reopens it) and while paused.
        /// </summary>
        public bool CanUndo => _moves.Count - UndoFloor >= UndoRoundPlies;

        /// <summary>
        /// Raised once per move taken back (twice per <see cref="Undo"/>, newest move first),
        /// last for that move, after the board, move list, turn, check flag, game-over state,
        /// clocks and hanging pieces are all back to the position before the move, with the
        /// record that was taken back (no longer in <see cref="Moves"/>). The board change itself is raised before
        /// it: <see cref="PieceMoved"/> for the piece going back to its from-square and
        /// <see cref="PieceAdded"/> for a captured piece returning.
        /// </summary>
        public event Action<MoveRecord> MoveUndone;

        private bool _hasUnsavedChanges = false;

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
            get => _hasUnsavedChanges;
            private set
            {
                if (_hasUnsavedChanges != value)
                {
                    _hasUnsavedChanges = value;
                    UnsavedChangesChanged?.Invoke(_hasUnsavedChanges);
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
        /// for an opening the position before its preset line), with Player1's colour
        /// (<see cref="ColorOf"/>) to move: with <see cref="Moves"/> it is the whole game, the <c>[FEN]</c> of a saved
        /// game. Null off the Full board and after <see cref="ClearBoard"/>.
        /// </summary>
        public string InitialFen { get; private set; } = null;

        /// <summary>
        /// The file the current game was set up from (endgame puzzle, opening, saved game); null
        /// for any other game. Kept for <see cref="Restart"/> (a loaded saved game has neither
        /// <see cref="CurrentEndgame"/> nor <see cref="CurrentOpening"/>, but is loaded again from
        /// this record on restart).
        /// </summary>
        private PgnGameFile _startSource = null;

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

        /// <summary>A game with the default <see cref="Rules"/> for every kind.</summary>
        public GameManager() : this((GameRuleSets)null) { }

        /// <summary>
        /// Every kind's new games start with a copy of <paramref name="rules"/> (null: the
        /// default <see cref="Rules"/>); see <see cref="GameManager(GameRuleSets)"/>.
        /// </summary>
        public GameManager(Rules rules) : this(new GameRuleSets(rules)) { }

        /// <summary>
        /// New games start with <paramref name="ruleSets"/>' rules for their kind (null: the
        /// default <see cref="Rules"/> for every kind): the board's rule toggles and the players'
        /// clocks (total / step time, increment, step timer on/off, count mode) all come from
        /// them. The launchers pass the rule sets built from the player settings
        /// (docs/SETTINGS.md). The first game is a <see cref="GameKind.Traditional"/> one.
        /// </summary>
        public GameManager(GameRuleSets ruleSets)
        {
            _movesView = _moves.AsReadOnly();
            DefaultRuleSets = ruleSets ?? new GameRuleSets();
            var rules = DefaultRulesFor(GameKind.Traditional);

            // Initialize the board, with this first game's own copy of the rules.
            Board = new Board(BoardType.Full, rules.Clone());
            Board.Initialize(BoardConfigLoader.Load());
            CurrentTurn = PlayerSide.Player1;
            InitialFen = FormatInitialFen(PieceColor.Red);
            _selectedPiece = null;
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
            // Load default pieces (the standard start: Red moves first, so Red is Player1)
            SetUpPosition(BoardConfigLoader.Load(), null);
        }

        /// <summary>
        /// Starts a new HalfCenter game (台灣暗棋半盤, 8×4): the 32 pieces shuffled
        /// (<see cref="BoardConfigLoader.CreateShuffledHalfCenter"/>) and owned by nobody, face
        /// down when <see cref="Rules.IsHiddenChess"/>, otherwise face up (明棋半盤). Player1 acts
        /// first — its first flip (or, face up, its first move) decides who plays which colour
        /// (see <see cref="ColorOf"/>).
        /// </summary>
        /// <param name="hiddenChess">
        /// 暗棋半盤 (true: <see cref="GameKind.DarkHalf"/>, face down) or 明棋半盤 (false:
        /// <see cref="GameKind.OpenHalf"/>, face up); the new-game menu picks it. The game plays
        /// by a copy of that kind's <see cref="DefaultRulesFor"/>, its <see cref="Rules.IsHiddenChess"/>
        /// set to match, so <see cref="Rules"/> tells a restart which variant to deal again.
        /// </param>
        /// <param name="seed">
        /// The shuffle's seed; null (the game's choice) seeds it from the clock, so every new game
        /// is a different layout. A fixed seed always gives the same layout (tests, replaying a
        /// layout from the log). <see cref="GlobalRandom"/> is not used: its fixed seed would
        /// deal the same layout on every launch.
        /// </param>
        public void StartHalfCenter(bool hiddenChess, int? seed = null)
        {
            var rules = DefaultRulesFor(hiddenChess ? GameKind.DarkHalf : GameKind.OpenHalf).Clone();
            rules.IsHiddenChess = hiddenChess;
            SetUpHalfCenter(rules, seed);
        }

        /// <summary><see cref="StartHalfCenter"/> played by <paramref name="rules"/> (this game's own copy; its <see cref="Rules.IsHiddenChess"/> picks the variant).</summary>
        private void SetUpHalfCenter(Rules rules, int? seed)
        {
            int shuffleSeed = seed ?? Environment.TickCount;
            var random = new RandomTable(HalfCenterShuffleTableSize, shuffleSeed);
            var pieces = BoardConfigLoader.CreateShuffledHalfCenter(random, rules.IsHiddenChess);
            SetUpPosition(pieces, null, BoardType.HalfCenter, rules);
            AppLogger.Log($"(DarkChess) Started a HalfCenter game, hidden: {rules.IsHiddenChess}, seed: {shuffleSeed}", LogLevel.DEBUG);
            Logger?.AddMessage(rules.IsHiddenChess ? "(DarkChess) 新局：暗棋半盤" : "(DarkChess) 新局：明棋半盤");
        }

        /// <summary>
        /// Size of the <see cref="RandomTable"/> a HalfCenter shuffle draws from: at least the
        /// 31 draws one Fisher–Yates pass over the 32 squares takes, so no value repeats.
        /// </summary>
        private const int HalfCenterShuffleTableSize = 64;

        /// <summary>
        /// Starts a game from <paramref name="customInitialPieces"/> on a board of
        /// <paramref name="boardType"/> (a new <see cref="Board"/> when the type changes);
        /// Player1 moves first. On the Full board the pieces' colours decide their owners: the
        /// <paramref name="firstColor"/> pieces are Player1's, the other colour's Player2's (the
        /// pieces' own <see cref="PieceInfo.Side"/> is ignored). On any other board the pieces
        /// are placed with their own sides (dark chess: nobody's until the first flip).
        /// </summary>
        /// <param name="customInitialPieces">The pieces to place; their squares must be on a <paramref name="boardType"/> board.</param>
        /// <param name="firstColor">Full board: the colour that moves first (Player1's colour; Red
        /// or Black). Ignored on the other boards.</param>
        /// <param name="boardType">The board to play on; Full by default.</param>
        /// <param name="localSide">The side the player plays (<see cref="LocalSide"/>, chosen when
        /// the position is set up); null for the default, Player1.</param>
        /// <exception cref="ArgumentException">Full board: <paramref name="firstColor"/> is not
        /// Red/Black, or a piece is neither Red nor Black.</exception>
        public void LoadCustomBoard(List<PieceInfo> customInitialPieces, PieceColor firstColor = PieceColor.Red, BoardType boardType = BoardType.Full,
            PlayerSide? localSide = null)
        {
            SetUpPosition(customInitialPieces, null, boardType, localSide: localSide, firstColor: firstColor);
            if (boardType == BoardType.HalfCenter)
                _customHalfCenterStart = customInitialPieces.Select(p => p.Clone()).ToList();
        }

        // The pieces a custom HalfCenter game started from (LoadCustomBoard), so Restart puts
        // them back instead of shuffling; null for a shuffled game and every other setup.
        private List<PieceInfo> _customHalfCenterStart;

        /// <summary>
        /// Starts a game from <paramref name="puzzle"/>'s position (its FEN); Player1 moves first
        /// and plays the FEN's colour to move (Black in a black-first puzzle) - and keeps the puzzle as
        /// <see cref="CurrentEndgame"/>. The puzzle's solution is not played.
        /// </summary>
        /// <exception cref="FormatException">The puzzle's FEN is not valid (puzzles from
        /// <see cref="EndgameLoader"/> have already been checked).</exception>
        public void StartEndgame(EndgamePuzzle puzzle) => SetUpEndgame(puzzle, null);

        /// <summary><see cref="StartEndgame"/> played by <paramref name="rules"/> (this game's own copy); null for a copy of the <see cref="GameKind.Traditional"/> defaults.</summary>
        private void SetUpEndgame(EndgamePuzzle puzzle, Rules rules)
        {
            ArgumentNullException.ThrowIfNull(puzzle);
            var (pieces, firstColor) = XiangqiFen.Parse(puzzle.Fen);
            SetUpPosition(pieces, puzzle, BoardType.Full, rules, firstColor: firstColor);
            AppLogger.Log($"(Endgame) Started {puzzle.FileName}: {puzzle.Title}, {firstColor} to move", LogLevel.DEBUG);
            Logger?.AddMessage($"(Endgame) {puzzle.Title} ({puzzle.Goal})");
        }

        /// <summary>
        /// Restarts the current game in its current mode from its starting position, by the
        /// same rules, with both clocks reset and the move list empty (an opening's preset line
        /// and a saved game's preset plies are replayed again; they are the start):
        /// a standard game goes back to the standard start (or to the custom start position it
        /// began from); a HalfCenter game becomes a new shuffled game of the same variant
        /// (暗棋 or 明棋), or, set up with <see cref="LoadCustomBoard"/>, starts again from its
        /// custom layout; an endgame challenge or an opening practice starts the same puzzle /
        /// line again; a loaded saved game is loaded again (<see cref="LoadSavedGame"/>): the
        /// position, moves, clocks and ending exactly as when it was loaded (an ended save comes
        /// back ended), from the <see cref="SavedGame"/> read at load time - the file is not read
        /// again, so a file changed or deleted since does not matter. After
        /// <see cref="ClearBoard"/> (no start position known) the standard start is used.
        /// Raises <see cref="BoardReset"/> like any new game.
        /// </summary>
        /// <exception cref="NotSupportedException">The board type cannot be played yet (HalfCross).</exception>
        public void Restart()
        {
            // The restarted game keeps its own rules (a fresh copy of them), not the current
            // DefaultRuleSets: rules are fixed per game.
            var rules = Rules.Clone();

            if (Board.Type == BoardType.HalfCenter)
            {
                // A custom layout starts again as it was set up (author decision 2026-10-02); a
                // shuffled game is shuffled again.
                if (_customHalfCenterStart != null)
                {
                    var start = _customHalfCenterStart;
                    SetUpPosition(start.Select(p => p.Clone()).ToList(), null, BoardType.HalfCenter, rules, LocalSide);
                    _customHalfCenterStart = start;
                }
                else
                {
                    SetUpHalfCenter(rules, null);
                }
                return;
            }
            if (Board.Type != BoardType.Full)
                throw new NotSupportedException($"Cannot restart a {Board.Type} game yet");

            switch (_startSource)
            {
                case EndgamePuzzle puzzle:
                    SetUpEndgame(puzzle, rules);
                    break;
                case OpeningLine opening:
                    SetUpOpening(opening, rules);
                    break;
                case SavedGame saved:
                    RestartSavedGame(saved, rules);
                    break;
                default:
                    var (pieces, firstColor) = InitialFen == null
                        ? (BoardConfigLoader.Load(), PieceColor.Red)
                        : XiangqiFen.Parse(InitialFen);
                    // A custom start keeps the side the player chose for it.
                    SetUpPosition(pieces, null, BoardType.Full, rules, LocalSide, firstColor);
                    break;
            }
            AppLogger.Log($"(Restart) Restarted the {Mode} game", LogLevel.DEBUG);
        }

        /// <summary>
        /// <see cref="Restart"/> of a loaded saved game (author decision 2026-10-02: restarting it
        /// = loading it again): the same <see cref="SavedGame"/> loaded again by
        /// <paramref name="rules"/> (the loaded game's), so the position, moves, undo floor,
        /// clocks and ending are exactly as when it was loaded. The record read at load time is
        /// used, not the file: it already is the file as loaded, and a file edited, overwritten or
        /// deleted since cannot change or break the restart.
        /// </summary>
        private void RestartSavedGame(SavedGame saved, Rules rules) => SetUpSavedGame(saved, rules, "(Restart)");

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
        public int StartOpening(OpeningLine opening) => SetUpOpening(opening, null);

        /// <summary><see cref="StartOpening"/> played by <paramref name="rules"/> (this game's own copy); null for a copy of the <see cref="GameKind.Traditional"/> defaults.</summary>
        private int SetUpOpening(OpeningLine opening, Rules rules)
        {
            ArgumentNullException.ThrowIfNull(opening);
            var (pieces, firstColor) = XiangqiFen.Parse(opening.Fen);
            SetUpPosition(pieces, opening, BoardType.Full, rules, firstColor: firstColor);
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
        /// counter; on the Full board owned by colour, <paramref name="firstColor"/> = Player1),
        /// clears the selection, gives the move to Player1 (always the first mover), resets both
        /// clocks and starts Player1's step, then informs the UI (<see cref="BoardReset"/>,
        /// <see cref="PieceAdded"/> per piece) and recomputes the hanging pieces.
        /// </summary>
        /// <param name="source">The file the game starts from (an endgame puzzle, an opening, a
        /// saved game), or null; sets <see cref="Mode"/>, <see cref="OriginId"/> and
        /// <see cref="OriginTitle"/>, and the <see cref="Rules"/> (a saved game's own over a copy
        /// of the <see cref="GameKind.Traditional"/> defaults, otherwise a copy of the defaults of
        /// <paramref name="boardType"/>'s kind, <see cref="GameRuleSets.KindFor"/>).</param>
        /// <param name="boardType">The board the game is played on: the current <see cref="Board"/>
        /// is replaced by a new one when its type differs. Every FEN-based setup (endgames,
        /// openings, saved games, the default position) is Full.</param>
        /// <param name="rules">This game's rules when they differ from what
        /// <paramref name="source"/> decides (e.g. <see cref="StartHalfCenter"/>'s
        /// 暗棋/明棋 choice, or the restarted game's rules for <see cref="Restart"/>); null for
        /// that default. Must be this game's own object (a copy), never a <see cref="DefaultRuleSets"/>
        /// entry itself.</param>
        /// <param name="localSide">The game's <see cref="LocalSide"/>; null for what
        /// <paramref name="source"/> decides (see <see cref="LocalSide"/>).</param>
        /// <param name="firstColor">Full board: the colour that moves first, played by Player1
        /// (<see cref="ColorOf"/>); the pieces' owners are assigned from it
        /// (<see cref="PieceColors.AssignOwners"/>). Ignored on the other boards (their pieces
        /// keep their own sides; Player1's colour there is decided as <see cref="ColorOf"/> says).</param>
        /// <exception cref="ArgumentException">Full board: <paramref name="firstColor"/> is not
        /// Red/Black, or a piece is neither Red nor Black.</exception>
        private void SetUpPosition(List<PieceInfo> pieces, PgnGameFile source, BoardType boardType = BoardType.Full,
            Rules rules = null, PlayerSide? localSide = null, PieceColor firstColor = PieceColor.Red)
        {
            _customHalfCenterStart = null;

            // Players are numbered by turn order: on the Full board the first colour is Player1's.
            if (boardType == BoardType.Full)
                pieces = PieceColors.AssignOwners(pieces, firstColor);

            // A different board type needs a differently-sized grid: a new board (its rules
            // are set right below).
            if (Board.Type != boardType)
                Board = new Board(boardType, Board.GameRules);

            // A saved game is played by the rules it was saved with (over the Traditional
            // defaults); every other game by a copy of its kind's defaults as they are now (or
            // the rules its setup passed). Always this game's own object, so a later settings
            // change (to DefaultRuleSets) does not reach it.
            ApplyRules(rules ?? (source is SavedGame savedGame
                ? savedGame.RulesFor(DefaultRulesFor(GameKind.Traditional))
                : DefaultRulesFor(GameRuleSets.KindFor(boardType)).Clone()));

            // Reset board:
            // (A) Clear pieces
            // (B) Reset turn
            // (C) Recreate pieces
            Board.Initialize(pieces);

            // Reset selected piece
            _selectedPiece = null;
            _startSource = source;
            CurrentEndgame = source as EndgamePuzzle;
            CurrentOpening = source as OpeningLine;
            (Mode, OriginId, OriginTitle) = source switch
            {
                EndgamePuzzle puzzle => (GameMode.Endgame, puzzle.Id, puzzle.Title),
                OpeningLine opening => (GameMode.Opening, opening.Id, opening.Title),
                SavedGame saved => (saved.Mode, saved.OriginId, saved.OriginTitle),
                _ => (GameMode.Normal, (string)null, (string)null),
            };
            // Reset side: Player1 always moves first.
            CurrentTurn = PlayerSide.Player1;
            _player1Color = boardType == BoardType.Full ? firstColor : PieceColor.Red;
            LocalSide = localSide ?? source switch
            {
                OpeningLine opening => opening.PlayerSide,
                SavedGame saved => saved.PlayerSide,
                // A new game, an endgame puzzle (solved by the side to move) and a half board.
                _ => PlayerSide.Player1,
            };
            InitialFen = FormatInitialFen(_player1Color);
            ResetTimers(startFirstTurn: true);
            // A custom position may start with the side to move already in check.
            IsInCheck = Board.UsesCheckRules && Board.IsSideInCheck(PlayerSide.Player1);

            // Inform UI
            BoardReset?.Invoke();

            // Inform pieces added
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);

            UpdateHangingPieces();
        }

        /// <summary>
        /// Makes <paramref name="rules"/> the current game's <see cref="Rules"/>: the board's
        /// move rules and both clocks' limits, increment, step timer switch and count mode.
        /// Elapsed times are not touched.
        /// </summary>
        private void ApplyRules(Rules rules)
        {
            Board.SetRules(rules);
            foreach (var timer in new[] { Player1.Timer, Player2.Timer })
            {
                timer.TotalTimeLimit = rules.TotalTimeLimit;
                timer.StepTimeLimit = rules.StepTimeLimit;
                timer.IncrementPerMove = rules.IncrementPerMove;
                timer.EnableStepTimer = rules.EnableStepTimer;
                timer.Mode = rules.TimerMode;
                timer.ContinueAfterTimeUp = !rules.EndGameWhenTimesUp;
            }
        }

        /// <summary>The FEN of the board's current pieces with <paramref name="colorToMove"/>; null off the Full board or when the pieces cannot be written as FEN.</summary>
        private string FormatInitialFen(PieceColor colorToMove)
        {
            if (Board.Type != BoardType.Full)
                return null;
            try
            {
                return XiangqiFen.Format(Board, colorToMove);
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
        /// result, time control, both clocks as they are now and the <see cref="Rules"/> in
        /// effect. Does not change <see cref="HasUnsavedChanges"/>.
        /// </summary>
        /// <param name="date">The <c>[Date]</c>; null for now.</param>
        /// <exception cref="InvalidOperationException">Not <see cref="CanSave"/>.</exception>
        public string ExportPgn(string redName, string blackName, DateTime? date = null) =>
            SavedGamePgn.Write(this, redName, blackName, date ?? DateTime.Now);

        /// <summary>
        /// Writes the current game (<see cref="ExportPgn"/>) to <paramref name="filePath"/>
        /// (UTF-8, its folder created when missing; an existing file is overwritten) and clears
        /// <see cref="HasUnsavedChanges"/>. Where and under which name is the caller's choice
        /// (the folder and file name rules are in <c>SystemSettings</c> / <c>GameSaveFiles</c>).
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
        /// same way again (<c>[Result]</c> + <c>[Termination]</c>). Rules: the file's time
        /// control and rules over the <see cref="GameKind.Traditional"/> defaults (<see cref="SavedGame.RulesFor"/>),
        /// for this game only; the next new game is back to the defaults. Clocks: both continue
        /// from the saved elapsed times (the side to move's step clock running from its saved
        /// step time; stopped when the game is over); a file without clock tags starts both
        /// fresh. Undoing a replayed move keeps both elapsed totals as they are (the file has
        /// no per-move clock history) and restarts the mover's step. Ends clean
        /// (<see cref="HasUnsavedChanges"/> false).
        /// </summary>
        /// <returns>The number of moves replayed: all of them unless one is not legal (files
        /// from <see cref="SavedGameLoader"/> have already been checked), where replay stops.</returns>
        /// <exception cref="FormatException">The saved game's FEN is not valid.</exception>
        public int LoadSavedGame(SavedGame saved) => SetUpSavedGame(saved, null, "(Load)");

        /// <summary>
        /// <see cref="LoadSavedGame"/> played by <paramref name="rules"/> (this game's own copy;
        /// null for <see cref="SavedGame.RulesFor"/> over the current <see cref="GameKind.Traditional"/> defaults),
        /// its game-log line headed <paramref name="logTag"/>. <see cref="Restart"/> of a loaded
        /// saved game uses it with the loaded game's rules, so it comes back exactly as loaded.
        /// </summary>
        private int SetUpSavedGame(SavedGame saved, Rules rules, string logTag)
        {
            ArgumentNullException.ThrowIfNull(saved);
            var (pieces, firstColor) = XiangqiFen.Parse(saved.Fen);
            SetUpPosition(pieces, saved, BoardType.Full, rules, firstColor: firstColor);
            Logger?.AddMessage($"{logTag} {saved.Title}");

            IsReplaying = true;
            try
            {
                return ReplaySavedGame(saved);
            }
            finally
            {
                IsReplaying = false;
            }
        }

        /// <summary>The replay and ending of <see cref="LoadSavedGame"/>, after the start position is set up.</summary>
        private int ReplaySavedGame(SavedGame saved)
        {
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

            // An ending that is not a move (resignation, time-up) is not replayed by the _moves.
            if (!IsGameOver && played == saved.Moves.Count && saved.Winner != PlayerSide.None &&
                saved.Termination is GameOverReason.Resign or GameOverReason.TimeUp)
            {
                EndGame(saved.Winner, OpponentOf(saved.Winner), saved.Termination.Value);
            }

            // The clocks recorded while replaying are not the game's: unknown for undo.
            for (int i = 0; i < _clocksBeforeMove.Count; i++)
                _clocksBeforeMove[i] = null;

            if (saved.Player1Clock != null || saved.Player2Clock != null)
                RestoreSavedClocks(saved.Player1Clock ?? default, saved.Player2Clock ?? default);
            else if (!IsGameOver)
                RestartClocks();
            HasUnsavedChanges = false;
            AppLogger.Log($"(Load) Loaded {saved.FileName}: {played} move(s) replayed, {CurrentTurn} to move, over: {IsGameOver}", LogLevel.DEBUG);
            return played;
        }

        /// <summary>
        /// Both clocks set to a saved game's elapsed times: the side to move's step runs on from
        /// its saved step time; an ended game's clocks stay stopped.
        /// </summary>
        private void RestoreSavedClocks(ClockState player1, ClockState player2)
        {
            bool running = !IsGameOver;
            Player1.Timer.RestoreClockState(player1, active: running && CurrentTurn == PlayerSide.Player1, paused: false);
            Player2.Timer.RestoreClockState(player2, active: running && CurrentTurn == PlayerSide.Player2, paused: false);
            if (!running)
            {
                Player1.Timer.End();
                Player2.Timer.End();
            }
        }

        public void ClearBoard()
        {
            // Clear board:
            // (A) Clear pieces
            // (B) Reset turn
            Board.Clear();

            // Reset selected piece
            _selectedPiece = null;
            _startSource = null;
            CurrentEndgame = null;
            CurrentOpening = null;
            Mode = GameMode.Normal;
            OriginId = null;
            OriginTitle = null;
            InitialFen = null;
            ApplyRules(DefaultRulesFor(GameKind.Traditional).Clone());
            // Reset side
            CurrentTurn = PlayerSide.Player1;
            _player1Color = PieceColor.Red;
            LocalSide = PlayerSide.Player1;
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
        /// The colour <paramref name="side"/> plays (players are numbered by turn order, so this
        /// is a per-game attribute). Off the dark-chess board it is fixed when the game is set
        /// up: on the Full board Player1 plays the colour that moves first in the start position
        /// (FEN <c>w</c> red, <c>b</c> black) and Player2 the other one; on any other such board
        /// Player1 red, Player2 black. On a <see cref="Board.UsesDarkChessRules"/> board nobody
        /// owns a colour until the first action decides it — the first flip
        /// (<see cref="MoveKind.Flip"/>) gives the flipping player the flipped piece's colour;
        /// in 明棋半盤 (all face up, <see cref="Rules.IsHiddenChess"/> off) the first move gives
        /// the mover the moved piece's colour; the other player gets the other one — so this is
        /// <see cref="PieceColor.None"/> before that (and again after that action is undone).
        /// For the turn display and the log lines.
        /// </summary>
        /// <param name="side">Player1 or Player2 (any other side has no colour: None).</param>
        /// <returns>Red, Black, or None while undecided.</returns>
        public PieceColor ColorOf(PlayerSide side)
        {
            if (side != PlayerSide.Player1 && side != PlayerSide.Player2)
                return PieceColor.None;
            if (!Board.UsesDarkChessRules)
                return side == PlayerSide.Player1 ? _player1Color : OppositeColor(_player1Color);

            // Decided once any piece has an owner: every piece got one at the first flip.
            var opponent = OpponentOf(side);
            foreach (var p in Board.GetAllPieces())
            {
                if (p.Side == side)
                    return p.Color;
                if (p.Side == opponent)
                    return OppositeColor(p.Color);
            }
            return PieceColor.None;
        }

        private static PieceColor OppositeColor(PieceColor color) => color switch
        {
            PieceColor.Red => PieceColor.Black,
            PieceColor.Black => PieceColor.Red,
            _ => PieceColor.None,
        };

        /// <summary>
        /// Flips the face-down piece at (x, y) face up as the side to move's whole turn (翻子),
        /// if the board plays by dark-chess rules (<see cref="Board.UsesDarkChessRules"/>).
        /// Same effect as clicking it through <see cref="HandleClick"/> with nothing selected.
        /// </summary>
        /// <returns>Whether the piece was flipped (false: paused, game over, not a dark-chess
        /// board, or no face-down piece there).</returns>
        public bool TryFlip(int x, int y)
        {
            if (IsPaused || IsGameOver || !Board.UsesDarkChessRules)
                return false;

            var piece = Board.GetPiece(x, y);
            if (piece == null || piece.CurrentInfo.IsFaceUp)
                return false;

            ExecuteFlip(piece);
            return true;
        }

        /// <summary>
        /// Whether <paramref name="piece"/> may be selected (and moved) by the side to move: it
        /// is that side's, and on a <see cref="Board.UsesDarkChessRules"/> board face up — a
        /// face-down piece is only ever flipped, so neither its side nor its type may decide
        /// anything (揭棋's face-down pieces on the Full board do move, as their square's type).
        /// A face-up piece nobody owns yet (明棋半盤 before the first move) is anyone's: moving it
        /// decides the factions.
        /// </summary>
        private bool IsSelectable(Piece piece) =>
            piece != null
            && (piece.Side == CurrentTurn || (Board.UsesDarkChessRules && piece.Side == PlayerSide.None))
            && (!Board.UsesDarkChessRules || piece.CurrentInfo.IsFaceUp);

        /// <summary>
        /// A piece for the click log lines: its type, or just "face-down" for a face-down piece
        /// on a <see cref="Board.UsesDarkChessRules"/> board (its type is hidden information
        /// and the game log is visible to both players).
        /// </summary>
        private string DescribeForLog(Piece piece)
        {
            if (piece == null)
                return "null";
            if (Board.UsesDarkChessRules && !piece.CurrentInfo.IsFaceUp)
                return "face-down piece";
            return piece.GetType().Name;
        }

        /// <summary>
        /// Moves the piece at (fromX, fromY) to (toX, toY) if it belongs to the side to
        /// move (and, on the dark-chess board, is face up) and the move is legal. Same effect as selecting and clicking through
        /// <see cref="HandleClick"/> (capture, log, events, selection cleared, turn switch).
        /// </summary>
        public bool TryMove(int fromX, int fromY, int toX, int toY)
        {
            if (IsPaused || IsGameOver)
                return false;

            var piece = Board.GetPiece(fromX, fromY);
            if (!IsSelectable(piece))
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
                $"Current turn: {CurrentTurn}, holding: {(_selectedPiece == null ? "null" : _selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {DescribeForLog(clickedPiece)}", LogLevel.DEBUG);
            Logger?.AddMessage($"Current turn: {CurrentTurn}, holding: {(_selectedPiece == null ? "null" : _selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {DescribeForLog(clickedPiece)}");

            // No selected piece: flip a face-down piece (dark chess), or try to select one
            if (_selectedPiece == null)
            {
                if (clickedPiece != null && Board.UsesDarkChessRules && !clickedPiece.CurrentInfo.IsFaceUp)
                {
                    ExecuteFlip(clickedPiece);
                    return;
                }
                if (IsSelectable(clickedPiece))
                {
                    _selectedPiece = clickedPiece;
                    AppLogger.Log($"(Action) Selected {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Selected {clickedPiece.Type} at ({x},{y})");
                    PieceSelected?.Invoke(_selectedPiece);
                }
                return;
            }

            // Has selected piece, but 2nd selection is another selectable (own, face-up) piece.
            // Before the factions are decided (明棋半盤) an other-coloured piece is a capture
            // target, not a piece to switch to.
            if (IsSelectable(clickedPiece) && clickedPiece.IsSameFaction(_selectedPiece))
            {
                if (clickedPiece == _selectedPiece)
                {
                    AppLogger.Log($"(Action) Un-selected {_selectedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Un-selected {_selectedPiece.Type} at ({x},{y})");
                    PieceUnselected?.Invoke(_selectedPiece);
                    _selectedPiece = null;
                }
                else
                {
                    AppLogger.Log($"(Action) Switched to {clickedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Switched to {clickedPiece.Type} at ({x},{y})");
                    PieceUnselected?.Invoke(_selectedPiece);
                    _selectedPiece = clickedPiece;
                    PieceSelected?.Invoke(_selectedPiece);
                }
                return;
            }

            // Has selected piece, try to move to 2nd selection
            if (_selectedPiece.CanMoveTo(Board, x, y))
            {
                ExecuteMove(_selectedPiece, x, y);
            }
            else
            {
                // If 2nd selection point is empty, unselected
                if (clickedPiece == null)
                {
                    AppLogger.Log($"(Action) Un-selected {_selectedPiece.Type} at ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Un-selected {_selectedPiece.Type} at ({x},{y})");
                }
                // Invalid catch
                else
                {
                    AppLogger.Log($"(Action) Invalid move to ({x},{y})", LogLevel.DEBUG);
                    Logger?.AddMessage($"(Action) Invalid move to ({x},{y})");
                }
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }
        }

        /// <summary>
        /// Applies an already-validated move: advances the board's turn counter (so the
        /// pieces' history snapshots carry the move number), captures whatever stands on
        /// the destination, moves the piece, clears the selection and switches the turn.
        /// Shared by <see cref="HandleClick"/> and <see cref="TryMove"/>. On the dark-chess
        /// board, moving a piece nobody owns yet (明棋半盤's first move) first gives the side to
        /// move that piece's colour and the other player the other one
        /// (<see cref="Board.AssignFactions"/>); the move is then undone like a dark-chess
        /// action, which takes the decision back too.
        /// </summary>
        private void ExecuteMove(Piece piece, int toX, int toY)
        {
            // 暗吃: a move onto a face-down piece reveals it first (see ExecuteHiddenCapture).
            var hiddenTarget = Board.GetPiece(toX, toY);
            if (Board.UsesDarkChessRules && hiddenTarget != null && !hiddenTarget.CurrentInfo.IsFaceUp)
            {
                ExecuteHiddenCapture(piece, hiddenTarget);
                return;
            }
            // 自殺: a move onto a stronger face-up enemy piece kills the mover (see ExecuteSuicide).
            if (Board.UsesDarkChessRules && piece.IsSuicideMove(Board, toX, toY))
            {
                ExecuteSuicide(piece, toX, toY);
                return;
            }

            int fromX = piece.X;
            int fromY = piece.Y;
            var mover = CurrentTurn;
            bool decidesFactions = Board.UsesDarkChessRules && piece.Side == PlayerSide.None;
            // Every piece's ownership changes with the decision: undo it piece by piece.
            var historyBefore = decidesFactions ? SnapshotHistoryCounts() : null;

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
            int ply = _moves.Count + 1;
            LastMove = new MoveRecord(piece.CurrentInfo.Clone(), fromX, fromY, toX, toY, targetPiece?.CurrentInfo.Clone(),
                ply, MoveNumberOf(ply), givesCheck, notation, iccs, side: mover);
            _moves.Add(LastMove);
            _capturedPieces.Add(targetPiece);
            _clocksBeforeMove.Add(clocks);
            _stateChanges.Add(null);
            HasUnsavedChanges = true;

            if (decidesFactions)
                Board.AssignFactions(piece.Color, mover);

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
            if (decidesFactions)
                _stateChanges[_stateChanges.Count - 1] = ChangesSince(historyBefore);

            // raise moved event AFTER board updated
            PieceMoved?.Invoke(piece, toX, toY);

            // Readable move-list line, in addition to the debug lines above.
            string line = LastMove.Notation != null ? FormatMoveLine(LastMove)
                : Board.UsesDarkChessRules ? FormatDarkChessLine(LastMove)
                : null;
            if (line != null)
            {
                AppLogger.Log(line, LogLevel.DEBUG);
                Logger?.AddMessage(line);
            }
            if (decidesFactions)
                LogFactions();
            MoveRecorded?.Invoke(LastMove);

            // unselect and notify
            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }

            // Board hints for the new position (also when this move ends the game).
            UpdateHangingPieces();

            // Standard xiangqi: the side about to move is evaluated right away. With no
            // legal move it loses on the spot — checkmate if in check, otherwise stalemate
            // (困斃, which also covers "every remaining move would face the Generals").
            // The turn is not handed over, so the loser's clock never starts.
            if (Board.UsesCheckRules)
            {
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

            if (Board.UsesDarkChessRules && EndDarkChessGameIfOver(mover))
                return;
            SwitchTurn();
        }

        /// <summary>
        /// The move number (第N回合) of the <paramref name="ply"/>-th move: a move and the reply
        /// share a number. Black-first games (endgames; Player1 plays Black) number like PGN:
        /// Black's first move is 1, Red's reply 2.
        /// </summary>
        private int MoveNumberOf(int ply) => (ply - 1 + (_player1Color == PieceColor.Black ? 1 : 0)) / 2 + 1;

        /// <summary>
        /// Every piece's history length now, so <see cref="ChangesSince"/> can tell which pieces
        /// an action changed and by how many snapshots.
        /// </summary>
        private Dictionary<Piece, int> SnapshotHistoryCounts()
        {
            var counts = new Dictionary<Piece, int>();
            foreach (var p in Board.GetAllPieces())
                counts[p] = p.History.Count;
            return counts;
        }

        /// <summary>The pieces whose history grew since <paramref name="before"/>, with how much (see <see cref="Board.RevertStates"/>).</summary>
        private static List<(Piece piece, int snapshots)> ChangesSince(Dictionary<Piece, int> before)
        {
            var changes = new List<(Piece piece, int snapshots)>();
            foreach (var (piece, count) in before)
            {
                if (piece.History.Count > count)
                    changes.Add((piece, piece.History.Count - count));
            }
            return changes;
        }

        /// <summary>
        /// Applies a flip (翻子, dark chess) as the side to move's turn: advances the board's
        /// turn counter, turns <paramref name="piece"/> face up and — if this is the game's first
        /// flip (nobody owns a colour yet) — gives the side to move the flipped piece's colour
        /// and the other player the other one (<see cref="Board.AssignFactions"/>). Then records
        /// it like a move (<see cref="MoveKind.Flip"/>, for undo with both clocks), logs it,
        /// clears the selection, recomputes the hanging pieces and switches the turn.
        /// </summary>
        private void ExecuteFlip(Piece piece)
        {
            var mover = CurrentTurn;
            var clocks = (Player1.Timer.GetClockState(), Player2.Timer.GetClockState());
            var before = SnapshotHistoryCounts();
            var pieceBefore = piece.CurrentInfo.Clone();
            bool decidesFactions = piece.Side == PlayerSide.None;

            Board.AdvanceTurn();
            Board.FlipPiece(piece.X, piece.Y);
            if (decidesFactions)
                Board.AssignFactions(piece.Color, mover);

            AppLogger.Log($"(Action) Flipped {piece.Color} {piece.Type} at ({piece.X},{piece.Y})", LogLevel.DEBUG);
            RecordDarkChessAction(pieceBefore, piece.X, piece.Y, piece.X, piece.Y, MoveKind.Flip, mover,
                piece.CurrentInfo.Clone(), null, clocks, before);
            if (decidesFactions)
                LogFactions();
            EndDarkChessAction();
        }

        /// <summary>Writes the faction decision's log line (which player plays which colour).</summary>
        private void LogFactions()
        {
            string factions = $"(Faction) {PlayerSide.Player1} 執{ColorName(ColorOf(PlayerSide.Player1))}，{PlayerSide.Player2} 執{ColorName(ColorOf(PlayerSide.Player2))}";
            AppLogger.Log(factions, LogLevel.DEBUG);
            Logger?.AddMessage(factions);
        }

        /// <summary>
        /// Applies an already-validated move of <paramref name="piece"/> onto the face-down
        /// <paramref name="target"/> (暗吃, <see cref="Rules.CanCaptureHiddenPiece"/>) as the side
        /// to move's turn: the target is turned face up, then
        /// <list type="bullet">
        /// <item>the mover's own piece: the mover stays on its from-square
        /// (<see cref="MoveKind.HiddenOwnPiece"/>);</item>
        /// <item>an enemy piece the mover may capture by the normal rules (re-checked now that
        /// it is face up — rank order and the Soldier/General pair; a Cannon's jump capture
        /// ignores rank): a normal capture (<see cref="MoveKind.HiddenCapture"/>);</item>
        /// <item>any other enemy piece (a stronger one, or a Soldier when the mover is a General):
        /// <see cref="Rules.IsCaptureHiddenPieceStrongerSuicide"/> on, the mover dies
        /// (<see cref="MoveKind.HiddenStrongerSuicide"/>); off, it returns to its from-square
        /// (<see cref="MoveKind.HiddenStrongerReturn"/>). The target stays either way.</item>
        /// </list>
        /// Every outcome uses the turn and is recorded, logged and undone like a move.
        /// </summary>
        private void ExecuteHiddenCapture(Piece piece, Piece target)
        {
            var mover = CurrentTurn;
            int fromX = piece.X;
            int fromY = piece.Y;
            int toX = target.X;
            int toY = target.Y;
            var clocks = (Player1.Timer.GetClockState(), Player2.Timer.GetClockState());
            var before = SnapshotHistoryCounts();
            var pieceBefore = piece.CurrentInfo.Clone();

            Board.AdvanceTurn();
            Board.FlipPiece(toX, toY);
            var revealed = target.CurrentInfo.Clone();

            MoveKind kind;
            Piece captured = null;
            if (target.Side == piece.Side)
            {
                kind = MoveKind.HiddenOwnPiece;
            }
            // With 自殺 on, a move onto a stronger enemy is legal too, so it is checked apart.
            else if (piece.IsPseudoLegalMove(Board, toX, toY) && !piece.IsSuicideMove(Board, toX, toY))
            {
                kind = MoveKind.HiddenCapture;
                captured = target;
                Board.RemovePiece(toX, toY);
                Board.MovePiece(fromX, fromY, toX, toY);
            }
            else if (Board.GameRules.IsCaptureHiddenPieceStrongerSuicide)
            {
                kind = MoveKind.HiddenStrongerSuicide;
                Board.RemovePiece(fromX, fromY);
            }
            else
            {
                kind = MoveKind.HiddenStrongerReturn;
            }

            AppLogger.Log($"(Action) Hidden capture {piece.Type} ({fromX},{fromY})->({toX},{toY}): revealed {target.Color} {target.Type}, {kind}", LogLevel.DEBUG);
            RecordDarkChessAction(pieceBefore, fromX, fromY, toX, toY, kind, mover, revealed, captured, clocks, before);

            // Board events after the record, like ExecuteMove's (the board is already final).
            switch (kind)
            {
                case MoveKind.HiddenCapture:
                    PieceCaptured?.Invoke(target);
                    PieceRemoved?.Invoke(target);
                    PieceMoved?.Invoke(piece, toX, toY);
                    break;
                case MoveKind.HiddenStrongerSuicide:
                    PieceCaptured?.Invoke(piece);
                    PieceRemoved?.Invoke(piece);
                    break;
            }

            EndDarkChessAction();
        }

        /// <summary>
        /// Applies an already-validated 自殺 move (<see cref="Rules.CanSuicide"/>,
        /// <see cref="Piece.IsSuicideMove"/>) as the side to move's turn: <paramref name="piece"/>
        /// dies (taken off the board) and the face-up enemy piece on (toX, toY) stays
        /// (<see cref="MoveKind.Suicide"/>). Recorded, logged and undone like a move.
        /// </summary>
        private void ExecuteSuicide(Piece piece, int toX, int toY)
        {
            var mover = CurrentTurn;
            int fromX = piece.X;
            int fromY = piece.Y;
            var clocks = (Player1.Timer.GetClockState(), Player2.Timer.GetClockState());
            var before = SnapshotHistoryCounts();
            var pieceBefore = piece.CurrentInfo.Clone();
            var target = Board.GetPiece(toX, toY).CurrentInfo.Clone();

            Board.AdvanceTurn();
            Board.RemovePiece(fromX, fromY);

            AppLogger.Log($"(Action) Suicide {piece.Type} ({fromX},{fromY})->({toX},{toY}) onto {target.Color} {target.Type}", LogLevel.DEBUG);
            RecordDarkChessAction(pieceBefore, fromX, fromY, toX, toY, MoveKind.Suicide, mover, target, null, clocks, before);

            PieceCaptured?.Invoke(piece);
            PieceRemoved?.Invoke(piece);

            EndDarkChessAction();
        }

        /// <summary>
        /// Records a dark-chess action (flip, hidden capture or 自殺) applied since
        /// <paramref name="before"/> as the next move — <see cref="LastMove"/>, the move list
        /// and the undo data (both clocks, every changed piece) — marks the game changed and
        /// writes its game-log line (<see cref="FormatDarkChessLine"/>).
        /// </summary>
        private void RecordDarkChessAction(PieceInfo pieceBefore, int fromX, int fromY, int toX, int toY, MoveKind kind,
            PlayerSide mover, PieceInfo revealed, Piece captured, (ClockState, ClockState) clocks, Dictionary<Piece, int> before)
        {
            int ply = _moves.Count + 1;
            // The captured piece is the revealed target (as it was before being taken off).
            LastMove = new MoveRecord(pieceBefore, fromX, fromY, toX, toY, captured != null ? revealed : null, ply, MoveNumberOf(ply),
                kind: kind, side: mover, revealed: revealed);
            _moves.Add(LastMove);
            _capturedPieces.Add(captured);
            _clocksBeforeMove.Add(clocks);
            _stateChanges.Add(ChangesSince(before));
            HasUnsavedChanges = true;

            string line = FormatDarkChessLine(LastMove);
            AppLogger.Log(line, LogLevel.DEBUG);
            Logger?.AddMessage(line);
        }

        /// <summary>
        /// The end of a dark-chess action: raises <see cref="MoveRecorded"/>, clears the
        /// selection, recomputes the hanging pieces and hands the turn over.
        /// </summary>
        private void EndDarkChessAction()
        {
            MoveRecorded?.Invoke(LastMove);

            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }

            UpdateHangingPieces();
            if (EndDarkChessGameIfOver(LastMove.Side))
                return;
            SwitchTurn();
        }

        /// <summary>
        /// Dark-chess game over (HalfCenter), checked after each action of
        /// <paramref name="mover"/> before the turn is handed over (like the Full board's
        /// checkmate check, so the loser's clock never starts): a player with no piece left
        /// (face up or face down) loses (<see cref="GameOverReason.NoPiecesLeft"/>) — the
        /// opponent first, then the mover (whose last piece can die in a hidden capture,
        /// <see cref="MoveKind.HiddenStrongerSuicide"/>); otherwise the opponent loses if it
        /// has no action on its turn (no legal move and nothing to flip,
        /// <see cref="Board.HasAnyAction"/>; <see cref="GameOverReason.Stalemate"/>). Draw rules
        /// are not decided yet (docs/DARK-CHESS-RULES.md §1.1), so none is applied.
        /// </summary>
        /// <returns>Whether the game ended.</returns>
        private bool EndDarkChessGameIfOver(PlayerSide mover)
        {
            var opponent = OpponentOf(mover);
            // Nobody owns a piece before the factions are decided (every action decides them,
            // so this only guards against a custom position).
            if (ColorOf(opponent) == PieceColor.None)
                return false;

            if (Board.QueryPieces(side: opponent).Count == 0)
                EndGame(mover, opponent, GameOverReason.NoPiecesLeft);
            else if (Board.QueryPieces(side: mover).Count == 0)
                EndGame(opponent, mover, GameOverReason.NoPiecesLeft);
            else if (!Board.HasAnyAction(opponent))
                EndGame(mover, opponent, GameOverReason.Stalemate);
            return IsGameOver;
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
        /// the moved piece's colour, like the notation's piece characters.
        /// </summary>
        public static string FormatMoveLine(MoveRecord move) =>
            $"第{move.MoveNumber}回合 {(move.Color == PieceColor.Red ? "紅" : "黑")}：{move.Notation}";

        /// <summary>
        /// The game-log line of a dark-chess action, which has no notation (e.g.
        /// <c>第1回合 紅：翻開(3,2) 俥</c>). The side name is the mover's colour as decided
        /// now (<see cref="ColorOf"/>), so call it while the action is on the board.
        /// </summary>
        private string FormatDarkChessLine(MoveRecord move)
        {
            string head = $"第{move.MoveNumber}回合 {ColorName(ColorOf(move.Side))}：";
            switch (move.Kind)
            {
                case MoveKind.Flip:
                    return head + $"翻開({move.FromX},{move.FromY}) {PieceText(move.Revealed)}";
                case MoveKind.HiddenCapture:
                    return head + $"{HiddenCaptureHead(move)}，吃掉";
                case MoveKind.HiddenOwnPiece:
                    return head + $"{HiddenCaptureHead(move)}（己方），退回原位";
                case MoveKind.HiddenStrongerReturn:
                    return head + $"{HiddenCaptureHead(move)}（吃不了），退回原位";
                case MoveKind.HiddenStrongerSuicide:
                    return head + $"{HiddenCaptureHead(move)}（吃不了），{PieceText(move.Piece)}陣亡";
                case MoveKind.Suicide:
                    return head + $"{PieceText(move.Piece)}({move.FromX},{move.FromY})撞({move.ToX},{move.ToY}){PieceText(move.Revealed)}，自殺陣亡";
                default:
                    return head + $"{PieceText(move.Piece)}({move.FromX},{move.FromY})→({move.ToX},{move.ToY})"
                        + (move.Captured != null ? $"，吃{PieceText(move.Captured)}" : "");
            }
        }

        /// <summary>The common start of a hidden-capture log line, e.g. 紅俥(2,1)暗吃(3,1)，翻出黑卒.</summary>
        private static string HiddenCaptureHead(MoveRecord move) =>
            $"{PieceText(move.Piece)}({move.FromX},{move.FromY})暗吃({move.ToX},{move.ToY})，翻出{PieceText(move.Revealed)}";

        /// <summary>A piece's character with its colour name, e.g. 黑卒 (see <see cref="PieceConstants.GetPieceText"/>).</summary>
        private static string PieceText(PieceInfo info) =>
            info == null ? "?" : ColorName(info.Color) + PieceConstants.GetPieceText(info.Type, info.Color);

        /// <summary>紅 / 黑 for the log lines; 未定 for a colour not decided yet.</summary>
        private static string ColorName(PieceColor color) => color switch
        {
            PieceColor.Red => "紅",
            PieceColor.Black => "黑",
            _ => "未定",
        };

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
        /// For a move replayed from a saved game (no clock history) both totals stay as they
        /// are and the mover's step starts from zero.
        /// </para>
        /// <para>
        /// A dark-chess flip or hidden capture is taken back as a whole: every piece it changed
        /// gets its earlier state back (<see cref="Board.RevertStates"/>) — a flipped piece is
        /// face down again, and undoing the game's first flip also undoes the faction decision
        /// (nobody owns a colour again, see <see cref="ColorOf"/>).
        /// </para>
        /// Events, in order: <see cref="PieceUnselected"/> (if a piece was selected),
        /// <see cref="PieceMoved"/> (piece, fromX, fromY; for a dark-chess action, each piece
        /// back on a different square), <see cref="PieceAdded"/> (the captured piece, if any;
        /// for a dark-chess action, each piece back on the board), <see cref="TurnChanged"/>,
        /// <see cref="HangingPiecesChanged"/>, <see cref="MoveUndone"/>.
        /// </summary>
        /// <returns>The record that was taken back; null when nothing could be undone.</returns>
        private MoveRecord UndoLastMove()
        {
            if (_moves.Count <= UndoFloor)
                return null;

            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }

            int last = _moves.Count - 1;
            var record = _moves[last];
            var captured = _capturedPieces[last];
            var clocks = _clocksBeforeMove[last];
            var changes = _stateChanges[last];

            if (changes != null)
            {
                UndoStateChanges(record, changes);
            }
            else
            {
                var piece = Board.GetPiece(record.ToX, record.ToY);
                // Written before the board changes back (the side names follow the factions).
                string undoLine = record.Notation != null ? $"(Undo) {FormatMoveLine(record)}"
                    : Board.UsesDarkChessRules ? $"(Undo) {FormatDarkChessLine(record)}"
                    : $"(Undo) {piece.Type} back to ({record.FromX},{record.FromY})";

                Board.UnmakeMove(piece, record.FromX, record.FromY, record.ToX, record.ToY, captured);

                AppLogger.Log($"(Undo) {piece.Type} back to ({record.FromX},{record.FromY})", LogLevel.DEBUG);
                Logger?.AddMessage(undoLine);

                PieceMoved?.Invoke(piece, record.FromX, record.FromY);
                if (captured != null)
                    PieceAdded?.Invoke(captured);
            }

            Board.RetreatTurn();
            _moves.RemoveAt(last);
            _capturedPieces.RemoveAt(last);
            _clocksBeforeMove.RemoveAt(last);
            _stateChanges.RemoveAt(last);
            LastMove = _moves.Count > 0 ? _moves[_moves.Count - 1] : null;
            HasUnsavedChanges = true;

            // Reopen an ended game.
            IsGameOver = false;
            Winner = PlayerSide.None;
            Result = null;

            var mover = record.Side;
            // Unknown clocks (a move replayed from a saved game): keep both totals, new step.
            var restored = clocks ?? (
                new ClockState(TimeSpan.Zero, Player1.Timer.CurrentTotalTime),
                new ClockState(TimeSpan.Zero, Player2.Timer.CurrentTotalTime));
            Player1.Timer.RestoreClockState(restored.Player1, active: mover == PlayerSide.Player1, paused: IsPaused);
            Player2.Timer.RestoreClockState(restored.Player2, active: mover == PlayerSide.Player2, paused: IsPaused);

            // Set before the turn switch so TurnChanged handlers already see it.
            IsInCheck = Board.UsesCheckRules && Board.IsSideInCheck(mover);
            CurrentTurn = mover;

            UpdateHangingPieces();
            MoveUndone?.Invoke(record);
            return record;
        }

        /// <summary>
        /// The board half of undoing a dark-chess action (see <see cref="UndoLastMove"/>):
        /// logs it, restores every changed piece (<see cref="Board.RevertStates"/>) and raises
        /// <see cref="PieceMoved"/> for each piece back on a different square and
        /// <see cref="PieceAdded"/> for each piece back on the board.
        /// </summary>
        private void UndoStateChanges(MoveRecord record, List<(Piece piece, int snapshots)> changes)
        {
            // Written before the board changes back (the side names follow the factions).
            string line = $"(Undo) {FormatDarkChessLine(record)}";

            var wasOnBoard = new Dictionary<Piece, (bool onBoard, int x, int y)>();
            foreach (var (p, _) in changes)
                wasOnBoard[p] = (Board.GetPiece(p.X, p.Y) == p, p.X, p.Y);

            Board.RevertStates(changes);

            AppLogger.Log($"(Undo) {record.Kind} at ({record.ToX},{record.ToY}) taken back", LogLevel.DEBUG);
            Logger?.AddMessage(line);

            foreach (var (p, _) in changes)
            {
                var (onBoard, x, y) = wasOnBoard[p];
                if (Board.GetPiece(p.X, p.Y) != p)
                    continue;
                if (!onBoard)
                    PieceAdded?.Invoke(p);
                else if (p.X != x || p.Y != y)
                    PieceMoved?.Invoke(p, p.X, p.Y);
            }
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

        /// <summary>
        /// Clears both clocks for a new game; optionally starts the first step of the side
        /// to move (<see cref="CurrentTurn"/>, set before this is called).
        /// Also clears the pause, game-over and check state and the move history
        /// (<see cref="Moves"/>, <see cref="LastMove"/>, <see cref="UndoFloor"/>,
        /// <see cref="HasUnsavedChanges"/>).
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
            _moves.Clear();
            _capturedPieces.Clear();
            _clocksBeforeMove.Clear();
            _stateChanges.Clear();
            UndoFloor = 0;
            HasUnsavedChanges = false;
            if (startFirstTurn)
                (CurrentTurn == PlayerSide.Player2 ? Player2 : Player1).Timer.StartStep();
        }

        /// <summary>
        /// A clock ran out. With <c>Rules.EndGameWhenTimesUp</c> (default) its owner loses (the
        /// PlayerTimer has already set itself Terminated); otherwise the game goes on and the
        /// clock keeps running into overtime, shown as a negative time
        /// (<see cref="PlayerTimer.ContinueAfterTimeUp"/>); this is raised once per overtime. Never happens with count-up clocks (正數, only
        /// measuring: <see cref="PlayerTimer.HasTimeLimit"/>); ignored there just in case.
        /// </summary>
        private void OnTimeUp(Player loser)
        {
            if (IsGameOver || Rules.TimerMode == TimerMode.CountUp)
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

            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
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
