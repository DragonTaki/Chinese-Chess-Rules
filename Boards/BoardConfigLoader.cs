/* ----- ----- ----- ----- */
// BoardConfigLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/21
// Update Date: 2026/10/06
// Version: v1.3
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;


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
