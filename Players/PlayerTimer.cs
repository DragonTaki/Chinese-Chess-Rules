/* ----- ----- ----- ----- */
// PlayerTimer.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/28
// Update Date: 2025/10/31
// Version: v1.1
/* ----- ----- ----- ----- */

using System;

namespace Chinese_Chess_v3.Game.Core.Players
{
    public class PlayerTimer
    {
        public TimeSpan TotalTimeLimit { get; set; }
        public TimeSpan StepTimeLimit { get; set; }
        public TimerMode Mode { get; set; } = TimerMode.CountDown;
        public bool Unlimited { get; set; } = false;

        public TimeSpan CurrentStepTime { get; private set; } = TimeSpan.Zero;
        public TimeSpan CurrentTotalTime { get; private set; } = TimeSpan.Zero;
        public bool EnableStepTimer { get; set; } = true;
        public TimeSpan IncrementPerMove { get; set; } = TimeSpan.Zero;

        private DateTime _lastUpdate;
        public TimerState State { get; private set; } = TimerState.Idle;

#nullable enable
        public event Action? TimeUp;
#nullable disable

        // --- 自訂顯示模板 ---
        public string StoppedSymbol { get; set; } = "--:--";
        public string UnlimitedSymbol { get; set; } = "∞:∞";

        // 時間顯示格式，例如 "hh:mm:ss.fff", "hh:mm:ss", "mm:ss.fff"
        public string TimeFormat { get; set; } = "{minute}:{second.2}";

        public PlayerTimer(
            TimeSpan totalTimeLimit,
            TimeSpan stepTimeLimit,
            TimeSpan? incrementPerMove = null,
            bool enableStepTimer = true,
            TimerMode mode = TimerMode.CountDown)
        {
            TotalTimeLimit = totalTimeLimit;
            StepTimeLimit = stepTimeLimit;
            IncrementPerMove = incrementPerMove ?? TimeSpan.Zero;
            EnableStepTimer = enableStepTimer;
            Mode = mode;
        }

        public void StartStep()
        {
            if (State is TimerState.Idle)
                State = TimerState.Active;
        }

        public void EndStep()
        {
            if (State is TimerState.Active)
                State = TimerState.StepEnded;
        }

        public void Pause()
        {
            if (State is TimerState.Active)
                State = TimerState.Paused;
        }

        public void Resume()
        {
            if (State is TimerState.Paused)
                State = TimerState.Active;
        }

        public void End()
        {
            State = TimerState.Terminated;
        }

        public void Update()
        {
            if (State != TimerState.Active)
                return;

            var now = DateTime.UtcNow;
            var delta = now - _lastUpdate;
            _lastUpdate = now;

            OnUpdate(delta);  // 內部計算增量、時間判斷
        }

        protected virtual void OnUpdate(TimeSpan delta)
        {
            switch (State)
            {
                case TimerState.Active:
                    // 正在進行：步時、局時都持續增加
                    if (EnableStepTimer)
                        CurrentStepTime += delta;
                    CurrentTotalTime += delta;

                    if (!Unlimited)  // Auto end game if time reach limit
                    {
                        if ((EnableStepTimer && CurrentStepTime >= StepTimeLimit) ||
                            CurrentTotalTime >= TotalTimeLimit)
                        {
                            State = TimerState.Terminated;
                            TimeUp?.Invoke();
                            return;
                        }
                    }

                    break;

                case TimerState.StepEnded:
                    // 步結束：將步時歸零，狀態轉回Idle等待下一步
                    if (EnableStepTimer)
                        CurrentStepTime += delta;
                    CurrentTotalTime += delta;

                    if (Mode == TimerMode.CountDown)
                        CurrentTotalTime -= IncrementPerMove;

                    CurrentStepTime = TimeSpan.Zero;
                    State = TimerState.Idle;
                    break;

                case TimerState.Paused:
                    // 暫停狀態：不更新時間
                    break;

                case TimerState.Terminated:
                    // 結束狀態：完全停止，不更新
                    break;

                case TimerState.Idle:
                    break;

                default:
                    // 等待開始，不處理
                    break;
            }
        }

        public void Reset()
        {
            CurrentStepTime = TimeSpan.Zero;
            CurrentTotalTime = TimeSpan.Zero;
            State = TimerState.Idle;
        }

        // 切換計時模式
        public void SwitchMode(TimerMode mode)
        {
            Mode = mode;
        }

        public string GetStepTimeString()
        {
            TimeSpan display = Mode == TimerMode.CountDown
                ? StepTimeLimit - CurrentStepTime
                : CurrentStepTime;

            if (display < TimeSpan.Zero) display = TimeSpan.Zero;
            return FormatTimeSpan(display);
        }

        public string GetTotalTimeString()
        {
            TimeSpan display = Mode == TimerMode.CountDown
                ? TotalTimeLimit - CurrentTotalTime
                : CurrentTotalTime;

            if (display < TimeSpan.Zero) display = TimeSpan.Zero;
            return FormatTimeSpan(display);
        }

        private string FormatTimeSpan(TimeSpan time)
        {
            const string defaultTemplate = "{minute}:{second.2}";  // default

            string template = TimeFormat;
            if (string.IsNullOrWhiteSpace(template))
                template = defaultTemplate;

            try
            {
                double totalSeconds = time.TotalSeconds;
                int hours = (int)time.TotalHours;
                int minutes = (int)time.TotalMinutes;
                int seconds = time.Seconds;
                double secondsInMinute = time.Seconds + time.Milliseconds / 1000.0;
                double fractional = time.TotalSeconds - Math.Floor(time.TotalSeconds);

                // --- 支援小數秒格式 {second.2}, {second.3} ---
                string result = template;

                // 解析 {second.X}
                result = System.Text.RegularExpressions.Regex.Replace(result, @"\{second\.(\d+)\}", m =>
                {
                    int digits = int.Parse(m.Groups[1].Value);
                    return secondsInMinute.ToString($"00.{new string('0', digits)}");
                });

                // 標準欄位
                result = result
                    .Replace("{hour}", hours.ToString("00"))
                    .Replace("{minute}", minutes.ToString("00"))
                    .Replace("{second}", seconds.ToString("00"))
                    .Replace("{totalSecond}", totalSeconds.ToString("0.##"));

                return result;
            }
            catch
            {
                // fallback when error
                double totalSeconds = time.TotalSeconds;
                int minutes = (int)time.TotalMinutes;
                double secondsInMinute = time.Seconds + time.Milliseconds / 1000.0;

                return $"{minutes:00}:{secondsInMinute:00.00}";
            }
        }
    }

    public enum TimerMode
    {
        CountUp,   // 正數計時
        CountDown  // 倒數計時
    }

    public enum TimerState
    {
        Idle,        // 尚未開始或剛初始化
        Active,      // 正在計時
        StepEnded,   // 當前步已結束（等待下一步）
        Paused,      // 人為暫停
        Terminated   // 時間結束或整局結束
    }
}