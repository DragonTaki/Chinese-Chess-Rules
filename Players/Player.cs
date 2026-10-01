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
    /// Represents a player in the game, including their side and clock.
    /// </summary>
    public class Player
    {
        /// <summary>
        /// The player’s side or faction (Player1, Player2, Player3, Neutral).
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
        /// Sets the per-move time limit (步時).
        /// </summary>
        public void SetStepTime(TimeSpan stepTime)
        {
            Timer.StepTimeLimit = stepTime;
        }

        /// <summary>
        /// Sets the total time limit (局時); to set both limits at once, use <see cref="SetTime"/>.
        /// </summary>
        public void SetTotalTime(TimeSpan totalTime)
        {
            Timer.TotalTimeLimit = totalTime;
        }

        /// <summary>
        /// Sets both the total time limit (局時) and the per-move limit (步時).
        /// </summary>
        public void SetTime(TimeSpan totalTime, TimeSpan stepTime)
        {
            Timer.TotalTimeLimit = totalTime;
            Timer.StepTimeLimit = stepTime;
        }

        /// <summary>
        /// Switches the timer mode (count up / count down).
        /// </summary>
        public void SetMode(TimerMode mode)
        {
            Timer.Mode = mode;
        }
    }
}
