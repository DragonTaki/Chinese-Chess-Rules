/* ----- ----- ----- ----- */
// PgnFolderLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/01
// Update Date: 2026/10/01
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Engine.Logging;

namespace Chinese_Chess_v3.Game.Core.Pgn
{
    /// <summary>
    /// What to do with a file whose position is fine but whose moves do not play through.
    /// </summary>
    public enum InvalidMovesPolicy
    {
        /// <summary>Keep the file with no moves (an endgame puzzle is still playable without its solution).</summary>
        DropMoves,

        /// <summary>Skip the file (an opening is nothing without its line).</summary>
        SkipFile,
    }

    /// <summary>
    /// Loads one kind of PGN game file (<c>*.pgn</c>) from folders: the shared part of
    /// <c>EndgameLoader</c> and <c>OpeningLoader</c>, which pass in their parser, the
    /// policy for moves that do not play through and the log label. The folder paths come
    /// from the caller; Core hard-codes no location.
    /// <para>
    /// A file directly in the root has no folder category; a file in a subfolder gets the
    /// name of the subfolder directly under the root as its folder category (deeper folders
    /// count as that same category). A <c>[Category]</c> tag overrides the folder category
    /// (applied by the parser).
    /// </para>
    /// <para>
    /// Never throws for a bad file: it is skipped with a warning (log and
    /// <c>warnings</c>).
    /// </para>
    /// </summary>
    /// <typeparam name="T">The kind of file.</typeparam>
    public sealed class PgnFolderLoader<T> where T : PgnGameFile
    {
        public const string FilePattern = "*.pgn";

        /// <summary>Parses one file's text: (text, file name, origin, folder category, path).</summary>
        public delegate T Parser(string text, string fileName, PgnOrigin origin, string folderCategory, string filePath);

        private readonly Parser _parse;
        private readonly InvalidMovesPolicy _invalidMoves;
        private readonly string _label;
        private readonly string _what;

        /// <param name="parse">The kind's parser; throws <see cref="FormatException"/> for a bad file.</param>
        /// <param name="invalidMoves">What to do when the moves do not play through.</param>
        /// <param name="label">Log prefix without parentheses, e.g. <c>Endgame</c>.</param>
        /// <param name="what">Name of the kind in warnings, e.g. <c>endgame</c>.</param>
        public PgnFolderLoader(Parser parse, InvalidMovesPolicy invalidMoves, string label, string what)
        {
            _parse = parse ?? throw new ArgumentNullException(nameof(parse));
            _invalidMoves = invalidMoves;
            _label = label;
            _what = what;
        }

        /// <summary>
        /// Loads every file under <paramref name="rootFolder"/>, sorted by category, then by
        /// file name (both ordinal).
        /// </summary>
        /// <param name="createIfMissing">Create the folder when it does not exist (for the player's folder).</param>
        /// <param name="warnings">Receives one line per skipped file or dropped move list (optional).</param>
        public List<T> LoadFolder(string rootFolder, PgnOrigin origin, bool createIfMissing = false, List<string> warnings = null)
        {
            var result = new List<T>();
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
                Warn(warnings, $"cannot read {_what} folder '{rootFolder}': {ex.Message}");
                return result;
            }

            foreach (var path in files)
            {
                var item = LoadFile(path, origin, GetFolderCategory(rootFolder, path), warnings);
                if (item != null)
                    result.Add(item);
            }

            result.Sort(Compare);
            return result;
        }

        /// <summary>
        /// Loads the built-in and the player's folder (created when missing): built-in files
        /// first, each part sorted as in <see cref="LoadFolder"/>.
        /// </summary>
        public List<T> LoadAll(string builtInFolder, string userFolder, List<string> warnings = null)
        {
            var result = LoadFolder(builtInFolder, PgnOrigin.BuiltIn, createIfMissing: false, warnings);
            result.AddRange(LoadFolder(userFolder, PgnOrigin.User, createIfMissing: true, warnings));
            return result;
        }

        /// <summary>
        /// Loads one file; null (with a warning) when it cannot be read or parsed, or when its
        /// moves do not play through and the policy is <see cref="InvalidMovesPolicy.SkipFile"/>.
        /// </summary>
        public T LoadFile(string path, PgnOrigin origin, string folderCategory = "", List<string> warnings = null)
        {
            string fileName = Path.GetFileName(path);
            T item;
            try
            {
                string text = File.ReadAllText(path);
                item = _parse(text, fileName, origin, folderCategory, path);
            }
            catch (Exception ex) when (ex is FormatException || ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Warn(warnings, $"skipped {_what} file '{path}': {ex.Message}");
                return null;
            }

            string movesError = PgnReader.CheckMoves(item.Fen, item.Moves, item.RulesForMoveCheck());
            if (movesError == null)
                return item;

            if (_invalidMoves == InvalidMovesPolicy.SkipFile)
            {
                Warn(warnings, $"skipped {_what} file '{path}': {movesError}");
                return null;
            }
            Warn(warnings, $"{_what} file '{path}': {movesError}; moves dropped");
            return (T)((PgnGameFile)item with { Moves = new List<IccsMove>() });
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

        private static int Compare(T a, T b)
        {
            int c = string.CompareOrdinal(a.Category, b.Category);
            if (c != 0)
                return c;
            c = string.CompareOrdinal(a.FileName, b.FileName);
            return c != 0 ? c : string.CompareOrdinal(a.FilePath, b.FilePath);
        }

        private void Warn(List<string> warnings, string message)
        {
            warnings?.Add(message);
            AppLogger.Log($"({_label}) {message}", LogLevel.WARN);
        }
    }
}
