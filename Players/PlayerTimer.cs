/* ----- ----- ----- ----- */
// PlayerTimer.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2025/10/28
// Update Date: 2026/10/05
// Version: v1.3
/* ----- ----- ----- ----- */

using System;

namespace Chinese_Chess_v3.Game.Core.Players
{
    /// <summary>
    /// A player's clock: measures the step and total time against the limits and raises
    /// <see cref="TimeUp"/>. Only measures; the clock texts are written by the logic layer
    /// (<c>ClockFormatter</c>).
    /// </summary>
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

        /// <summary>
        /// Whether the step time is measured: always for a count-up clock (it only measures,
        /// it has no limits), and for a countdown clock while <see cref="EnableStepTimer"/>.
        /// </summary>
        private bool MeasuresStep => EnableStepTimer || Mode == TimerMode.CountUp;

        /// <summary>
        /// Whether the limits apply (<see cref="TimeUp"/> can be raised): only a countdown clock
        /// that is not <see cref="Unlimited"/>. A count-up clock (正數) only measures time: no
        /// limit, never a time-up, no increment.
        /// </summary>
        public bool HasTimeLimit => Mode == TimerMode.CountDown && !Unlimited;

        /// <summary>
        /// What happens when a limit is reached: false (default) stops the clock
        /// (<see cref="TimerState.Terminated"/>) before raising <see cref="TimeUp"/>; true keeps it
        /// running into overtime (<see cref="IsOvertime"/>, shown as a negative time) and raises
        /// <see cref="TimeUp"/> once each time it newly goes over (<c>Rules.EndGameWhenTimesUp</c>
        /// off, author decision 2026-10-02).
        /// </summary>
        public bool ContinueAfterTimeUp { get; set; } = false;

        /// <summary>Whether a limit is reached: the total time, or the step time while <see cref="EnableStepTimer"/>.</summary>
        public bool IsOvertime => HasTimeLimit
            && ((EnableStepTimer && CurrentStepTime >= StepTimeLimit) || CurrentTotalTime >= TotalTimeLimit);

        // Whether TimeUp was raised for the current overtime (ContinueAfterTimeUp).
        private bool _overtimeRaised;

#nullable enable
        public event Action? TimeUp;
#nullable disable

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

                if (MeasuresStep)
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
                    if (MeasuresStep)
                        CurrentStepTime += delta;
                    CurrentTotalTime += delta;

                    // Auto end game if time reach limit (countdown only: count-up just measures)
                    if (IsOvertime)
                    {
                        if (!ContinueAfterTimeUp)
                        {
                            State = TimerState.Terminated;
                            TimeUp?.Invoke();
                            return;
                        }
                        if (!_overtimeRaised)
                        {
                            _overtimeRaised = true;
                            TimeUp?.Invoke();
                        }
                    }
                    else
                    {
                        _overtimeRaised = false;
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
                    if (MeasuresStep)
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
            _overtimeRaised = false;
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
            _overtimeRaised = IsOvertime;
            _lastUpdate = DateTime.UtcNow;
            State = !active ? TimerState.Idle : (paused ? TimerState.Paused : TimerState.Active);
        }

        // Switches the timer mode
        public void SwitchMode(TimerMode mode)
        {
            Mode = mode;
        }
    }

    /// <summary>A clock's elapsed step and total time at one moment (<see cref="PlayerTimer.GetClockState"/>).</summary>
    public readonly record struct ClockState(TimeSpan StepTime, TimeSpan TotalTime);

    public enum TimerMode
    {
        CountUp,   // count elapsed time up (正數): only measures - no limit, no time-up, no increment
        CountDown  // count remaining time down to the limits (倒數)
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
