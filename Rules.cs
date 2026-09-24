/* ----- ----- ----- ----- */
// Rules.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/06
// Update Date: 2025/10/29
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Collections.Generic;

using Chinese_Chess_v3.Game.Core.Pieces;
using Chinese_Chess_v3.Game.Core.Players;

namespace Chinese_Chess_v3.Game.Core
{

    /// <summary>
    /// Represents the rules configuration for different board types in Chinese Chess.
    /// Contains settings for Full board, Half board, and HalfCross (三國) variants.
    /// </summary>
    public class Rules
    {
        #region Timer Setting

        public bool EnableStepTimer { get; set; } = true;

        public TimerMode TimerMode { get; set; } = TimerMode.CountDown;

        public bool EndGameWhenTimesUp { get; set; } = true;

        #endregion

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

        #region Jieqi / FlipChess Rules (揭棋大盤規則設定)

        /// <summary>
        /// Whether the board is in 揭棋 (Jieqi/FlipChess) mode — same board
        /// and starting piece counts as the Full board, but every piece
        /// except the two Generals starts face-down and shuffled among its
        /// own side's non-General starting squares. A still-hidden piece's
        /// first move must follow the movement rules of whichever piece
        /// type canonically starts at that square (see
        /// <c>PieceConstants.GetClassicPieceTypeAt</c>), not its own true
        /// identity; moving reveals it, after which it always moves as
        /// itself. Default: false.
        /// </summary>
        public bool IsJieqi { get; set; } = false;

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
        /// Piece composition for each of HalfCross's three independently
        /// hostile factions (confirmed by the author — not an alliance by
        /// color). <c>side</c> is the actual owning faction; <c>color</c> is
        /// only the visual color (a physical set only has two, so faction 3
        /// necessarily reuses both — see <see cref="Players.PlayerSide.Player3"/>).
        /// Key: arbitrary faction number, Value: that faction's pieces.
        /// </summary>
        public Dictionary<int, List<(PieceType type, int count, PieceColor color, PlayerSide side)>> HalfCrossTeamSetup { get; set; }
            = new Dictionary<int, List<(PieceType, int, PieceColor, PlayerSide)>>()
        {
            // 陣營1
            [1] = new List<(PieceType, int, PieceColor, PlayerSide)>()
            {
                (PieceType.Advisor,  2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Elephant, 2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Chariot,  2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Horse,    2, PieceColor.Red, PlayerSide.Player1),
                (PieceType.Cannon,   2, PieceColor.Red, PlayerSide.Player1),
            },

            // 陣營2
            [2] = new List<(PieceType, int, PieceColor, PlayerSide)>()
            {
                (PieceType.Advisor,  2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Elephant, 2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Chariot,  2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Horse,    2, PieceColor.Black, PlayerSide.Player2),
                (PieceType.Cannon,   2, PieceColor.Black, PlayerSide.Player2),
            },

            // 陣營3：將帥方 — its own independent faction (PlayerSide.Player3),
            // even though half its pieces are colored to look like the
            // other two factions' pieces (see the field doc above).
            [3] = new List<(PieceType, int, PieceColor, PlayerSide)>()
            {
                (PieceType.General, 1, PieceColor.Red,   PlayerSide.Player3),
                (PieceType.General, 1, PieceColor.Black, PlayerSide.Player3),
                (PieceType.Soldier, 5, PieceColor.Red,   PlayerSide.Player3),
                (PieceType.Soldier, 5, PieceColor.Black, PlayerSide.Player3),
            },
        };

        #endregion
    }
}
