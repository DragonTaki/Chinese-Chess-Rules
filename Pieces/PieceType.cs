/* ----- ----- ----- ----- */
// PieceType.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/14
// Update Date: 2025/10/31
// Version: v1.1
/* ----- ----- ----- ----- */

namespace Chinese_Chess_v3.Game.Core.Pieces
{
    public enum PieceType
    {
        None,
        General,   // 帥；將
        Advisor,   // 仕；士
        Elephant,  // 相；象
        Chariot,   // 俥；車
        Horse,     // 傌；馬
        Cannon,    // 炮；包
        Soldier,   // 兵；卒
        Shadow,    // For rendering
    }
}