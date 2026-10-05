/* ----- ----- ----- ----- */
// GameLogEvent.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/05
// Update Date: 2026/10/05
// Version: v1.0
/* ----- ----- ----- ----- */

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{
    /// <summary>
    /// Something <see cref="GameManager"/> reports for the game log (<see cref="GameManager.Logged"/>),
    /// as plain data: the logic layer above composes the player-facing line from it, so the rules
    /// layer never writes a sentence itself. Each case carries what its line needs as it was when
    /// the event was raised (e.g. a side's colour before an undo changes it back).
    /// </summary>
    public abstract record GameLogEvent
    {
        private GameLogEvent() { }

        // ----- Game setup -----

        /// <summary>A shuffled HalfCenter game was dealt (暗棋半盤 when <paramref name="IsHiddenChess"/>, otherwise 明棋半盤).</summary>
        public sealed record HalfCenterStarted(bool IsHiddenChess) : GameLogEvent;

        /// <summary>An endgame puzzle was set up (<see cref="GameManager.StartEndgame"/>, or its restart).</summary>
        public sealed record EndgameStarted(string Title, string Goal) : GameLogEvent;

        /// <summary>An opening's position was set up, before its line is played; <paramref name="Ecco"/> may be null.</summary>
        public sealed record OpeningStarted(string Title, string Ecco) : GameLogEvent;

        /// <summary>A saved game's position was set up, before its moves are replayed (loaded, or restarted when <paramref name="IsRestart"/>).</summary>
        public sealed record SavedGameStarted(string Title, bool IsRestart) : GameLogEvent;

        /// <summary>The game was written to a file (<paramref name="FileName"/> without its folder).</summary>
        public sealed record GameSaved(string FileName) : GameLogEvent;

        // ----- Board input (debug-style lines) -----

        /// <summary>
        /// A board click reached <see cref="GameManager.HandleClick"/>: the side to move, the
        /// selected piece's type (null: nothing selected), the square and what stands on it
        /// (null: empty; <paramref name="ClickedFaceDown"/>: a face-down dark-chess piece, whose
        /// type must not be shown).
        /// </summary>
        public sealed record BoardClicked(PlayerSide Turn, PieceType? Held, int X, int Y, PieceType? Clicked, bool ClickedFaceDown) : GameLogEvent;

        /// <summary>A click changed the selection (see <see cref="SelectionChange"/>); <paramref name="Type"/> is the piece it names.</summary>
        public sealed record SelectionChanged(SelectionChange Change, PieceType Type, int X, int Y) : GameLogEvent;

        /// <summary>An ordinary move took the piece of <paramref name="Type"/> on (X, Y).</summary>
        public sealed record PieceTaken(PieceType Type, int X, int Y) : GameLogEvent;

        /// <summary>An ordinary move put the piece of <paramref name="Type"/> on (X, Y).</summary>
        public sealed record PieceMoved(PieceType Type, int X, int Y) : GameLogEvent;

        // ----- Moves -----

        /// <summary>
        /// A move or dark-chess action was recorded, with the style of its line
        /// (<see cref="MoveLineStyle"/>; <see cref="MoveLineStyle.Plain"/>: no line) and the
        /// mover's colour as decided right after the action (<paramref name="MoverColor"/>).
        /// </summary>
        public sealed record MovePlayed(MoveRecord Move, PieceColor MoverColor, MoveLineStyle Style) : GameLogEvent;

        /// <summary>The first action decided the factions: the colours Player1 and Player2 now play.</summary>
        public sealed record FactionsDecided(PieceColor Player1Color, PieceColor Player2Color) : GameLogEvent;

        /// <summary>A move put <paramref name="Side"/> in check and the game goes on.</summary>
        public sealed record CheckGiven(PlayerSide Side) : GameLogEvent;

        /// <summary>One tactical event of the last move (raised once per event, before <see cref="GameManager.TacticalEvents"/>).</summary>
        public sealed record TacticDetected(TacticalEvent Event) : GameLogEvent;

        /// <summary>
        /// A move was taken back. Its line is like <see cref="MovePlayed"/>'s (the mover's colour
        /// as it was before the board changed back); <see cref="MoveLineStyle.Plain"/> names the
        /// piece's type (<paramref name="PieceType"/>) and its from-square.
        /// </summary>
        public sealed record MoveTakenBack(MoveRecord Move, PieceColor MoverColor, MoveLineStyle Style, PieceType PieceType) : GameLogEvent;

        // ----- Clocks and result -----

        /// <summary><paramref name="Side"/>'s clock ran out and the game goes on (判負 off: overtime).</summary>
        public sealed record TimeRanOut(PlayerSide Side) : GameLogEvent;

        /// <summary>The game ended.</summary>
        public sealed record GameEnded(PlayerSide Winner, GameOverReason Reason) : GameLogEvent;
    }

    /// <summary>Which line describes a move in the game log (<see cref="GameLogEvent.MovePlayed"/>, <see cref="GameLogEvent.MoveTakenBack"/>).</summary>
    public enum MoveLineStyle
    {
        /// <summary>Neither of the others: no line for a move played; the piece and its from-square for one taken back.</summary>
        Plain,

        /// <summary>By the move's Chinese notation (<see cref="MoveRecord.Notation"/>, Full board).</summary>
        Notation,

        /// <summary>The dark-chess line (flips, hidden captures, 自殺, moves on a dark-chess board).</summary>
        DarkChess,
    }

    /// <summary>How a click changed the selection (<see cref="GameLogEvent.SelectionChanged"/>).</summary>
    public enum SelectionChange
    {
        /// <summary>A piece was selected (on the clicked square).</summary>
        Selected,

        /// <summary>The selected piece was clicked again, or an empty square that is not a move: the selection is dropped.</summary>
        Unselected,

        /// <summary>Another own piece was clicked: the selection moves to it.</summary>
        Switched,

        /// <summary>A piece the selected one cannot move onto was clicked: the selection is dropped.</summary>
        Invalid,
    }
}
