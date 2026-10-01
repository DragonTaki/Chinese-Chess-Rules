/* ----- ----- ----- ----- */
// SavedGameLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pgn;

namespace Chinese_Chess_v3.Game.Core.Saves
{
    /// <summary>
    /// Loads saved games (<c>*.pgn</c>, see <see cref="SavedGamePgn"/>) from the saves folder:
    /// <see cref="PgnFolderLoader{T}"/> with the saved-game parser. The subfolders directly
    /// under the root (the mode folders, <c>SystemSettings</c>) are the categories. A file
    /// whose moves do not play through is skipped, with a warning.
    /// </summary>
    public static class SavedGameLoader
    {
        public const string FilePattern = PgnFolderLoader<SavedGame>.FilePattern;

        private static readonly PgnFolderLoader<SavedGame> Loader =
            new(SavedGamePgn.Parse, InvalidMovesPolicy.SkipFile, "Save", "saved game");

        /// <summary>
        /// Loads every saved game under <paramref name="rootFolder"/> (origin
        /// <see cref="PgnOrigin.User"/>), sorted by category (mode folder), then by file name
        /// (both ordinal; within one mode and name that is oldest first).
        /// </summary>
        /// <param name="createIfMissing">Create the folder when it does not exist.</param>
        /// <param name="warnings">Receives one line per skipped file (optional).</param>
        public static List<SavedGame> LoadFolder(string rootFolder, bool createIfMissing = true, List<string> warnings = null) =>
            Loader.LoadFolder(rootFolder, PgnOrigin.User, createIfMissing, warnings);

        /// <summary>Loads one file; null (with a warning) when it cannot be read or parsed, or its moves do not play through.</summary>
        public static SavedGame LoadFile(string path, string folderCategory = "", List<string> warnings = null) =>
            Loader.LoadFile(path, PgnOrigin.User, folderCategory, warnings);
    }
}
