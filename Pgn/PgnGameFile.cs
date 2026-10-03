/* ----- ----- ----- ----- */
// PgnGameFile.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/02
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core.Pgn
{
    /// <summary>Where a PGN file (endgame puzzle, opening) was loaded from.</summary>
    public enum PgnOrigin
    {
        /// <summary>Shipped with the game (a built-in folder under <c>Assets/</c>).</summary>
        BuiltIn,

        /// <summary>The player's own folder (per-user app data, or the folder set in the settings).</summary>
        User,
    }

    /// <summary>
    /// What every game file loaded from a PGN file has in common: where it came from, its category and title, the start position
    /// and the main-line moves. The kinds of file (<c>EndgamePuzzle</c>, <c>OpeningLine</c>)
    /// derive from it and add their own tags. Plain, immutable data: the original file
    /// content only. A record so a changed copy can be made with <c>with</c>.
    /// </summary>
    public abstract record PgnGameFile
    {
        /// <summary>The 4-digit number of the file name (<c>0001</c> of <c>0001-七星聚會.pgn</c>).</summary>
        public string Id { get; init; }

        /// <summary>The file name (<c>0001-七星聚會.pgn</c>); files are sorted by it (ordinal).</summary>
        public string FileName { get; init; }

        /// <summary>Full path of the file it was loaded from (null when parsed from text only).</summary>
        public string FilePath { get; init; }

        /// <summary>Built-in or the player's own folder.</summary>
        public PgnOrigin Origin { get; init; }

        /// <summary>
        /// The category: the <c>[Category]</c> tag, else the name of the subfolder (directly
        /// under the loaded root) the file is in; empty when neither exists.
        /// </summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>The <c>[Title]</c> tag, else the name part of the file name (<c>七星聚會</c>).</summary>
        public string Title { get; init; }

        /// <summary>The start position, including the side to move (the <c>[FEN]</c> tag, or the kind's default).</summary>
        public string Fen { get; init; }

        /// <summary>
        /// The colour to move in <see cref="Fen"/>: <see cref="PlayerSide.Player1"/>'s colour
        /// (the first mover is always Player1; the other colour is Player2's).
        /// </summary>
        public PieceColor FirstColor { get; init; } = PieceColor.Red;

        /// <summary>
        /// The main-line moves of the movetext, both sides' moves in order starting with
        /// Player1 (<see cref="FirstColor"/>); empty when the file has none.
        /// </summary>
        public IReadOnlyList<IccsMove> Moves { get; init; } = new List<IccsMove>();

        /// <summary>The <c>[Description]</c> tag (optional).</summary>
        public string Description { get; init; }

        /// <summary>The <c>[Source]</c> tag (optional): where the file's content comes from (出處).</summary>
        public string Source { get; init; }

        /// <summary>Every tag pair of the file as written, including ones not mapped to a property.</summary>
        public IReadOnlyDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();

        /// <summary>The side to move after <see cref="Moves"/> have been played from <see cref="Fen"/> (Player1 moves first).</summary>
        public PlayerSide SideToMoveAfterMoves =>
            Moves.Count % 2 == 0 ? PlayerSide.Player1 : PlayerSide.Player2;

        /// <summary>The colour <paramref name="side"/> plays in this file's game (Player1 <see cref="FirstColor"/>, Player2 the other); None for any other side.</summary>
        public PieceColor ColorOf(PlayerSide side) => side switch
        {
            PlayerSide.Player1 => FirstColor,
            PlayerSide.Player2 => PieceColors.Opposite(FirstColor),
            _ => PieceColor.None,
        };

        /// <summary>
        /// The rules the file's <see cref="Moves"/> are checked with when it is loaded
        /// (<see cref="PgnReader.CheckMoves"/>); null = the default <see cref="Rules"/>. A saved
        /// game overrides it with the rules it was played by.
        /// </summary>
        internal virtual Rules RulesForMoveCheck() => null;

        /// <summary>No file content; for <c>with</c> copies and object initializers.</summary>
        protected PgnGameFile() { }

        /// <summary>
        /// The common fields from a read file: <paramref name="content"/> (file name, tags,
        /// moves) and the start position the kind decided on, with its colour to move.
        /// </summary>
        protected PgnGameFile(PgnFileContent content, string fen, PieceColor firstColor)
        {
            Id = content.Id;
            FileName = content.FileName;
            FilePath = content.FilePath;
            Origin = content.Origin;
            Category = content.Optional("Category") ?? (content.FolderCategory ?? string.Empty);
            Title = content.Optional("Title") ?? content.NameFromFile;
            Fen = fen;
            FirstColor = firstColor;
            Moves = content.Moves;
            Description = content.Optional("Description");
            Source = content.Optional("Source");
            Tags = content.Tags;
        }

        // Sealed: the derived records would otherwise each print every member.
        public sealed override string ToString() => $"{FileName} ({Origin}, {Category}): {Title}";
    }
}
