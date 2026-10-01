/* ----- ----- ----- ----- */
// PlayerTimer.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/28
// Update Date: 2026/10/01
// Version: v1.2
/* ----- ----- ----- ----- */

using System;
using System.Globalization;

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

        // --- Custom display templates ---
        public string StoppedSymbol { get; set; } = "--:--";
        public string UnlimitedSymbol { get; set; } = "∞:∞";

        // Time display template; placeholders: {hour}, {minute} (total minutes, or the minutes within the hour when {hour} is used too), {second}, {second.N} (N decimals), {totalSecond}. Default "{minute}:{second.2}"
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
            {
                // _lastUpdate defaults to DateTime.MinValue (never set
                // before the first step) — without stamping it here, the
                // very first Update() call after starting would compute a
                // multi-thousand-year delta and instantly terminate the
                // timer via the time-limit check in OnUpdate.
                _lastUpdate = DateTime.UtcNow;
                State = TimerState.Active;
            }
        }

        public void EndStep()
        {
            if (State is TimerState.Active)
            {
                // Finalize immediately rather than transitioning to
                // TimerState.StepEnded and waiting for a future Update()
                // call to process it: Update() only invokes OnUpdate while
                // State == Active (see below), so the StepEnded case in
                // OnUpdate's switch can never actually run — leaving the
                // timer permanently stuck at StepEnded (and StartStep()
                // unable to restart it, since it only accepts Idle).
                var now = DateTime.UtcNow;
                var delta = now - _lastUpdate;
                _lastUpdate = now;

                if (EnableStepTimer)
                    CurrentStepTime += delta;
                CurrentTotalTime += delta;

                if (Mode == TimerMode.CountDown)
                    CurrentTotalTime -= IncrementPerMove;

                CurrentStepTime = TimeSpan.Zero;
                State = TimerState.Idle;
            }
        }

        public void Pause()
        {
            if (State is TimerState.Active)
                State = TimerState.Paused;
        }

        public void Resume()
        {
            if (State is TimerState.Paused)
            {
                // Same reasoning as StartStep(): without restamping here,
                // the wall-clock time spent paused would be counted as
                // elapsed active time on the next Update() call.
                _lastUpdate = DateTime.UtcNow;
                State = TimerState.Active;
            }
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

            OnUpdate(delta);  // computes the elapsed delta and checks the time limits
        }

        protected virtual void OnUpdate(TimeSpan delta)
        {
            switch (State)
            {
                case TimerState.Active:
                    // Running: both the step time and the total time keep accumulating
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
                    // Unreachable in practice — Update() only calls
                    // OnUpdate() while State == Active (see above), and
                    // EndStep() now finalizes this transition synchronously
                    // itself instead of leaving State at StepEnded. Left
                    // in place (not deleted) as a defensive fallback in
                    // case something else ever sets State to StepEnded
                    // directly.
                    // Step ended: reset the step time and go back to Idle to wait for the next step
                    if (EnableStepTimer)
                        CurrentStepTime += delta;
                    CurrentTotalTime += delta;

                    if (Mode == TimerMode.CountDown)
                        CurrentTotalTime -= IncrementPerMove;

                    CurrentStepTime = TimeSpan.Zero;
                    State = TimerState.Idle;
                    break;

                case TimerState.Paused:
                    // Paused: time is not updated
                    break;

                case TimerState.Terminated:
                    // Terminated: fully stopped, nothing is updated
                    break;

                case TimerState.Idle:
                    break;

                default:
                    // Unknown state: nothing to do
                    break;
            }
        }

        public void Reset()
        {
            CurrentStepTime = TimeSpan.Zero;
            CurrentTotalTime = TimeSpan.Zero;
            State = TimerState.Idle;
        }

        /// <summary>
        /// The clock's elapsed times right now (step and total), for restoring later with
        /// <see cref="RestoreClockState"/> (undo). Does not include the time since the last
        /// <see cref="Update"/> call.
        /// </summary>
        public ClockState GetClockState() => new ClockState(CurrentStepTime, CurrentTotalTime);

        /// <summary>
        /// Sets the elapsed times back to <paramref name="state"/> and puts the clock in the
        /// given running state: <paramref name="active"/> = this side is to move (its step runs
        /// from now on, or is held <see cref="TimerState.Paused"/> when
        /// <paramref name="paused"/>); otherwise the clock is <see cref="TimerState.Idle"/>.
        /// Revives a <see cref="TimerState.Terminated"/> clock. Used to undo a move.
        /// </summary>
        public void RestoreClockState(ClockState state, bool active, bool paused)
        {
            CurrentStepTime = state.StepTime;
            CurrentTotalTime = state.TotalTime;
            _lastUpdate = DateTime.UtcNow;
            State = !active ? TimerState.Idle : (paused ? TimerState.Paused : TimerState.Active);
        }

        // Switches the timer mode
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
                // {minute} is the minutes within the hour when the template also shows {hour},
                // otherwise the total minutes (e.g. 65 for 1:05:00).
                int minutes = template.Contains("{hour}") ? time.Minutes : (int)time.TotalMinutes;
                int seconds = time.Seconds;
                double fractional = time.TotalSeconds - Math.Floor(time.TotalSeconds);

                // Invariant culture throughout: the decimal point is always '.', whatever the
                // system's regional settings.
                // --- Supports fractional-second formats {second.2}, {second.3} ---
                string result = template;

                // Parse {second.X}
                result = System.Text.RegularExpressions.Regex.Replace(result, @"\{second\.(\d+)\}", m =>
                {
                    int digits = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    return TruncatedSecondsInMinute(time, digits).ToString($"00.{new string('0', digits)}", CultureInfo.InvariantCulture);
                });

                // Standard fields
                result = result
                    .Replace("{hour}", hours.ToString("00", CultureInfo.InvariantCulture))
                    .Replace("{minute}", minutes.ToString("00", CultureInfo.InvariantCulture))
                    .Replace("{second}", seconds.ToString("00", CultureInfo.InvariantCulture))
                    .Replace("{totalSecond}", totalSeconds.ToString("0.##", CultureInfo.InvariantCulture));

                return result;
            }
            catch
            {
                // fallback when error
                double totalSeconds = time.TotalSeconds;
                int minutes = (int)time.TotalMinutes;
                double secondsInMinute = TruncatedSecondsInMinute(time, 2);

                return string.Create(CultureInfo.InvariantCulture, $"{minutes:00}:{secondsInMinute:00.00}");
            }
        }

        /// <summary>
        /// Seconds within the current minute, truncated (not rounded) to
        /// <paramref name="digits"/> decimals. Rounding made e.g. 4:59.996 print as
        /// "04:60.00" — a countdown shows that right after every step starts — so the
        /// fraction is cut from the integer tick count instead.
        /// </summary>
        private static double TruncatedSecondsInMinute(TimeSpan time, int digits)
        {
            digits = Math.Clamp(digits, 0, 7);  // TimeSpan resolution is 10^-7 s
            long scale = 1;
            for (int i = 0; i < digits; i++)
                scale *= 10;

            long ticksInMinute = time.Ticks % TimeSpan.TicksPerMinute;
            long units = ticksInMinute * scale / TimeSpan.TicksPerSecond;
            return (double)units / scale;
        }
    }

    /// <summary>A clock's elapsed step and total time at one moment (<see cref="PlayerTimer.GetClockState"/>).</summary>
    public readonly record struct ClockState(TimeSpan StepTime, TimeSpan TotalTime);

    public enum TimerMode
    {
        CountUp,   // count elapsed time up
        CountDown  // count remaining time down
    }

    public enum TimerState
    {
        Idle,        // not started yet, or just initialised / between steps
        Active,      // running
        StepEnded,   // the current step has ended (waiting for the next one)
        Paused,      // paused manually
        Terminated   // time is up or the game is over
    }
}