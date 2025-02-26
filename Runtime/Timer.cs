using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// Represents a timer utility for managing time-based operations.
    /// </summary>
    /// <remarks>
    /// The Timer class allows you to create and manage timers that can execute actions after a specified duration.
    /// Timers can be configured to repeat their execution and can be associated with a MonoBehaviour for lifecycle management.
    /// </remarks>
    [Serializable]
    public sealed class Timer : IComparable<Timer>
    {
        #region Fields

        [Header("STATE")]
        [SerializeField] private TimerState _state = TimerState.Disable;
        [SerializeField] private bool _isDirty;
        [SerializeField] private bool _canLoop;
        [SerializeField] private bool _hasReference;
        [SerializeField] private bool _useUnscaledTime;
        [SerializeField] private bool _isSuspendedManually;

        [Header("DATA")]
        [SerializeField] [ReadOnly] private float _startTime;
        [SerializeField] [ReadOnly] private float _duration;
        [SerializeField] [ReadOnly] private float _endTime;
        [SerializeField] [ReadOnly] private float _suspendedTime;

        private MonoBehaviour _behaviour;
        
        #endregion Fields

        #region Constructors
        
        /// <summary>
        /// Creates an inactive Timer with an optional choice of using unscaled time.
        /// </summary>
        /// <param name="isUnscaled">Set to true for timeScale-independent (real-time) Timer, false for Timer affected by game time scaling (default).</param>
        public Timer(bool isUnscaled = false) : this(false, isUnscaled) {}

        /// <summary>
        /// Creates an inactive Timer with an optional choice to loop.
        /// </summary>
        /// <param name="hasLoop">Does the Timer repeat after execution?</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public Timer(bool hasLoop, bool isUnscaled)
        {
            _canLoop = hasLoop;
            _state = TimerState.Enabled;
            _useUnscaledTime = isUnscaled;
        }

        /// <summary>
        /// Creates an active Timer with the specified duration, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public Timer(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            Default(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates an active Timer with the specified duration, action to execute, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="action">The action to execute when the Timer completes.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public Timer(float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            Set(time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates an active Timer with the specified duration, TimerActions, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="timerActions">The TimerActions to associate with this Timer.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public Timer(float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            Set(time, timerActions, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates a Timer associated with a MonoBehaviour.
        /// The Timer is initially set to an inactive state.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public Timer([NotNull] MonoBehaviour behaviour, bool isUnscaled = false) : this(behaviour, false, isUnscaled) {}

        /// <summary>
        /// Creates a Timer associated with a MonoBehaviour.
        /// The Timer is initially set to an inactive state with loop and time scaling options.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public Timer([NotNull] MonoBehaviour behaviour, bool hasLoop, bool isUnscaled)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            SetBehaviour(behaviour);
            
            _canLoop = hasLoop;
            _state = TimerState.Enabled;
            _useUnscaledTime = isUnscaled;
        }

        /// <summary>
        /// Creates a Timer associated with a MonoBehaviour.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            Set(behaviour, time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates a Timer attached tp the life cycle of an object with a specified action.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="action">The action to execute when the Timer completes.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            Set(behaviour, time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates a Timer attached to the life cycle of an object with specified TimerActions and options.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="timerActions">The TimerActions to associate with the Timer.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            Set(behaviour, time, timerActions, hasLoop, isUnscaled);
        }
        
        #endregion Constructors
        
        #region Properties

        /// <summary>
        /// Gets or sets the TimerActions associated with the Timer.
        /// </summary>
        /// <value>
        /// The TimerActions associated with the Timer.
        /// </value>
        /// <remarks>
        /// This property allows you to access and manipulate the TimerActions which provide event handlers
        /// for various Timer events such as: OnCompleted, OnCancelled, OnSuspended, OnResumed, OnUpdated.
        /// </remarks>
        public TimerActions Actions { get; set; } = new();

        /// <summary>
        /// Gets the MonoBehaviour associated with the Timer.
        /// </summary>
        /// <value>
        /// The MonoBehaviour associated with the Timer.
        /// </value>
        /// <remarks>
        /// The Timer's life cycle is associated with this MonoBehaviour reference.
        /// </remarks>
        public MonoBehaviour Behaviour => _behaviour;

        /// <summary>
        /// Gets the duration of the Timer in seconds.
        /// </summary>
        /// <value>
        /// The duration of the Timer in seconds.
        /// </value>
        /// <remarks>
        /// It represents the length of time for which the Timer is set.
        /// </remarks>
        public float Duration => _duration;

        /// <summary>
        /// Gets the elapsed time in seconds since the Timer started.
        /// </summary>
        /// <value>
        /// The elapsed time in seconds.
        /// </value>
        /// <remarks>
        /// If the Timer has been manually suspended or canceled, it returns the time at which it was suspended or canceled.
        /// Otherwise, it returns the time elapsed since the Timer was started.
        /// </remarks>
        public float ElapsedTime
        {
            get
            {
                if (_state is TimerState.Cancelled or TimerState.Suspended) return _suspendedTime;

                return Time - _startTime;
            }
        }

        /// <summary>
        /// Gets the ending time of the Timer in seconds.
        /// </summary>
        /// <remarks>
        /// This property retrieves the time, in seconds, when the Timer is expected to end. It represents
        /// the moment in time when the Timer's duration has elapsed since it started.
        /// </remarks>
        public float EndTime => _endTime;

        /// <summary>
        /// Gets a value indicating whether the Timer has the loop feature enabled.
        /// </summary>
        /// <remarks>
        /// This property indicates whether the Timer is configured to automatically restart after reaching its end time,
        /// creating a looping behavior.
        /// </remarks>
        public bool HasLoop => _canLoop;

        /// <summary>
        /// Gets a value indicating whether the Timer has a MonoBehaviour reference attached.
        /// </summary>
        /// <remarks>
        /// This property indicates whether the Timer's life cycle is associated with a MonoBehaviour reference.
        /// </remarks>
        public bool HasReference => _hasReference;

        /// <summary>
        /// Gets a value indicating whether the Timer has been canceled.
        /// </summary>
        /// <remarks>
        /// This property indicates whether the Timer has been manually canceled by calling the Cancel method.
        /// </remarks>
        public bool IsCancelled => _state == TimerState.Cancelled;

        /// <summary>
        /// Gets a value indicating whether the Timer has been destroyed.
        /// </summary>
        /// <remarks>
        /// This property returns true if the underlying Timer <see cref="Behaviour"/> component has been destroyed, indicating that the Timer is no longer functional.
        /// </remarks>
        public bool IsDestroyed => !_behaviour;

        /// <summary>
        /// Gets a value indicating whether the Timer has completed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the Timer has completed; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// This property returns <c>true</c> if the Timer has completed its specified duration and is not cancelled or suspended.
        /// If the Timer is in a dirty state, it returns the value of the dirty flag. A Timer is considered dirty when its state is
        /// modified externally without following the regular completion conditions.
        /// </remarks>
        public bool IsDone
        {
            get
            {
                if (_state is TimerState.Cancelled or TimerState.Suspended) return false;
                if (!_isDirty) return Time >= _endTime;

                return _isDirty;
            }
            private set => _isDirty = value;
        }

        /// <summary>
        /// Gets a value indicating whether the Timer is currently in a suspended state.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the Timer is suspended; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// Use this property to check if the Timer is currently in a suspended state. A Timer can be suspended
        /// manually or as a result of being cancelled. When suspended, it does not actively count down.
        /// </remarks>
        public bool IsSuspended => _state == TimerState.Suspended;

        /// <summary>
        /// Gets a value indicating whether the Timer is manually suspended.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the Timer is manually suspended; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>
        /// Use this property to check if the Timer is manually suspended. A Timer can be manually suspended by
        /// calling the `Suspend` method with the `isManual` parameter set to `true`. When manually suspended, 
        /// it can be resumed using the `Resume` method.
        /// </remarks>
        public bool IsSuspendedManually => _isSuspendedManually;

        /// <summary>
        /// Gets the recorded start time of the Timer in seconds.
        /// </summary>
        /// <remarks>
        /// This property retrieves the time in seconds when the Timer was originally started.
        /// </remarks>
        public float StartTime => _startTime;

        /// <summary>
        /// Gets the current state of the Timer.
        /// </summary>
        /// <remarks>
        /// This property retrieves the current state of the Timer, from the <see cref="TimerState"/> enumeration,
        /// indicating whether the Timer is active, inactive, suspended, cancelled, or completed.
        /// </remarks>
        public TimerState State => _state;

        /// <summary>
        /// Gets the current time used by the Timer, considering the timescale.
        /// </summary>
        /// <remarks>
        /// This property returns the current time used by the Timer. If the Timer is set to use unscaled time,
        /// it returns the time since the start of the application; otherwise,
        /// it returns the scaled time based on the timescale of the application.
        /// </remarks>
        public float Time => _useUnscaledTime ? StaticTime.UnscaledTime : StaticTime.ScaledTime;

        /// <summary>
        /// Gets the remaining time of the Timer's execution.
        /// </summary>
        /// <remarks>
        /// This property returns the remaining time in seconds until completion of the Timer.
        /// If the Timer is canceled or suspended, it returns the time that was remaining when the Timer was interrupted.
        /// </remarks>
        /// <returns>The remaining time.</returns>
        public float TimeRemaining
        {
            get
            {
                if (_state is TimerState.Cancelled or TimerState.Suspended) return _suspendedTime;

                return _endTime - Time;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Timer uses unscaled time.
        /// </summary>
        /// <remarks>
        /// This property returns if the Timer is configured to use unscaled time, which means it
        /// operates independently of the timescale of the application, making it unaffected by pause or slow motion.
        /// </remarks>
        public bool UnscaledTime => _useUnscaledTime;

        #endregion Properties

        #region Methods

        private void AddTimer() => SO_TimerManager.AddTimer(this);

        /// <summary>
        /// Creates a Timer associated with a MonoBehaviour.
        /// The Timer is initially set to an inactive state.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, bool isUnscaled = false)
        {
            return new Timer(behaviour, false, isUnscaled);
        }

        /// <summary>
        /// Creates a Timer associated with a MonoBehaviour.
        /// The Timer is initially set to an inactive state with loop and time scaling options.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, bool hasLoop, bool isUnscaled)
        {
            return new Timer(behaviour, hasLoop, isUnscaled);
        }
        
        /// <summary>
        /// Creates a Timer associated with a MonoBehaviour.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(behaviour, time, hasLoop, isUnscaled);
        }
        
        /// <summary>
        /// Creates a Timer attached tp the life cycle of an object with a specified action.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="onCompleted">The action to execute when the Timer completes.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, float time, Action onCompleted, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(behaviour, time, onCompleted, hasLoop, isUnscaled);
        }
        
        /// <summary>
        /// Creates a Timer attached to the life cycle of an object with specified TimerActions and options.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="callbacks">The TimerActions to associate with the Timer.</param>
        /// <param name="hasLoop">Determines whether the Timer should repeat after execution.</param>
        /// <param name="isUnscaled">True to create a Timer unaffected by timescale (real-time), false to make it affected by game time scaling (default).</param>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(behaviour, time, callbacks, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Cancels the Timer, stopping its countdown and marking it as cancelled.
        /// </summary>
        /// <remarks>
        /// This method stops the Timer's countdown and sets its state to "Cancelled," indicating that the Timer has been intentionally terminated.
        /// The remaining time is stored for reference.
        /// </remarks>
        public void Cancel()
        {
            _state = TimerState.Cancelled;
            _suspendedTime = TimeRemaining;
            
            Actions.Cancell?.Invoke(); // Uncomment this line if necessary.
        }

        /// <summary>
        /// Cancels all active Timers managed by the Timer Manager.
        /// </summary>
        /// <remarks>
        /// This method cancels all active Timers that are currently managed by the Timer Manager.
        /// It effectively stops their countdowns and marks them as canceled, preventing them from
        /// completing or triggering their associated actions.
        /// </remarks>
        /// <seealso cref="Timer"/>
        /// <seealso cref="SO_TimerManager"/>
        public static void CancelAllTimers() => SO_TimerManager.CancelAllTimers();

        /// <summary>
        /// Cancels all active Timers associated with a specific MonoBehaviour.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour whose associated Timers should be cancelled.</param>
        /// <remarks>
        /// This method cancels all active Timers that are associated with the MonoBehaviour.
        /// It effectively stops their countdowns and marks them as canceled, preventing them from
        /// completing or triggering their associated actions.
        /// </remarks>
        /// <seealso cref="Timer"/>
        /// <seealso cref="SO_TimerManager"/>
        public static void CancelAllTimers(MonoBehaviour behaviour) => SO_TimerManager.CancelAllTimers(behaviour);

        /// <summary>
        /// Compares this Timer to another Timer based on their respective end times.
        /// </summary>
        /// <param name="other">The Timer to compare to.</param>
        /// <returns>
        /// A value indicating the relative order of the Timers based on their <see cref="EndTime"/>:
        /// -1 if earlier than, 0 if equal to, 1 if later than, the other Timer.
        /// </returns>
        /// <remarks>
        /// This method compares two Timer instances based on their <see cref="EndTime"/>.
        /// It returns a value that indicates their relative order, which can be used for sorting or comparison purposes.
        /// </remarks>
        public int CompareTo(Timer other) => EndTime.CompareTo(other.EndTime);

        private void Default(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            _useUnscaledTime = isUnscaled;
            _canLoop = hasLoop;
            NewDuration(time);
        }

        /// <summary>
        /// Extends the duration of the Timer by the specified amount.
        /// </summary>
        /// <param name="duration">The additional duration in seconds to add to the Timer.</param>
        /// <remarks>
        /// This method increases the duration of the Timer by the specified amount, effectively lengthening the time it takes to complete.
        /// It updates the Timer's end time accordingly, allowing it to account for the extended duration.
        /// </remarks>
        public void ExtendDuration(float duration) => _endTime += duration;

        /// <summary>
        /// Forces the immediate completion of the Timer.
        /// </summary>
        /// <remarks>
        /// Calling this method sets the Timer's completion state to true, indicating that the Timer has finished.
        /// This method bypasses the normal countdown process and immediately marks the Timer as completed.
        /// </remarks>
        public void ForceCompletion() => IsDone = true;

        /// <summary>
        /// Restart the Timer with the same duration, creating a loop effect.
        /// </summary>
        /// <remarks>
        /// This method resets the Timer's state and restarts it with the same duration, effectively creating a loop effect.
        /// The Timer will be in the "Active" state, and it will start counting down from the beginning of its duration.
        /// </remarks>
        public void LoopDuration()
        {
            ResetState();
            
            _state = TimerState.Activated;
            _isDirty = false;
            _startTime = _endTime;
            _endTime += _duration;
            _suspendedTime = 0;
            
            Actions.Restart?.Invoke();
        }

        /// <summary>
        /// Set a new duration and start the Timer.
        /// </summary>
        /// <param name="duration">The length of time in seconds for the new duration.</param>
        /// <remarks>
        /// This method resets the Timer's state and sets a new duration, effectively starting the Timer countdown.
        /// The Timer will be in the "Active" state after calling this method, and any previous state or time will be reset.
        /// </remarks>
        public void NewDuration(float duration)
        {
            ResetTime();
            ResetState();

            _isDirty = false;
            _startTime = Time;
            _duration = duration;
            _endTime = _startTime + duration;
            _state = TimerState.Activated;
            
            Actions.Restart?.Invoke();

            if (SO_TimerManager.Contains(this)) return;

            AddTimer();
        }

        /// <summary>
        /// Calculates the normalized time in seconds since the start of the Timer (Read Only). Helpful for Lerp methods.
        /// </summary>
        /// <returns>
        /// A value between 0 and 1 representing the progress of the Timer's current duration.
        /// If the Timer is cancelled or suspended, the progress is adjusted accordingly.
        /// </returns>
        /// <remarks>
        /// Use this method to obtain the normalized time progress of the Timer.
        /// The returned value ranges from 0 (start) to 1 (completion).
        /// </remarks>
        public float PercentageDone()
        {
            var interpolant = !(IsCancelled && IsSuspended) ? Time : _endTime - _suspendedTime;
            
            return Mathf.InverseLerp(_startTime, _endTime, interpolant);
        }

        /// <summary>
        /// Calculates the smoothed progress of the Timer's current duration using the SmoothStep function (Read Only).
        /// </summary>
        /// <returns>
        /// A value between 0 and 1 representing the smoothed progress of the Timer's current duration.
        /// The SmoothStep function is applied to the progress obtained from <see cref="PercentageDone"/>.
        /// </returns>
        /// <remarks>
        /// The returned value ranges from 0 (start) to 1 (completion) and provides a smooth transition between these points.
        /// It is based on the progress obtained from the <see cref="PercentageDone"/> method.
        /// </remarks>
        public float PercentageDoneSmoothStep() => Mathf.SmoothStep(0f, 1f, PercentageDone());

        /// <summary>
        /// Creates an active Timer with the specified duration, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public static Timer Record(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates an active Timer with the specified duration, action to execute, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="action">The action to execute when the Timer completes.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public static Timer Record(float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Creates an active Timer with the specified duration, TimerActions, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="timerActions">The TimerActions to associate with this Timer.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public static Timer Record(float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(time, timerActions, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Resets the Timer to its default state, excluding TimerActions events.
        /// </summary>
        /// <remarks>
        /// This method resets the Timer to its default state, clearing any previous settings or modifications.
        /// It sets the Timer's time, state, looping behavior, and timescale to their default values.
        /// Use this method when you want to return the Timer to its initial configuration without affecting TimerActions events.
        /// </remarks>
        public void Reload()
        {
            ResetTime();
            ResetState();
        }

        /// <summary>
        /// Resets the Timer to its default settings, including TimerActions events.
        /// </summary>
        /// <remarks>
        /// This method resets the Timer to its default state, clearing any previous settings or modifications,
        /// and also resets the TimerActions events. It sets the Timer's time, state, looping behavior, and timescale
        /// to their default values. Use this method when you want to completely reset the Timer, including its actions.
        /// </remarks>
        public void Reset()
        {
            ResetTime();
            ResetState();
            Actions.Reset();
            ResetBehaviour();
        }

        private void ResetBehaviour()
        {
            _behaviour = null;
            _hasReference = false;
        }

        private void ResetState()
        {
            _isDirty = false;
            _canLoop = false;
            _useUnscaledTime = false;
            _isSuspendedManually = false;
            _state = TimerState.Disable;
        }

        private void ResetTime()
        {
            _endTime = 0f;
            _duration = 0f;
            _startTime = 0f;
            _suspendedTime = 0f;
        }

        /// <summary>
        /// Restarts the Timer with the same duration.
        /// </summary>
        /// <remarks>
        /// This method restarts the Timer using its current settings.
        /// It effectively resets the Timer's state, allowing it to run for the same duration.
        /// Use this method when you want to restart the Timer without changing its duration.
        /// </remarks>
        public void Restart() => NewDuration(_duration);

        /// <summary>
        /// Restarts the Timer with a new duration while keeping current settings.
        /// </summary>
        /// <param name="duration">The length of time in seconds for the Timer's new duration.</param>
        /// <remarks>
        /// This method restarts the Timer with a new duration while retaining its current settings.
        /// It allows you to specify a new duration for the Timer to run.
        /// Use this method to change the Timer's duration without altering other settings.
        /// </remarks>
        public void Restart(float duration) => NewDuration(duration);

        /// <summary>
        /// Resumes a suspended Timer, continuing its countdown.
        /// </summary>
        /// <remarks>
        /// This method resumes a previously suspended Timer, allowing it to continue its countdown from the point where it was suspended.
        /// It resets the Timer's state to "Active" and adjusts the start and end times based on the time remaining (when it was suspended).
        /// </remarks>
        public void Resume()
        {
            _isSuspendedManually = false;
            _endTime = Time + _suspendedTime;
            _startTime = _endTime - _duration;
            _state = TimerState.Activated;
            
            Actions.Resumed?.Invoke();
        }

        /// <summary>
        /// Resumes all suspended Timers to continue their countdowns.
        /// </summary>
        /// <remarks>
        /// This method resumes all suspended Timers, allowing them to continue their countdowns from
        /// the point where they were suspended.
        /// </remarks>
        /// <seealso cref="Timer"/>
        /// <seealso cref="SO_TimerManager"/>
        public static void ResumeAllTimers() => SO_TimerManager.ResumeAllTimers();

        /// <summary>
        /// Resumes all suspended Timers associated with a specific MonoBehaviour to continue their countdowns.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour whose associated Timers should be resumed.</param>
        /// <remarks>
        /// This method resumes all suspended Timers associated with the specified MonoBehaviour,
        /// allowing them to continue their countdowns from the point where they were suspended.
        /// </remarks>
        /// <seealso cref="Timer"/>
        /// <seealso cref="SO_TimerManager"/>
        public static void ResumeAllTimers(MonoBehaviour behaviour) => SO_TimerManager.ResumeAllTimers(behaviour);

        /// <summary>
        /// Sets the Timer's properties, including duration, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public void Set(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            Reload();
            Default(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Sets the Timer's properties, including duration, the action to execute, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="onComplete">The action to execute when the Timer completes.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public void Set(float time, Action onComplete, bool hasLoop = false, bool isUnscaled = false)
        {
            Reload();
            Actions.Set(onComplete);
            Default(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Sets the Timer's properties, including duration, TimerActions to handle events, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="callbacks">The TimerActions to associate with the Timer.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public void Set(float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            Reload();
            Actions.Set(callbacks);
            Default(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Sets the Timer's properties, including the associated MonoBehaviour, duration, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public void Set(MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            Reload();
            SetBehaviour(behaviour);
            Default(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Sets the Timer's properties, including the associated MonoBehaviour, duration, action to execute, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="onComplete">The action to execute when the Timer completes.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public void Set(MonoBehaviour behaviour, float time, Action onComplete, bool hasLoop = false, bool isUnscaled = false)
        {
            Reset();
            Actions.Set(onComplete);
            SetBehaviour(behaviour);
            Default(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Sets the Timer's properties, including the associated MonoBehaviour, duration, TimerActions to handle events, loop setting, and time scaling preference.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour to associate with the Timer.</param>
        /// <param name="time">The Timer duration in seconds.</param>
        /// <param name="callbacks">The TimerActions to associate with the Timer.</param>
        /// <param name="hasLoop">Does the Timer repeat after execution? Default is false.</param>
        /// <param name="isUnscaled">Set to true for a timeScale-independent (real-time) Timer, false for a Timer affected by game time scaling (default).</param>
        public void Set(MonoBehaviour behaviour, float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            Reset();
            Actions.Set(callbacks);
            SetBehaviour(behaviour);
            Default(time, hasLoop, isUnscaled);
        }

        public Timer SetBehaviour([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            _behaviour = behaviour;
            _hasReference = true;
            
            return this;
        }

        /// <summary>
        /// Sets the state of the Timer to the specified TimerState.
        /// </summary>
        /// <param name="state">The new Timer state to be set.</param>
        /// <remarks>
        /// Use this method to change the state of the Timer, such as from Active to Suspended or Cancelled.
        /// </remarks>
        public void SetState(TimerState state) => _state = state;

        /// <summary>
        /// Starts the Timer with the specified duration.
        /// </summary>
        /// <param name="duration">The length of time in seconds for the Timer.</param>
        /// <remarks>
        /// This method initiates the Timer with the given duration. It effectively sets the Timer's state to active.
        /// </remarks>
        public void Start(float duration) => NewDuration(duration);

        /// <summary>
        /// Suspends the Timer, pausing its countdown.
        /// </summary>
        /// <param name="isManual">Optional. Indicates whether the suspension is manual (default) or automatic.</param>
        /// <remarks>
        /// When the timer is suspended, its countdown is paused. You can specify whether the suspension is manual or automatic.
        /// Manual suspension allows you to resume the timer at a later time, while automatic suspension might be triggered by specific conditions.
        /// </remarks>
        public void Suspend(bool isManual = true)
        {
            _isSuspendedManually = isManual;
            _suspendedTime = TimeRemaining;
            _state = TimerState.Suspended;
            
            Actions.Suspend?.Invoke();
        }

        /// <summary>
        /// Suspends all active Timers, pausing their countdown.
        /// </summary>
        /// <remarks>
        /// This method suspends all active Timers, which temporarily halts their countdowns.
        /// Suspended Timers will not trigger their associated actions until they are resumed.
        /// </remarks>
        /// <seealso cref="Timer"/>
        /// <seealso cref="SO_TimerManager"/>
        public static void SuspendAllTimers() => SO_TimerManager.SuspendAllTimers();

        /// <summary>
        /// Suspends all active Timers associated with a specific MonoBehaviour, pausing their countdown.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour whose associated Timers should be suspended.</param>
        /// <remarks>
        /// This method suspends all active Timers associated with the specified MonoBehaviour, which temporarily halts their countdowns.
        /// Suspended Timers will not trigger their associated actions until they are resumed.
        /// </remarks>
        /// <seealso cref="Timer"/>
        /// <seealso cref="SO_TimerManager"/>
        public static void SuspendAllTimers(MonoBehaviour behaviour) => SO_TimerManager.SuspendAllTimers(behaviour);

        #endregion Methods    
    
        #region Builder Pattern

        public Timer OnCancell(Action action)
        {
            Actions.Cancell += action;
            return this;
        }

        public Timer OnCompleted(Action action)
        {
            Actions.Completed += action;
            return this;
        }

        public Timer OnRestart(Action action)
        {
            Actions.Restart += action;
            return this;
        }

        public Timer OnResumed(Action action)
        {
            Actions.Resumed += action;
            return this;
        }

        public Timer OnSuspend(Action action)
        {
            Actions.Suspend += action;
            return this;
        }

        public Timer OnUpdated(Action<float> action)
        {
            Actions.Updated += action;
            return this;
        }

        public Timer Run()
        {
            NewDuration(_duration);
            return this;
        }

        public Timer Run(float duration)
        {
            NewDuration(duration);
            return this;
        }

        public Timer SetActions([NotNull] TimerActions actions)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            return this;
        }

        public Timer SetDuration(float duration)
        {
            _duration = duration;
            return this;
        }

        public Timer SetLoop(bool canLoop)
        {
            _canLoop = canLoop;
            return this;
        }

        public Timer SetUnscaledTime(bool useUnscaledTime)
        {
            _useUnscaledTime = useUnscaledTime;
            return this;
        }

        #endregion Builder Pattern
    }

    /// <summary>
    /// A class that defines actions to be triggered at different phases of a Timer.
    /// </summary>
    public sealed class TimerActions
    {
        #region Constructors

        public TimerActions() {}

        /// <summary>
        /// Creates a new instance of TimerActions, copying actions from an existing TimerActions object.
        /// </summary>
        /// <param name="actions">The TimerActions object to copy from.</param>
        public TimerActions(TimerActions actions) => Set(actions);
        
        /// <summary>
        /// Creates a new instance of TimerActions with the specified events.
        /// </summary>
        /// <param name="onCompleted">Action to be triggered when the Timer completes its execution.</param>
        /// <param name="onUpdated">Action to be triggered periodically during the Timer's execution, providing the current progress as a parameter.</param>
        /// <param name="onSuspend">Action to be triggered when the Timer is suspended.</param>
        /// <param name="onResumed">Action to be triggered when the Timer is resumed.</param>
        /// <param name="onCancell">Action to be triggered when the Timer is cancelled.</param>
        /// <param name="onRestart">Action to be triggered when the Timer is restarted.</param>
        public TimerActions(
            Action onCompleted = null, 
            Action<float> onUpdated = null, 
            Action onSuspend = null, 
            Action onResumed = null, 
            Action onCancell = null, 
            Action onRestart = null)
        {
            Set(onCompleted, onUpdated, onSuspend, onResumed, onCancell, onRestart);
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Resets the Timer actions to their default values.
        /// </summary>
        public void Reset() => Set();

        /// <summary>
        /// Sets the Timer actions based on a TimerActions object.
        /// </summary>
        /// <param name="actions">The TimerActions object containing actions for various Timer events.</param>
        public void Set([NotNull] TimerActions actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            
            Set(
                actions.Completed,
                actions.Updated,
                actions.Suspend,
                actions.Resumed,
                actions.Cancell,
                actions.Restart);
        }

        /// <summary>
        /// Sets the actions to be triggered at different phases during the Timer's execution.
        /// </summary>
        /// <param name="onCompleted">Action to be triggered when the Timer completes its execution.</param>
        /// <param name="onUpdated">Action to be triggered periodically during the Timer's execution, providing the current progress as a parameter.</param>
        /// <param name="onSuspend">Action to be triggered when the Timer is suspended.</param>
        /// <param name="onResumed">Action to be triggered when the Timer is resumed.</param>
        /// <param name="onCancell">Action to be triggered when the Timer is cancelled.</param>
        /// <param name="onRestart">Action to be triggered when the Timer is restarted.</param>
        public void Set(
            Action onCompleted = null, 
            Action<float> onUpdated = null, 
            Action onSuspend = null, 
            Action onResumed = null, 
            Action onCancell = null, 
            Action onRestart = null)
        {
            Completed = onCompleted ?? delegate { };
            Updated = onUpdated ?? delegate { };
            Suspend = onSuspend ?? delegate { };
            Resumed = onResumed ?? delegate { };
            Cancell = onCancell ?? delegate { };
            Restart = onRestart ?? delegate { };
        }

        #endregion Methods

        #region Events

        /// <summary>
        /// Action to be triggered when the Timer completes its execution.
        /// </summary>
        public Action Completed = delegate { };

        /// <summary>
        /// Action to be triggered periodically during the Timer's execution, providing the current progress as a parameter.
        /// </summary>
        public Action<float> Updated = delegate { };

        /// <summary>
        /// Action to be triggered when the Timer is suspended.
        /// </summary>
        public Action Suspend = delegate { };

        /// <summary>
        /// Action to be triggered when the Timer is resumed.
        /// </summary>
        public Action Resumed = delegate { };

        /// <summary>
        /// Action to be triggered when the Timer is cancelled.
        /// </summary>
        public Action Cancell = delegate { };

        /// <summary>
        /// Action to be triggered when the Timer is restarted.
        /// </summary>
        public Action Restart = delegate { };

        #endregion Events
    }
    
    /// <summary>
    /// Represents the current phase of a Timer.
    /// </summary>
    public enum TimerState
    {
        /// <summary>
        /// The Timer is disabled and not currently running.
        /// </summary>
        Disable = 0,
        
        /// <summary>
        /// The Timer is enabled, but not currently running.
        /// </summary>
        Enabled = 1,

        /// <summary>
        /// The Timer is active and running.
        /// </summary>
        Activated = 2,

        /// <summary>
        /// The Timer is suspended, temporarily paused.
        /// </summary>
        Suspended = 3,

        /// <summary>
        /// The Timer has been cancelled and will not complete.
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// The Timer has completed its execution.
        /// </summary>
        Completed = 5
    }
}
