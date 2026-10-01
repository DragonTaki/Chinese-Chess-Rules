/* ----- ----- ----- ----- */
// OpeningLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pgn;

namespace Chinese_Chess_v3.Game.Core.Openings
{
    /// <summary>
    /// Loads opening files (<c>*.pgn</c>, see <see cref="OpeningPgn"/>) from folders:
    /// <see cref="PgnFolderLoader{T}"/> with the opening parser - the same folder categories,
    /// sorting and bad-file skipping as endgames. An opening whose line does not play
    /// through is skipped (the line is the whole point of the file), with a warning.
    /// </summary>
    public static class OpeningLoader
    {
        public const string FilePattern = PgnFolderLoader<OpeningLine>.FilePattern;

        private static readonly PgnFolderLoader<OpeningLine> Loader =
            new(OpeningPgn.Parse, InvalidMovesPolicy.SkipFile, "Opening", "opening");

        /// <summary>
        /// Loads every opening under <paramref name="rootFolder"/>, sorted by category, then
        /// by file name (both ordinal).
        /// </summary>
        /// <param name="createIfMissing">Create the folder when it does not exist (for the player's folder).</param>
        /// <param name="warnings">Receives one line per skipped file (optional).</param>
        public static List<OpeningLine> LoadFolder(string rootFolder, PgnOrigin origin, bool createIfMissing = false, List<string> warnings = null) =>
            Loader.LoadFolder(rootFolder, origin, createIfMissing, warnings);

        /// <summary>
        /// Loads the built-in and the player's folder: built-in openings first, each part
        /// sorted as in <see cref="LoadFolder"/>.
        /// </summary>
        public static List<OpeningLine> LoadAll(string builtInFolder, string userFolder, List<string> warnings = null) =>
            Loader.LoadAll(builtInFolder, userFolder, warnings);

        /// <summary>
        /// Loads one file; null (with a warning) when it cannot be read or parsed, or its
        /// line does not play through.
        /// </summary>
        public static OpeningLine LoadFile(string path, PgnOrigin origin, string folderCategory = "", List<string> warnings = null) =>
            Loader.LoadFile(path, origin, folderCategory, warnings);
    }
}
