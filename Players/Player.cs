/* ----- ----- ----- ----- */
// Player.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/05/10
// Update Date: 2025/05/10
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

namespace Chinese_Chess_v3.Game.Core.Players
{
    /// <summary>
    /// Represents a player in the game, including their timer and optional list of owned pieces.
    /// </summary>
    public class Player
    {
        /// <summary>
        /// The player’s side or faction (Red, Black, Neutral).
        /// </summary>
        public PlayerSide Side { get; }

        /// <summary>
        /// Timer associated with this player.
        /// </summary>
        public PlayerTimer Timer { get; }

        public Player(
            PlayerSide side,
            TimeSpan totalTimeLimit,
            TimeSpan stepTimeLimit,
            TimeSpan? incrementPerMove = null,
            bool enableStepTimer = true,
            TimerMode mode = TimerMode.CountDown)
        {
            Side = side;
            Timer = new PlayerTimer(totalTimeLimit, stepTimeLimit, incrementPerMove, enableStepTimer, mode);
        }

        /// <summary>
        /// 設定步時限制
        /// </summary>
        public void SetStepTime(TimeSpan stepTime)
        {
            Timer.StepTimeLimit = stepTime;
        }

        /// <summary>
        /// 設定局時限制，若同時要設定步時可以一起呼叫 SetTime
        /// </summary>
        public void SetTotalTime(TimeSpan totalTime)
        {
            Timer.TotalTimeLimit = totalTime;
        }

        /// <summary>
        /// 同時設定局時與步時限制
        /// </summary>
        public void SetTime(TimeSpan totalTime, TimeSpan stepTime)
        {
            Timer.TotalTimeLimit = totalTime;
            Timer.StepTimeLimit = stepTime;
        }

        /// <summary>
        /// 切換計時模式（正數 / 倒數）
        /// </summary>
        public void SetMode(TimerMode mode)
        {
            Timer.Mode = mode;
        }
    }
}
