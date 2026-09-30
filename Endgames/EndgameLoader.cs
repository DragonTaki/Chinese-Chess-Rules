/* ----- ----- ----- ----- */
// EndgameLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/09/30
// Update Date: 2026/09/30
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Engine.Logging;

namespace Chinese_Chess_v3.Game.Core.Endgames
{
    /// <summary>
    /// Loads endgame puzzle files (<c>*.pgn</c>, see <see cref="EndgamePgn"/>) from folders.
    /// The folder paths are passed in by the caller (the built-in folder next to the game,
    /// the player's own folder in app data); Core hard-codes no location.
    /// <para>
    /// A file directly in the root has no folder category; a file in a subfolder gets the
    /// name of the subfolder directly under the root as its folder category (deeper folders
    /// count as that same category). A <c>[Category]</c> tag overrides the folder category.
    /// </para>
    /// <para>
    /// Never throws for a bad file: it is skipped with a warning (log and
    /// <c>warnings</c>). A file whose position is fine but whose solution does not play
    /// through is kept with an empty solution, also with a warning.
    /// </para>
    /// </summary>
    public static class EndgameLoader
    {
        public const string FilePattern = "*.pgn";

        /// <summary>
        /// Loads every puzzle under <paramref name="rootFolder"/>, sorted by category, then
        /// by file name (both ordinal).
        /// </summary>
        /// <param name="createIfMissing">Create the folder when it does not exist (for the player's folder).</param>
        /// <param name="warnings">Receives one line per skipped file or dropped solution (optional).</param>
        public static List<EndgamePuzzle> LoadFolder(string rootFolder, EndgameOrigin origin, bool createIfMissing = false, List<string> warnings = null)
        {
            var result = new List<EndgamePuzzle>();
            if (string.IsNullOrWhiteSpace(rootFolder))
                return result;

            IEnumerable<string> files;
            try
            {
                if (!Directory.Exists(rootFolder))
                {
                    if (!createIfMissing)
                        return result;
                    Directory.CreateDirectory(rootFolder);
                }
                files = Directory.EnumerateFiles(rootFolder, FilePattern, SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Warn(warnings, $"cannot read endgame folder '{rootFolder}': {ex.Message}");
                return result;
            }

            foreach (var path in files)
            {
                var puzzle = LoadFile(path, origin, GetFolderCategory(rootFolder, path), warnings);
                if (puzzle != null)
                    result.Add(puzzle);
            }

            result.Sort(Compare);
            return result;
        }

        /// <summary>
        /// Loads the built-in and the player's folder: built-in puzzles first, each part
        /// sorted as in <see cref="LoadFolder"/>.
        /// </summary>
        public static List<EndgamePuzzle> LoadAll(string builtInFolder, string userFolder, List<string> warnings = null)
        {
            var result = LoadFolder(builtInFolder, EndgameOrigin.BuiltIn, createIfMissing: false, warnings);
            result.AddRange(LoadFolder(userFolder, EndgameOrigin.User, createIfMissing: true, warnings));
            return result;
        }

        /// <summary>
        /// Loads one file; null (with a warning) when it cannot be read or parsed.
        /// </summary>
        public static EndgamePuzzle LoadFile(string path, EndgameOrigin origin, string folderCategory = "", List<string> warnings = null)
        {
            string fileName = Path.GetFileName(path);
            EndgamePuzzle puzzle;
            try
            {
                string text = File.ReadAllText(path);
                puzzle = EndgamePgn.Parse(text, fileName, origin, folderCategory, path);
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Warn(warnings, $"skipped endgame file '{path}': {ex.Message}");
                return null;
            }

            string solutionError = EndgamePgn.CheckSolution(puzzle);
            if (solutionError != null)
            {
                Warn(warnings, $"endgame file '{path}': {solutionError}; solution dropped");
                puzzle = puzzle with { Solution = new List<IccsMove>() };
            }
            return puzzle;
        }

        /// <summary>The name of the subfolder directly under the root containing <paramref name="filePath"/>; empty for the root itself.</summary>
        private static string GetFolderCategory(string rootFolder, string filePath)
        {
            string relative = Path.GetRelativePath(rootFolder, Path.GetDirectoryName(filePath) ?? rootFolder);
            if (relative == "." || relative.StartsWith("..", StringComparison.Ordinal))
                return string.Empty;
            int separator = relative.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return separator < 0 ? relative : relative.Substring(0, separator);
        }

        private static int Compare(EndgamePuzzle a, EndgamePuzzle b)
        {
            int c = string.CompareOrdinal(a.Category, b.Category);
            if (c != 0)
                return c;
            c = string.CompareOrdinal(a.FileName, b.FileName);
            return c != 0 ? c : string.CompareOrdinal(a.FilePath, b.FilePath);
        }

        private static void Warn(List<string> warnings, string message)
        {
            warnings?.Add(message);
            AppLogger.Log($"(Endgame) {message}", LogLevel.WARN);
        }
    }
}
