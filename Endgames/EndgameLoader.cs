/* ----- ----- ----- ----- */
// EndgameLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/10/01
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pgn;

namespace Chinese_Chess_v3.Game.Core.Endgames
{
    /// <summary>
    /// Loads endgame puzzle files (<c>*.pgn</c>, see <see cref="EndgamePgn"/>) from folders:
    /// <see cref="PgnFolderLoader{T}"/> with the endgame parser. Folder categories, sorting
    /// and skipping bad files are described there. A puzzle whose position is fine but whose
    /// solution does not play through is kept with an empty solution, with a warning.
    /// </summary>
    public static class EndgameLoader
    {
        public const string FilePattern = PgnFolderLoader<EndgamePuzzle>.FilePattern;

        private static readonly PgnFolderLoader<EndgamePuzzle> Loader =
            new(EndgamePgn.Parse, InvalidMovesPolicy.DropMoves, "Endgame", "endgame");

        /// <summary>
        /// Loads every puzzle under <paramref name="rootFolder"/>, sorted by category, then
        /// by file name (both ordinal).
        /// </summary>
        /// <param name="createIfMissing">Create the folder when it does not exist (for the player's folder).</param>
        /// <param name="warnings">Receives one line per skipped file or dropped solution (optional).</param>
        public static List<EndgamePuzzle> LoadFolder(string rootFolder, PgnOrigin origin, bool createIfMissing = false, List<string> warnings = null) =>
            Loader.LoadFolder(rootFolder, origin, createIfMissing, warnings);

        /// <summary>
        /// Loads the built-in and the player's folder: built-in puzzles first, each part
        /// sorted as in <see cref="LoadFolder"/>.
        /// </summary>
        public static List<EndgamePuzzle> LoadAll(string builtInFolder, string userFolder, List<string> warnings = null) =>
            Loader.LoadAll(builtInFolder, userFolder, warnings);

        /// <summary>
        /// Loads one file; null (with a warning) when it cannot be read or parsed.
        /// </summary>
        public static EndgamePuzzle LoadFile(string path, PgnOrigin origin, string folderCategory = "", List<string> warnings = null) =>
            Loader.LoadFile(path, origin, folderCategory, warnings);
    }
}
