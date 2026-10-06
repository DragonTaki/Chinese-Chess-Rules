/* ----- ----- ----- ----- */
// GameManager.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2026/10/06
// Version: v1.9
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Chinese_Chess_v3.Game.Core.Boards;
using Chinese_Chess_v3.Game.Core.Families.ThreeKingdoms;
using Chinese_Chess_v3.Game.Core.Endgames;
using Chinese_Chess_v3.Game.Core.Notation;
using Chinese_Chess_v3.Game.Core.Openings;
using Chinese_Chess_v3.Game.Core.Pgn;
using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;
using Chinese_Chess_v3.Game.Core.Saves;


namespace Chinese_Chess_v3.Game.Core
{
    public class GameManager
    {
        /// <summary>
        /// Raised for each game-log entry (a game set up, a click, a move, a check, an undo, the
        /// result... see <see cref="GameLogEvent"/>), as plain data at the moment it happens; the
        /// logic layer above composes the lines the sidebar's log box shows.
        /// </summary>
        public event Action<GameLogEvent> Logged;

        /// <summary>
        /// The board of the current game. Replaced by a new <see cref="Boards.Board"/> instance
        /// when a game is set up on a different <see cref="BoardType"/> (see
        /// <see cref="LoadCustomBoard"/>), so read it from here each time instead of keeping it.
        /// </summary>
        public Board Board { get; private set; }
        public Player Player1 { get; private set; }
        public Player Player2 { get; private set; }

        /// <summary>三國's third player (its clock only runs in a 三國 game).</summary>
        public Player Player3 { get; private set; }

        /// <summary>The three players, Player1 first.</summary>
        private Player[] AllPlayers => new[] { Player1, Player2, Player3 };

        /// <summary>The player of <paramref name="side"/>; null for a side that is not a player.</summary>
        public Player PlayerOf(PlayerSide side) => side switch
        {
            PlayerSide.Player1 => Player1,
            PlayerSide.Player2 => Player2,
            PlayerSide.Player3 => Player3,
            _ => null,
        };

        /// <summary>How many players the current game has (2; 三國 3).</summary>
        public int PlayerCount => Board.Family.PlayerCount;

        /// <summary>Every player's clock as it is now (Player1 first), for undo.</summary>
        private ClockState[] CaptureClocks() => AllPlayers.Select(p => p.Timer.GetClockState()).ToArray();
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
        /// the UI does not announce it. Also true while a position set up with its side to move
        /// already checkmated or stalemated is ended for display (see <see cref="LoadCustomBoard"/>).
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
        /// undo and replay. Read-only view of the live list.
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
        private readonly List<ClockState[]> _clocksBeforeMove = new List<ClockState[]>();

        // 三國: the claims, scores and outs before each move (null on other boards), for undo.
        private readonly List<ThreeKingdomsState> _kingdomsBefore = new List<ThreeKingdomsState>();
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

        /// <summary>How many moves one <see cref="Undo"/> takes back: a round, the last move of each player (2; 三國 3).</summary>
        public int UndoRoundPlies => PlayerCount;

        /// <summary>
        /// Whether <see cref="Undo"/> can take a round back: there are at least
        /// <see cref="UndoRoundPlies"/> moves above <see cref="UndoFloor"/> (e.g. not with Black
        /// to move after Red's first move). Also true after the game has ended (undoing
        /// reopens it) and while paused.
        /// </summary>
        /// 三國: not once a 棄權 (or a time-up, which counts as one) has ended the game — a
        /// resignation is not a move, so undoing moves cannot take it back.
        public bool CanUndo => _moves.Count - UndoFloor >= UndoRoundPlies
            && !(IsGameOver && Board.ThreeKingdoms != null && Result?.Reason == GameOverReason.Resign);

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
        /// them. The launchers pass the rule sets built from the player settings.
        /// The first game is a <see cref="GameKind.Traditional"/> one.
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
            Player3 = new Player(PlayerSide.Player3, rules.TotalTimeLimit, rules.StepTimeLimit, rules.IncrementPerMove, rules.EnableStepTimer, rules.TimerMode);

            // The player whose clock runs out loses.
            Player1.Timer.TimeUp += () => OnTimeUp(Player1);
            Player2.Timer.TimeUp += () => OnTimeUp(Player2);
            Player3.Timer.TimeUp += () => OnTimeUp(Player3);

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

        public void ResetBoardToDefault()
        {
            // Load default pieces (the standard start: Red moves first, so Red is Player1)
            SetUpPosition(BoardConfigLoader.Load(), null);
        }

        /// <summary>
        /// Starts a new HalfCenter game (台灣暗棋半盤, 8×4) from a layout <paramref name="deal"/>
        /// makes: the 32 pieces owned by nobody, face down when <see cref="Rules.IsHiddenChess"/>,
        /// otherwise face up (明棋半盤). Player1 acts first — its first flip (or, face up, its first
        /// move) decides who plays which colour (see <see cref="ColorOf"/>). Dealing is not a rule:
        /// the caller deals (a local game's dealer in the logic layer, an online game's server).
        /// </summary>
        /// <param name="hiddenChess">
        /// 暗棋半盤 (true: <see cref="GameKind.DarkHalf"/>, face down) or 明棋半盤 (false:
        /// <see cref="GameKind.OpenHalf"/>, face up); the new-game menu picks it. The game plays
        /// by a copy of that kind's <see cref="DefaultRulesFor"/>, its <see cref="Rules.IsHiddenChess"/>
        /// set to match, so <see cref="Rules"/> tells a restart which variant to deal again.
        /// </param>
        /// <param name="deal">
        /// Makes a layout: given whether the pieces are face down, 32 pieces owned by nobody, one per
        /// square. Called now and again on every <see cref="Restart"/> (a restart deals a new layout).
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="deal"/> is null.</exception>
        public void StartHalfCenter(bool hiddenChess, Func<bool, List<PieceInfo>> deal)
        {
            ArgumentNullException.ThrowIfNull(deal);
            var rules = DefaultRulesFor(hiddenChess ? GameKind.DarkHalf : GameKind.OpenHalf).Clone();
            rules.IsHiddenChess = hiddenChess;
            _halfCenterDeal = deal;
            SetUpHalfCenter(rules);
        }

