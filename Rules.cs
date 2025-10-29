/* ----- ----- ----- ----- */
// Rules.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/29
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;
using Chinese_Chess_v3.Game.Models;

namespace Chinese_Chess_v3.Game.Core
{

    /// <summary>
    /// Represents the rules configuration for different board types in Chinese Chess.
    /// Contains settings for Full board, Half board, and HalfCross (三國) variants.
    /// </summary>
    public class Rules
    {
        #region Full Board Rules (大盤規則設定)

        /// <summary>
        /// Whether the General can see the opposing General directly (王見王). Default: false
        /// </summary>
        public bool CanGeneralSeeGeneral { get; set; } = false;

        /// <summary>
        /// Whether the General can leave the palace (將帥出宮). Default: false
        /// </summary>
        public bool CanGeneralLeavePalace { get; set; } = false;

        /// <summary>
        /// Whether the Advisors can leave the palace (士出宮). Default: false
        /// </summary>
        public bool CanAdvisorLeavePalace { get; set; } = false;

        /// <summary>
        /// Whether the Elephant's eye can be blocked (卡象眼). Default: true
        /// </summary>
        public bool CanElephantEyeBlockd { get; set; } = true;

        /// <summary>
        /// Whether the Horse's leg can be hobbled (蹩馬腳). Default: true
        /// </summary>
        public bool CanHorseLegHobbled { get; set; } = true;

        /// <summary>
        /// Whether a piece can capture a friendly piece (吃己棋). Default: false
        /// </summary>
        public bool CanCaptureOwnPiece { get; set; } = false;

        /// <summary>
        /// Whether a piece can kill itself (單獨自殺). Default: false
        /// </summary>
        public bool CanSuiside { get; set; } = false;

        #endregion

        #region Half Board Rules (小盤規則設定)

        /// <summary>
        /// Whether the board uses hidden pieces (暗棋). Default: true
        /// </summary>
        public bool IsHiddenChess { get; set; } = true;

        /// <summary>
        /// Whether a piece can capture hidden pieces (暗吃). Default: false
        /// </summary>
        public bool CanCaptureHiddenPiece { get; set; } = false;

        /// <summary>
        /// Capturing a stronger hidden piece counts as self-kill (吃到比自己大的子自殺). Default: true
        /// </summary>
        public bool IsCaptureHiddenPieceStrongerSuiside { get; set; } = true;

        /// <summary>
        /// Whether multiple captures in a row are allowed (連吃). Default: false
        /// </summary>
        public bool IsAllowChainCapture { get; set; } = false;

        /// <summary>
        /// Whether Chariots can move multiple grids (車衝). Default: false
        /// </summary>
        public bool CanChariotRush { get; set; } = false;

        /// <summary>
        /// Whether Horses should move diagonally (馬斜). Default: false
        /// </summary>
        public bool IsHorseMoveDiagonally { get; set; } = false;

        /// <summary>
        /// Whether Cannons must jump over one piece to capture (包跳吃子). Default: true
        /// </summary>
        public bool IsCannonMustJumpToCapture { get; set; } = true;

        #endregion

        #region Rank Settings (小盤棋子大小)

        /// <summary>
        /// Piece ranking order for Half Board (from strongest to weakest)
        /// </summary>
        public PieceType[] PieceRankings { get; set; } = new PieceType[]
        {
            PieceType.General,
            PieceType.Advisor,
            PieceType.Elephant,
            PieceType.Chariot,
            PieceType.Horse,
            PieceType.Cannon,
            PieceType.Soldier,
        };

        #endregion

        #region HalfCross Board Team Setup (三國半盤隊伍)

        /// <summary>
        /// Number of pieces for each team in HalfCross board.
        /// Key: piece type, Value: count
        /// </summary>
        public Dictionary<int, List<(PieceType type, int count, PieceColor color)>> HalfCrossTeamSetup { get; set; }
            = new Dictionary<int, List<(PieceType, int, PieceColor)>>()
        {
            // 陣營1：紅方
            [1] = new List<(PieceType, int, PieceColor)>()
            {
                (PieceType.Advisor,  2, PieceColor.Red),
                (PieceType.Elephant, 2, PieceColor.Red),
                (PieceType.Chariot,  2, PieceColor.Red),
                (PieceType.Horse,    2, PieceColor.Red),
                (PieceType.Cannon,   2, PieceColor.Red),
            },

            // 陣營2：黑方
            [2] = new List<(PieceType, int, PieceColor)>()
            {
                (PieceType.Advisor,  2, PieceColor.Black),
                (PieceType.Elephant, 2, PieceColor.Black),
                (PieceType.Chariot,  2, PieceColor.Black),
                (PieceType.Horse,    2, PieceColor.Black),
                (PieceType.Cannon,   2, PieceColor.Black),
            },
            
            // 陣營3：將帥方
            [3] = new List<(PieceType, int, PieceColor)>()
            {
                (PieceType.General, 1, PieceColor.Red),
                (PieceType.General, 1, PieceColor.Black),
                (PieceType.Soldier, 5, PieceColor.Red),
                (PieceType.Soldier, 5, PieceColor.Black),
            },
        };

        #endregion
    }
}
