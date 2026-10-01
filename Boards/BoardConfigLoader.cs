/* ----- ----- ----- ----- */
// BoardConfigLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/21
// Update Date: 2026/10/01
// Version: v1.2
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

using Engine.Randomization;

namespace Chinese_Chess_v3.Game.Core.Boards
{
    public static class BoardConfigLoader
    {
        /// <summary>
        /// Generates a complete chessboard configuration.
        /// Priority:
        /// 1. overridePieces (if provided and not empty; copied)
        /// 2. configFilePath (if provided and valid)
        /// 3. PieceConstants.InitialClassicPieces (default setup; copied)
        /// </summary>
        /// <param name="overridePieces">Optional pre-defined chess pieces list.</param>
        /// <param name="configFilePath">Optional JSON configuration file path.</param>
        /// <returns>A list of PieceInfo representing the chessboard state.</returns>
#nullable enable
        public static List<PieceInfo> Load(
            List<PieceInfo>? overridePieces = null,
            string? configFilePath = null)
#nullable disable
        {
            // 1. Pieces supplied by the caller are used as they are
            if (overridePieces?.Count > 0)
            {
                return DeepCopyPieces(overridePieces);
            }

            // 2. Try to read the config file
            if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
            {
                try
                {
                    string json = File.ReadAllText(configFilePath);
                    var pieces = JsonSerializer.Deserialize<List<PieceInfo>>(json);
                    if (pieces?.Count > 0)
                        return pieces;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Failed to read config file: {ex.Message}");
                }
            }

            // 3. Fall back to the default board
            return DeepCopyPieces(PieceConstants.InitialClassicPieces);
        }

        /// <summary>
        /// A fresh HalfCenter (台灣暗棋半盤, 8×4) start position: both colours'
        /// <see cref="PieceConstants.HalfCenterPieceSet"/> (32 pieces) shuffled over all 32
        /// squares (Fisher–Yates with <paramref name="random"/>).
        /// </summary>
        /// <param name="random">The random source; the game passes <see cref="GlobalRandom.Instance"/>.</param>
        /// <param name="faceDown">
        /// true (暗棋, <c>Rules.IsHiddenChess</c>): every piece face down and owned by nobody
        /// (<c>PlayerSide.None</c>) — the first flip decides the factions. false (明棋半盤): every
        /// piece face up, red owned by Player1 and black by Player2 (no standard rule for
        /// this variant yet, see docs/DARK-CHESS-RULES.md §1.1).
        /// </param>
        /// <returns>32 pieces, one per square.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="random"/> is null.</exception>
        public static List<PieceInfo> CreateShuffledHalfCenter(IRandomProvider random, bool faceDown = true)
        {
            ArgumentNullException.ThrowIfNull(random);

            var squares = new List<(int x, int y)>();
            for (int y = 0; y < BoardConstants.HalfCenter.Rows; y++)
            {
                for (int x = 0; x < BoardConstants.HalfCenter.Columns; x++)
                    squares.Add((x, y));
            }

            // Fisher–Yates: every square order equally likely (as far as the source is).
            for (int i = squares.Count - 1; i > 0; i--)
            {
                int j = random.NextInt(i + 1);
                (squares[i], squares[j]) = (squares[j], squares[i]);
            }

            var pieces = new List<PieceInfo>(squares.Count);
            int next = 0;
            foreach (var color in new[] { PieceColor.Red, PieceColor.Black })
            {
                var side = !faceDown ? (color == PieceColor.Red ? PlayerSide.Player1 : PlayerSide.Player2) : PlayerSide.None;
                foreach (var (type, count) in PieceConstants.HalfCenterPieceSet)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var (x, y) = squares[next++];
                        pieces.Add(new PieceInfo(type, x, y, color, side, isFaceUp: !faceDown));
                    }
                }
            }
            return pieces;
        }

        /// <summary>
        /// Exports a board layout to JSON file.
        /// </summary>
        public static void Save(string filePath, List<PieceInfo> pieces)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(pieces, options);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Creates a deep copy of the piece list to prevent shared reference issues.
        /// </summary>
        private static List<PieceInfo> DeepCopyPieces(List<PieceInfo> source)
        {
            return source?.Select(p => p?.Clone()).ToList() ?? new List<PieceInfo>();
        }
    }
}