        // The dealer of the HalfCenter game started with StartHalfCenter, for Restart to deal again.
        private Func<bool, List<PieceInfo>> _halfCenterDeal;

        /// <summary>
        /// Starts a new 揭棋 game (<see cref="GameKind.Flip"/>, Full board, <see cref="Board.IsJieqi"/>)
        /// from a layout <paramref name="deal"/> makes: both Generals face up on their squares, each
        /// side's other 15 pieces face down, shuffled over that side's other starting squares. Red
        /// (Player1) moves first. A face-down piece moves as its square's piece and turns face up
        /// when it has moved. Dealing is not a rule: the caller deals (the client's dealer, the server).
        /// Not saved: a 揭棋 position has no FEN.
        /// </summary>
        /// <param name="deal">Makes a layout; called now and again on every <see cref="Restart"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="deal"/> is null.</exception>
        public void StartJieqi(Func<List<PieceInfo>> deal)
        {
            ArgumentNullException.ThrowIfNull(deal);
            _jieqiDeal = deal;
            SetUpJieqi(DefaultRulesFor(GameKind.Flip).Clone());
        }

        // The dealer of the 揭棋 game started with StartJieqi, for Restart to deal again.
        private Func<List<PieceInfo>> _jieqiDeal;

        /// <summary>
        /// Starts a new 三國 game (<see cref="GameKind.ThreeKingdoms"/>, HalfCross board, three
        /// players) from a layout <paramref name="deal"/> makes: the 32 pieces owned by nobody, face
        /// down, 8 in each corner block. Player1 acts first; flips decide the teams
        /// (<see cref="TeamOf"/>). Dealing is not a rule: the caller deals (the client's dealer, the server).
        /// Not saved: a 三國 game has no saved-game format yet.
        /// </summary>
        /// <param name="deal">Makes a layout; called now and again on every <see cref="Restart"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="deal"/> is null.</exception>
        /// <exception cref="NotSupportedException">The rules pick 收軍 (<see cref="HalfCrossWinCondition.Recall"/>), whose rules are not decided yet.</exception>
        /// <exception cref="InvalidOperationException">The team split (<see cref="Rules.HalfCrossTeams"/>) leaves a team without pieces.</exception>
        public void StartThreeKingdoms(Func<List<PieceInfo>> deal)
        {
            ArgumentNullException.ThrowIfNull(deal);
            var rules = DefaultRulesFor(GameKind.ThreeKingdoms).Clone();
            if (rules.HalfCrossWinCondition == HalfCrossWinCondition.Recall)
                throw new NotSupportedException("收軍 (Recall) is not decided yet");
            if (!rules.HalfCrossTeams.IsValid)
                throw new InvalidOperationException("Every 三國 team needs at least one piece");
            _threeKingdomsDeal = deal;
            SetUpThreeKingdoms(rules);
        }

        // The dealer of the 三國 game started with StartThreeKingdoms, for Restart to deal again.
        private Func<List<PieceInfo>> _threeKingdomsDeal;

        /// <summary><see cref="StartThreeKingdoms"/> played by <paramref name="rules"/> (this game's own copy).</summary>
        private void SetUpThreeKingdoms(Rules rules)
        {
            SetUpPosition(_threeKingdomsDeal(), null, BoardType.HalfCross, rules);
            CoreLog.Log($"(ThreeKingdoms) Started a 三國 game, {rules.HalfCrossWinCondition}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.ThreeKingdomsStarted(rules.HalfCrossWinCondition));
        }

        /// <summary><see cref="StartJieqi"/> played by <paramref name="rules"/> (this game's own copy).</summary>
        private void SetUpJieqi(Rules rules)
        {
            SetUpPosition(_jieqiDeal(), null, BoardType.Full, rules, firstColor: PieceColor.Red, jieqi: true);
            CoreLog.Log("(Jieqi) Started a 揭棋 game", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.JieqiStarted());
        }

        /// <summary><see cref="StartHalfCenter"/> played by <paramref name="rules"/> (this game's own copy; its <see cref="Rules.IsHiddenChess"/> picks the variant).</summary>
        private void SetUpHalfCenter(Rules rules)
        {
            var pieces = _halfCenterDeal(rules.IsHiddenChess);
            SetUpPosition(pieces, null, BoardType.HalfCenter, rules);
            CoreLog.Log($"(DarkChess) Started a HalfCenter game, hidden: {rules.IsHiddenChess}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.HalfCenterStarted(rules.IsHiddenChess));
        }

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
            CoreLog.Log($"(Endgame) Started {puzzle.FileName}: {puzzle.Title}, {firstColor} to move", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.EndgameStarted(puzzle.Title, puzzle.Goal));
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
                    if (_halfCenterDeal == null)
                        throw new InvalidOperationException("This HalfCenter game has no dealer to restart with");
                    SetUpHalfCenter(rules);
                }
                return;
            }
            if (Board.Type == BoardType.HalfCross)
            {
                if (_threeKingdomsDeal == null)
                    throw new InvalidOperationException("This 三國 game has no dealer to restart with");
                SetUpThreeKingdoms(rules);
                return;
            }
            if (Board.Type != BoardType.Full)
                throw new NotSupportedException($"Cannot restart a {Board.Type} game yet");

