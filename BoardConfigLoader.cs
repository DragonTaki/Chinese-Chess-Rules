/* ----- ----- ----- ----- */
// BoardConfigLoader.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/21
// Update Date: 2025/10/31
// Version: v1.1
/* ----- ----- ----- ----- */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core
{
    public static class BoardConfigLoader
    {
        /// <summary>
        /// Generates a complete chessboard configuration.
        /// Priority:
        /// 1. parameterPieces (if provided)
        /// 2. configFilePath (if provided and valid)
        /// 3. PieceConstants.InitialPieces (default setup)
        /// </summary>
        /// <param name="parameterPieces">Optional pre-defined chess pieces list.</param>
        /// <param name="configFilePath">Optional JSON configuration file path.</param>
        /// <returns>A list of PieceInfo representing the chessboard state.</returns>
#nullable enable
        public static List<PieceInfo> Load(
            List<PieceInfo>? overridePieces = null,
            string? configFilePath = null)
#nullable disable
        {
            // 1. 若有程式參數提供的棋子，直接使用
            if (overridePieces?.Count > 0)
            {
                return DeepCopyPieces(overridePieces);
            }

            // 2. 嘗試讀取檔案
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

            // 3. 回傳預設棋盤
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