            // A 揭棋 game is dealt again.
            if (Board.IsJieqi)
            {
                if (_jieqiDeal == null)
                    throw new InvalidOperationException("This 揭棋 game has no dealer to restart with");
                SetUpJieqi(rules);
                CoreLog.Log($"(Restart) Restarted the {Mode} game", CoreLogLevel.Debug);
                return;
            }

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
            CoreLog.Log($"(Restart) Restarted the {Mode} game", CoreLogLevel.Debug);
        }

        /// <summary>
        /// <see cref="Restart"/> of a loaded saved game (author decision 2026-10-02: restarting it
        /// = loading it again): the same <see cref="SavedGame"/> loaded again by
        /// <paramref name="rules"/> (the loaded game's), so the position, moves, undo floor,
        /// clocks and ending are exactly as when it was loaded. The record read at load time is
        /// used, not the file: it already is the file as loaded, and a file edited, overwritten or
        /// deleted since cannot change or break the restart.
        /// </summary>
        private void RestartSavedGame(SavedGame saved, Rules rules) => SetUpSavedGame(saved, rules, isRestart: true);

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
            Logged?.Invoke(new GameLogEvent.OpeningStarted(opening.Title, opening.Ecco));

            int played = 0;
            foreach (var move in opening.Moves)
            {
                if (!TryMove(move.FromX, move.FromY, move.ToX, move.ToY))
                {
                    CoreLog.Log($"(Opening) {opening.FileName}: move {played + 1} ({move}) is not legal here; line stopped", CoreLogLevel.Warn);
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
            CoreLog.Log($"(Opening) Started {opening.FileName}: {opening.Title}, {played} move(s) played, {CurrentTurn} to move", CoreLogLevel.Debug);
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
        /// <param name="jieqi">A 揭棋 game (<see cref="Board.IsJieqi"/>; Full board only).</param>
        /// <exception cref="ArgumentException">Full board: <paramref name="firstColor"/> is not
        /// Red/Black, or a piece is neither Red nor Black.</exception>
        private void SetUpPosition(List<PieceInfo> pieces, PgnGameFile source, BoardType boardType = BoardType.Full,
            Rules rules = null, PlayerSide? localSide = null, PieceColor firstColor = PieceColor.Red, bool jieqi = false)
        {
            _customHalfCenterStart = null;

            // Players are numbered by turn order: on the Full board the first colour is Player1's.
            if (boardType == BoardType.Full)
                pieces = PieceColors.AssignOwners(pieces, firstColor);

            // A different board type needs a differently-sized grid: a new board (its rules
            // are set right below).
            if (Board.Type != boardType)
                Board = new Board(boardType, Board.GameRules);
            // 揭棋 is the kind of game, set by StartJieqi only; every other setup is not one.
            Board.IsJieqi = jieqi && boardType == BoardType.Full;

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
            // A 揭棋 position (face-down pieces) has no FEN, so it cannot be saved.
            InitialFen = Board.IsJieqi ? null : FormatInitialFen(_player1Color);
            ResetTimers(startFirstTurn: true);
            // A custom position may start with the side to move already in check.
            IsInCheck = Board.UsesCheckRules && Board.IsSideInCheck(PlayerSide.Player1);

            // Inform UI
            BoardReset?.Invoke();

            // Inform pieces added
            foreach (var p in Board.GetAllPieces())
                PieceAdded?.Invoke(p);

            UpdateHangingPieces();

            // A position whose side to move is already checkmated or stalemated (a custom board
            // or an endgame) is shown as an ended game (author decision 2026-10-02): it ends at
            // once, flagged IsReplaying so no result dialog is shown.
            if (Board.UsesCheckRules && Board.GetGeneral(PlayerSide.Player1) != null && !Board.HasAnyLegalMove(PlayerSide.Player1))
            {
                bool replaying = IsReplaying;
                IsReplaying = true;
                try
                {
                    EndGame(PlayerSide.Player2, PlayerSide.Player1, IsInCheck ? GameOverReason.Checkmate : GameOverReason.Stalemate);
                }
                finally
                {
                    IsReplaying = replaying;
                }
            }
        }

        /// <summary>
        /// Makes <paramref name="rules"/> the current game's <see cref="Rules"/>: the board's
        /// move rules and both clocks' limits, increment, step timer switch and count mode.
        /// Elapsed times are not touched.
        /// </summary>
        private void ApplyRules(Rules rules)
        {
            Board.SetRules(rules);
            foreach (var timer in AllPlayers.Select(p => p.Timer))
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
                CoreLog.Log($"(Save) Start position has no FEN: {ex.Message}", CoreLogLevel.Warn);
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
            CoreLog.Log($"(Save) Saved {Moves.Count} move(s) to {filePath}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.GameSaved(Path.GetFileName(filePath)));
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
        public int LoadSavedGame(SavedGame saved) => SetUpSavedGame(saved, null, isRestart: false);

        /// <summary>
        /// <see cref="LoadSavedGame"/> played by <paramref name="rules"/> (this game's own copy;
        /// null for <see cref="SavedGame.RulesFor"/> over the current <see cref="GameKind.Traditional"/> defaults),
        /// its game-log entry marked as a restart when <paramref name="isRestart"/>. <see cref="Restart"/> of a loaded
        /// saved game uses it with the loaded game's rules, so it comes back exactly as loaded.
        /// </summary>
        private int SetUpSavedGame(SavedGame saved, Rules rules, bool isRestart)
        {
            ArgumentNullException.ThrowIfNull(saved);
            var (pieces, firstColor) = XiangqiFen.Parse(saved.Fen);
            SetUpPosition(pieces, saved, BoardType.Full, rules, firstColor: firstColor);
            Logged?.Invoke(new GameLogEvent.SavedGameStarted(saved.Title, isRestart));

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
                    CoreLog.Log($"(Load) {saved.FileName}: move {played + 1} ({move}) is not legal here; replay stopped", CoreLogLevel.Warn);
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
            CoreLog.Log($"(Load) Loaded {saved.FileName}: {played} move(s) replayed, {CurrentTurn} to move, over: {IsGameOver}", CoreLogLevel.Debug);
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
            // 三國's players own teams, not colours (TeamOf).
            if (Board.ThreeKingdoms != null)
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

        /// <summary>三國: the team (1..3) <paramref name="side"/> claimed; 0 while it has none, or off the 三國 board.</summary>
        public int TeamOf(PlayerSide side) =>
            Board.ThreeKingdoms != null && side is PlayerSide.Player1 or PlayerSide.Player2 or PlayerSide.Player3
                ? Board.ThreeKingdoms.Teams[ThreeKingdomsState.Index(side)] : 0;

        /// <summary>三國: <paramref name="side"/>'s points; 0 off the 三國 board.</summary>
        public int ScoreOf(PlayerSide side) =>
            Board.ThreeKingdoms != null && side is PlayerSide.Player1 or PlayerSide.Player2 or PlayerSide.Player3
                ? Board.ThreeKingdoms.Scores[ThreeKingdomsState.Index(side)] : 0;

        /// <summary>三國: whether <paramref name="side"/> resigned (棄權; its turns are skipped).</summary>
        public bool HasResigned(PlayerSide side) =>
            Board.ThreeKingdoms != null && side is PlayerSide.Player1 or PlayerSide.Player2 or PlayerSide.Player3
            && Board.ThreeKingdoms.Resigned[ThreeKingdomsState.Index(side)];

        /// <summary>三國: the number <paramref name="side"/> is ranked by (計分: points above its team's threshold); 0 off the 三國 board.</summary>
        public int RankingScoreOf(PlayerSide side) =>
            Board.ThreeKingdoms != null && side is PlayerSide.Player1 or PlayerSide.Player2 or PlayerSide.Player3
                ? ThreeKingdomsStandings.RankingScore(Board, side) : 0;

        /// <summary>三國: whether <paramref name="side"/> still plays (not out, not resigned); true off the 三國 board.</summary>
        public bool IsStillPlaying(PlayerSide side) =>
            Board.ThreeKingdoms == null || ThreeKingdomsStandings.IsPlaying(Board, side);

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
            if (IsPaused || IsGameOver)
                return false;

            if (!ActionResolver.IsLegalFlip(Board, x, y))
                return false;

            ExecuteFlip(Board.GetPiece(x, y));
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
        private bool IsSelectable(Piece piece) => ActionResolver.CanAct(Board, piece, CurrentTurn);

        /// <summary>
        /// A piece for the click log lines: its type, or just "face-down" for a face-down piece
        /// (dark chess, 揭棋, 三國: its type is hidden information
        /// and the game log is visible to both players).
        /// </summary>
        private string DescribeForLog(Piece piece)
        {
            if (piece == null)
                return "null";
            if (!piece.CurrentInfo.IsFaceUp)
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
            CoreLog.Log(
                $"Current turn: {CurrentTurn}, holding: {(_selectedPiece == null ? "null" : _selectedPiece.Type.ToString())},\n" +
                $"clicked at ({x},{y}), on: {DescribeForLog(clickedPiece)}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.BoardClicked(CurrentTurn, _selectedPiece?.Type, x, y, clickedPiece?.Type,
                clickedPiece != null && !clickedPiece.CurrentInfo.IsFaceUp));

            // No selected piece: flip a face-down piece (dark chess), or try to select one
            if (_selectedPiece == null)
            {
                if (Board.Family.CanFlip(Board, clickedPiece))
                {
                    ExecuteFlip(clickedPiece);
                    return;
                }
                if (IsSelectable(clickedPiece))
                {
                    _selectedPiece = clickedPiece;
                    CoreLog.Log($"(Action) Selected {clickedPiece.Type} at ({x},{y})", CoreLogLevel.Debug);
                    Logged?.Invoke(new GameLogEvent.SelectionChanged(SelectionChange.Selected, clickedPiece.Type, x, y));
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
                    CoreLog.Log($"(Action) Un-selected {_selectedPiece.Type} at ({x},{y})", CoreLogLevel.Debug);
                    Logged?.Invoke(new GameLogEvent.SelectionChanged(SelectionChange.Unselected, _selectedPiece.Type, x, y));
                    PieceUnselected?.Invoke(_selectedPiece);
                    _selectedPiece = null;
                }
                else
                {
                    CoreLog.Log($"(Action) Switched to {clickedPiece.Type} at ({x},{y})", CoreLogLevel.Debug);
                    Logged?.Invoke(new GameLogEvent.SelectionChanged(SelectionChange.Switched, clickedPiece.Type, x, y));
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
                    CoreLog.Log($"(Action) Un-selected {_selectedPiece.Type} at ({x},{y})", CoreLogLevel.Debug);
                    Logged?.Invoke(new GameLogEvent.SelectionChanged(SelectionChange.Unselected, _selectedPiece.Type, x, y));
                }
                // Invalid catch
                else
                {
                    CoreLog.Log($"(Action) Invalid move to ({x},{y})", CoreLogLevel.Debug);
                    Logged?.Invoke(new GameLogEvent.SelectionChanged(SelectionChange.Invalid, _selectedPiece.Type, x, y));
                }
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }
        }

        /// <summary>
        /// Applies an already-validated move for the side to move: advances the board's turn
        /// counter (so the pieces' history snapshots carry the move number), lets
        /// <see cref="ActionResolver.ApplyMove"/> change the board (a capture, or on the dark-chess
        /// board a hidden capture / 自殺 / the faction decision of 明棋半盤's first move), then
        /// records it, raises the events, clears the selection and switches the turn or ends the
        /// game. Shared by <see cref="HandleClick"/> and <see cref="TryMove"/>.
        /// </summary>
        private void ExecuteMove(Piece piece, int toX, int toY)
        {
            var mover = CurrentTurn;
            // Both clocks as they are right before the move, for Undo.
            var clocks = CaptureClocks();
            var kingdomsBefore = Board.ThreeKingdoms?.Clone();
            // Pre-move facts for the "newly ..." tactical events, taken on the unchanged board.
            var tacticalBefore = Board.UsesCheckRules ? TacticalAnalysis.TakeSnapshot(Board, piece.Side) : null;
            // A dark-chess action or a 揭棋 reveal is undone piece by piece: every piece's history length now.
            var historyBefore = Board.UsesDarkChessRules || Board.IsJieqi ? SnapshotHistoryCounts() : null;

            Board.AdvanceTurn();
            var outcome = ActionResolver.ApplyMove(Board, mover, piece, toX, toY);

            if (outcome.Kind != MoveKind.Move)
            {
                FinishDarkChessAction(outcome, clocks, kingdomsBefore, historyBefore);
                return;
            }

            int ply = _moves.Count + 1;
            LastMove = new MoveRecord(outcome.PieceBefore, outcome.FromX, outcome.FromY, toX, toY, outcome.Captured,
                ply, MoveNumberOf(ply), outcome.GivesCheck, outcome.Notation, outcome.Iccs, side: mover);
            _moves.Add(LastMove);
            _capturedPieces.Add(outcome.CapturedPiece);
            _clocksBeforeMove.Add(clocks);
            _kingdomsBefore.Add(kingdomsBefore);
            // A faction decision changes every piece's owner and a 揭棋 move turns the piece face up:
            // undone piece by piece; any other move by Board.UnmakeMove.
            _stateChanges.Add(outcome.DecidesFactions || outcome.Revealed != null ? ChangesSince(historyBefore) : null);
            HasUnsavedChanges = true;

            var targetPiece = outcome.CapturedPiece;
            if (targetPiece != null)
            {
                CoreLog.Log($"(Action) Captured {targetPiece.Type} at ({toX},{toY})", CoreLogLevel.Debug);
                Logged?.Invoke(new GameLogEvent.PieceTaken(targetPiece.Type, toX, toY));
                PieceCaptured?.Invoke(targetPiece);
                PieceRemoved?.Invoke(targetPiece);
            }

            CoreLog.Log($"(Action) Moved {piece.Type} to ({toX},{toY})", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.PieceMoved(piece.Type, toX, toY));

            // raise moved event AFTER board updated
            PieceMoved?.Invoke(piece, toX, toY);

            // Readable move-list entry, in addition to the debug entries above.
            CoreLog.Log($"(Action) Recorded move {LastMove.Ply}: {LastMove.Notation ?? LastMove.Kind.ToString()}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.MovePlayed(LastMove, ColorOf(LastMove.Side), LineStyleOf(LastMove)));
            if (outcome.DecidesFactions)
                LogFactions(kingdomsBefore);
            MoveRecorded?.Invoke(LastMove);

            // unselect and notify
            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }

            // Board hints for the new position (also when this move ends the game).
            UpdateHangingPieces();

            // The side about to move is evaluated right away (ActionResolver.EvaluateEnd); the
            // turn is not handed over when the game ends, so the loser's clock never starts.
            var end = ActionResolver.EvaluateEnd(Board, mover, out bool opponentInCheck);
            if (Board.UsesCheckRules)
            {
                // Evaluated before the game-over / turn-switch bookkeeping (the board is
                // already final), raised after it.
                var tactical = TacticalAnalysis.Analyze(Board, LastMove, tacticalBefore);

                if (end != null)
                {
                    EndGame(end);
                    RaiseTacticalEvents(tactical);
                    return;
                }

                var opponent = OpponentOf(mover);
                // Set before the turn switch so TurnChanged handlers already see it.
                IsInCheck = opponentInCheck;
                SwitchTurn();
                if (opponentInCheck)
                {
                    CoreLog.Log($"(Check) {opponent} is in check", CoreLogLevel.Debug);
                    Logged?.Invoke(new GameLogEvent.CheckGiven(opponent));
                    Check?.Invoke(opponent);
                }
                RaiseTacticalEvents(tactical);
                return;
            }

            if (end != null)
            {
                EndGame(end);
                return;
            }
            SwitchTurn();
        }

        /// <summary>
        /// The move number (第N回合) of the <paramref name="ply"/>-th move: a move and the reply
        /// share a number. Black-first games (endgames; Player1 plays Black) number like PGN:
        /// Black's first move is 1, Red's reply 2. 三國: three moves (one round) share a number.
        /// </summary>
        private int MoveNumberOf(int ply) => (ply - 1 + (_player1Color == PieceColor.Black ? 1 : 0)) / PlayerCount + 1;

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
        /// turn counter and lets <see cref="ActionResolver.ApplyFlip"/> turn the piece face up
        /// (deciding the factions on the game's first flip), then records it like a move
        /// (<see cref="MoveKind.Flip"/>, for undo with both clocks), logs it, clears the selection,
        /// recomputes the hanging pieces and switches the turn.
        /// </summary>
        private void ExecuteFlip(Piece piece)
        {
            var mover = CurrentTurn;
            var clocks = CaptureClocks();
            var kingdomsBefore = Board.ThreeKingdoms?.Clone();
            var before = SnapshotHistoryCounts();

            Board.AdvanceTurn();
            var outcome = ActionResolver.ApplyFlip(Board, mover, piece);
            FinishDarkChessAction(outcome, clocks, kingdomsBefore, before);
        }

        /// <summary>
        /// Reports the faction decision to the game log: which player plays which colour, or (三國)
        /// each team claimed since <paramref name="kingdomsBefore"/>.
        /// </summary>
        private void LogFactions(ThreeKingdomsState kingdomsBefore)
        {
            if (Board.ThreeKingdoms != null)
            {
                foreach (var side in ThreeKingdomsState.Players)
                {
                    int i = ThreeKingdomsState.Index(side);
                    int team = Board.ThreeKingdoms.Teams[i];
                    if (team != 0 && kingdomsBefore?.Teams[i] != team)
                    {
                        CoreLog.Log($"(Faction) {side} claims team {team}", CoreLogLevel.Debug);
                        Logged?.Invoke(new GameLogEvent.TeamClaimed(side, team, Rules.HalfCrossTeams));
                    }
                }
                return;
            }

            var player1 = ColorOf(PlayerSide.Player1);
            var player2 = ColorOf(PlayerSide.Player2);
            CoreLog.Log($"(Faction) {PlayerSide.Player1} plays {player1}, {PlayerSide.Player2} plays {player2}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.FactionsDecided(player1, player2));
        }

        /// <summary>
        /// The bookkeeping of a dark-chess action (flip, hidden capture or 自殺) the board already
        /// shows: records it as the next move — <see cref="LastMove"/>, the move list and the undo
        /// data (both clocks, every piece changed since <paramref name="before"/>) — marks the game
        /// changed, logs it, raises the board events, then raises <see cref="MoveRecorded"/>,
        /// clears the selection, recomputes the hanging pieces and hands the turn over or ends the game.
        /// </summary>
        private void FinishDarkChessAction(ActionOutcome outcome, ClockState[] clocks, ThreeKingdomsState kingdomsBefore, Dictionary<Piece, int> before)
        {
            var piece = outcome.Piece;
            var mover = outcome.Mover;
            switch (outcome.Kind)
            {
                case MoveKind.Flip:
                    CoreLog.Log($"(Action) Flipped {piece.Color} {piece.Type} at ({piece.X},{piece.Y})", CoreLogLevel.Debug);
                    break;
                case MoveKind.Suicide:
                    CoreLog.Log($"(Action) Suicide {outcome.PieceBefore.Type} ({outcome.FromX},{outcome.FromY})->({outcome.ToX},{outcome.ToY}) onto {outcome.Revealed.Color} {outcome.Revealed.Type}", CoreLogLevel.Debug);
                    break;
                default:
                    CoreLog.Log($"(Action) Hidden capture {outcome.PieceBefore.Type} ({outcome.FromX},{outcome.FromY})->({outcome.ToX},{outcome.ToY}): revealed {outcome.Revealed.Color} {outcome.Revealed.Type}, {outcome.Kind}", CoreLogLevel.Debug);
                    break;
            }

            int ply = _moves.Count + 1;
            // The captured piece is the revealed target (as it was before being taken off).
            LastMove = new MoveRecord(outcome.PieceBefore, outcome.FromX, outcome.FromY, outcome.ToX, outcome.ToY, outcome.Captured,
                ply, MoveNumberOf(ply), kind: outcome.Kind, side: mover, revealed: outcome.Revealed);
            _moves.Add(LastMove);
            _capturedPieces.Add(outcome.CapturedPiece);
            _clocksBeforeMove.Add(clocks);
            _kingdomsBefore.Add(kingdomsBefore);
            _stateChanges.Add(ChangesSince(before));
            HasUnsavedChanges = true;

            Logged?.Invoke(new GameLogEvent.MovePlayed(LastMove, ColorOf(mover), LineStyleOf(LastMove)));
            if (outcome.DecidesFactions)
                LogFactions(kingdomsBefore);

            // Board events after the record (the board is already final).
            if (outcome.CapturedPiece != null)
            {
                PieceCaptured?.Invoke(outcome.CapturedPiece);
                PieceRemoved?.Invoke(outcome.CapturedPiece);
                PieceMoved?.Invoke(piece, outcome.ToX, outcome.ToY);
            }
            else if (outcome.MoverDied)
            {
                PieceCaptured?.Invoke(piece);
                PieceRemoved?.Invoke(piece);
            }

            MoveRecorded?.Invoke(LastMove);

            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }

            UpdateHangingPieces();
            var end = ActionResolver.EvaluateEnd(Board, mover, out _);
            if (end != null)
            {
                EndGame(end);
                return;
            }
            SwitchTurn();
        }

        /// <summary>
        /// Reports each tactical event to the game log (and the debug log) and raises
        /// <see cref="TacticalEvents"/> if any.
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
                CoreLog.Log(line, CoreLogLevel.Debug);
                Logged?.Invoke(new GameLogEvent.TacticDetected(e));
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
            var kingdomsBefore = _kingdomsBefore[last];
            var changes = _stateChanges[last];

            if (changes != null)
            {
                UndoStateChanges(record, changes);
            }
            else
            {
                var piece = Board.GetPiece(record.ToX, record.ToY);
                // Taken before the board changes back (the side names follow the factions).
                var undoEntry = new GameLogEvent.MoveTakenBack(record, ColorOf(record.Side), LineStyleOf(record), piece.Type);

                Board.UnmakeMove(piece, record.FromX, record.FromY, record.ToX, record.ToY, captured);

                CoreLog.Log($"(Undo) {piece.Type} back to ({record.FromX},{record.FromY})", CoreLogLevel.Debug);
                Logged?.Invoke(undoEntry);

                PieceMoved?.Invoke(piece, record.FromX, record.FromY);
                if (captured != null)
                    PieceAdded?.Invoke(captured);
            }

            Board.RetreatTurn();
            _moves.RemoveAt(last);
            _capturedPieces.RemoveAt(last);
            _clocksBeforeMove.RemoveAt(last);
            _kingdomsBefore.RemoveAt(last);
            _stateChanges.RemoveAt(last);
            if (kingdomsBefore != null)
                Board.ThreeKingdoms.RestoreActionState(kingdomsBefore);
            LastMove = _moves.Count > 0 ? _moves[_moves.Count - 1] : null;
            HasUnsavedChanges = true;

            // Reopen an ended game.
            IsGameOver = false;
            Winner = PlayerSide.None;
            Result = null;

            var mover = record.Side;
            // Unknown clocks (a move replayed from a saved game): keep both totals, new step.
            var players = AllPlayers;
            var restored = clocks ?? players.Select(p => new ClockState(TimeSpan.Zero, p.Timer.CurrentTotalTime)).ToArray();
            for (int i = 0; i < players.Length; i++)
                players[i].Timer.RestoreClockState(restored[i], active: mover == players[i].Side, paused: IsPaused);

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
            // Taken before the board changes back (the side names follow the factions).
            var undoEntry = new GameLogEvent.MoveTakenBack(record, ColorOf(record.Side),
                record.Notation != null ? MoveLineStyle.Notation
                : Board.ThreeKingdoms != null ? MoveLineStyle.ThreeKingdoms
                : MoveLineStyle.DarkChess, record.Piece.Type);

            var wasOnBoard = new Dictionary<Piece, (bool onBoard, int x, int y)>();
            foreach (var (p, _) in changes)
                wasOnBoard[p] = (Board.GetPiece(p.X, p.Y) == p, p.X, p.Y);

            Board.RevertStates(changes);

            CoreLog.Log($"(Undo) {record.Kind} at ({record.ToX},{record.ToY}) taken back", CoreLogLevel.Debug);
            Logged?.Invoke(undoEntry);

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

        /// <summary>The game-log line style of an ordinary move: its notation when it has one, else the dark-chess line on a dark-chess board.</summary>
        private MoveLineStyle LineStyleOf(MoveRecord move) =>
            move.Notation != null ? MoveLineStyle.Notation
            : Board.ThreeKingdoms != null ? MoveLineStyle.ThreeKingdoms
            : Board.UsesDarkChessRules ? MoveLineStyle.DarkChess
            : MoveLineStyle.Plain;

        private static PlayerSide OpponentOf(PlayerSide side) =>
            side == PlayerSide.Player1 ? PlayerSide.Player2 : PlayerSide.Player1;

        /// <summary>
        /// <paramref name="side"/> resigns and loses immediately (also allowed while
        /// paused). Returns false if the game is already over or the side is not one of
        /// the two players.
        /// </summary>
        public bool Resign(PlayerSide side)
        {
            if (Board.ThreeKingdoms != null)
                return Forfeit(side, timeUp: false);
            if (IsGameOver || (side != PlayerSide.Player1 && side != PlayerSide.Player2))
                return false;

            // Leave the pause state first so the ended game is not also "paused".
            if (IsPaused)
                ResumeGame();

            EndGame(OpponentOf(side), side, GameOverReason.Resign);
            return true;
        }

        /// <summary>
        /// 三國's resignation (棄權, author decision 8): <paramref name="side"/>'s pieces stay on the
        /// board (they may still be captured and scored) and its turns are skipped from now on;
        /// the game ends once fewer than two players still play. A time-up counts as one
        /// (<paramref name="timeUp"/>). Not a move: undo does not take it back.
        /// </summary>
        /// <returns>False when the game is over, <paramref name="side"/> is not a player, or it already resigned.</returns>
        private bool Forfeit(PlayerSide side, bool timeUp)
        {
            if (IsGameOver || side is not (PlayerSide.Player1 or PlayerSide.Player2 or PlayerSide.Player3))
                return false;
            var state = Board.ThreeKingdoms;
            int i = ThreeKingdomsState.Index(side);
            if (state.Resigned[i])
                return false;

            if (IsPaused)
                ResumeGame();
            state.Resigned[i] = true;
            PlayerOf(side).Timer.End();
            HasUnsavedChanges = true;
            CoreLog.Log($"(Forfeit) {side} forfeits{(timeUp ? " (time up)" : "")}", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.PlayerForfeited(side, timeUp));

            var end = ActionResolver.EvaluateEnd(Board, CurrentTurn, out _);
            if (end != null)
            {
                EndGame(end);
                return true;
            }
            if (side == CurrentTurn)
            {
                if (_selectedPiece != null)
                {
                    PieceUnselected?.Invoke(_selectedPiece);
                    _selectedPiece = null;
                }
                SwitchTurn();
            }
            return true;
        }

        /// <summary>
        /// Hands the turn to the next player (<see cref="ActionResolver.NextToMove"/>: the opponent;
        /// 三國 skips the players out, resigned or without an action, each reported to the log).
        /// </summary>
        private void SwitchTurn()
        {
            var mover = CurrentTurn;
            var next = ActionResolver.NextToMove(Board, mover);
            if (Board.ThreeKingdoms != null)
            {
                // Everyone between the mover and the next player is skipped (decisions 8 and 9).
                var side = mover;
                for (int step = 0; step < PlayerCount; step++)
                {
                    side = ThreeKingdomsState.SideOf(ThreeKingdomsState.Index(side) % 3 + 1);
                    if (side == next)
                        break;
                    if (side != mover && ThreeKingdomsStandings.IsPlaying(Board, side))
                    {
                        CoreLog.Log($"(Turn) {side} has no action, skipped", CoreLogLevel.Debug);
                        Logged?.Invoke(new GameLogEvent.TurnSkipped(side));
                    }
                }
            }
            PlayerOf(mover).Timer.EndStep();
            PlayerOf(next).Timer.StartStep();
            CurrentTurn = next;
        }

        /// <summary>
        /// Both clocks back to their start values and the side to move's step clock started,
        /// keeping the moves and the game state (unlike <see cref="ResetTimers"/>). Used after
        /// an opening line has been played onto the board.
        /// </summary>
        private void RestartClocks()
        {
            foreach (var p in AllPlayers)
                p.Timer.Reset();
            PlayerOf(CurrentTurn).Timer.StartStep();
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
            foreach (var p in AllPlayers)
                p.Timer.Reset();
            IsPaused = false;
            IsGameOver = false;
            Winner = PlayerSide.None;
            Result = null;
            IsInCheck = false;
            LastMove = null;
            _moves.Clear();
            _capturedPieces.Clear();
            _clocksBeforeMove.Clear();
            _kingdomsBefore.Clear();
            _stateChanges.Clear();
            UndoFloor = 0;
            HasUnsavedChanges = false;
            if (startFirstTurn)
                PlayerOf(CurrentTurn).Timer.StartStep();
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
                CoreLog.Log($"(Timer) {loser.Side} ran out of time (EndGameWhenTimesUp is off)", CoreLogLevel.Debug);
                Logged?.Invoke(new GameLogEvent.TimeRanOut(loser.Side));
                return;
            }

            // 三國: running out of time is a 棄權 (the other two play on).
            if (Board.ThreeKingdoms != null)
            {
                Logged?.Invoke(new GameLogEvent.TimeRanOut(loser.Side));
                Forfeit(loser.Side, timeUp: true);
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
        private void EndGame(GameEnd end) => EndGame(end.Winner, end.Loser, end.Reason, end.Ranking);

        private void EndGame(PlayerSide winner, PlayerSide loser, GameOverReason reason, IReadOnlyList<PlayerSide> ranking = null)
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
            Result = new GameOverInfo(winner, loser, reason, finalBoard, LastMove, checking, ranking,
                Board.ThreeKingdoms != null ? (int[])Board.ThreeKingdoms.Scores.Clone() : null);

            foreach (var p in AllPlayers)
                p.Timer.End();

            if (_selectedPiece != null)
            {
                PieceUnselected?.Invoke(_selectedPiece);
                _selectedPiece = null;
            }

            CoreLog.Log($"(Game over) {winner} wins ({reason})", CoreLogLevel.Debug);
            Logged?.Invoke(new GameLogEvent.GameEnded(winner, reason));
            GameOver?.Invoke(Result);
        }

        /// <summary>
        /// Advances both players' clocks; call once per frame. Only the side whose
        /// step is active actually accumulates time (see PlayerTimer.Update).
        /// </summary>
        public void UpdateTimers()
        {
            foreach (var p in AllPlayers)
                p.Timer.Update();
        }

        /// <summary>
        /// Pauses the game: stops the running clock (PlayerTimer.Pause only affects the
        /// side whose step is active) and ignores board input until resumed.
        /// </summary>
        public void PauseGame()
        {
            if (IsPaused || IsGameOver)
                return;

            foreach (var p in AllPlayers)
                p.Timer.Pause();
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

            foreach (var p in AllPlayers)
                p.Timer.Resume();
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
