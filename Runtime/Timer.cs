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

        [Header(TimerLib.Headers.State)]
        [SerializeField] [Tooltip(TimerLib.Tooltips.State)] private TimerState _state = TimerState.Disable;
        [SerializeField] [Tooltip(TimerLib.Tooltips.IsDirty)] private bool _isDirty;
        [SerializeField] [Tooltip(TimerLib.Tooltips.CanLoop)] private bool _canLoop;
        [SerializeField] [Tooltip(TimerLib.Tooltips.HasReference)] private bool _hasReference;
        [SerializeField] [Tooltip(TimerLib.Tooltips.UseUnscaledTime)] private bool _useUnscaledTime;
        [SerializeField] [Tooltip(TimerLib.Tooltips.IsSuspendedManually)] private bool _isSuspendedManually;
        
        [Header(TimerLib.Headers.Data)]
        [SerializeField] [Tooltip(TimerLib.Tooltips.Duration)] [ReadOnly] private float _duration;
        [SerializeField] [Tooltip(TimerLib.Tooltips.SuspendedTime)] [ReadOnly] private float _suspendedTime;
        [SerializeField] [Tooltip(TimerLib.Tooltips.TimeRemaining)] [ReadOnly] private float _timeRemaining;
        [Space]
        [SerializeField] [Tooltip(TimerLib.Tooltips.IsDelayPhase)] [ReadOnly] private bool _isDelayPhase;
        [SerializeField] [Tooltip(TimerLib.Tooltips.DelayDuration)] [ReadOnly] private float _delayDuration;

        private Action _onCompleted;
        private Action<float> _onUpdated;

        private TimerActions _actions;
        private MonoBehaviour _behaviour;

        #endregion Fields

        #region Constructors
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class.
        /// </summary>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock (default is false).</param>
        /// <remarks>
        /// This constructor chains to the primary constructor with the looping parameter set to <c>false</c> by default.
        /// Use this for one-shot timers that need to ignore or respect the Unity Time Scale.
        /// </remarks>
        public Timer(bool isUnscaled = false) : this(false, isUnscaled) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with specific looping and time-scaling behaviors.
        /// </summary>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock (default is false).</param>
        /// <remarks>
        /// This constructor sets the timer to the <see cref="TimerState.Enabled"/> state, allowing it to be 
        /// added to the <c>TimerManager</c> for processing. Note that a duration must still be set 
        /// (via <see cref="SetDuration"/> or similar) before the timer can effectively progress.
        /// </remarks>
        public Timer(bool hasLoop, bool isUnscaled)
        {
            _canLoop = hasLoop;
            _useUnscaledTime = isUnscaled;
            _state = TimerState.Enabled;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with a specified duration.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock (default is false).</param>
        public Timer(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with a completion callback.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="action">The delegate to execute when the timer finishes.</param>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock (default is false).</param>
        public Timer(float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            Set(time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with a comprehensive set of callbacks.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="timerActions">A struct or class containing multiple lifecycle callbacks (Update, Start, Cancel, etc.).</param>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock (default is false).</param>
        public Timer(float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            Set(time, timerActions, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class bound to a <see cref="MonoBehaviour"/> lifecycle.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        public Timer([NotNull] MonoBehaviour behaviour, bool isUnscaled = false) : this(behaviour, false, isUnscaled) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with an owner, looping, and time-scaling settings.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        public Timer([NotNull] MonoBehaviour behaviour, bool hasLoop, bool isUnscaled)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            SetBehaviour(behaviour);
            
            _canLoop = hasLoop;
            _useUnscaledTime = isUnscaled;
            _state = TimerState.Enabled;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with an owner and a duration.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
    
            Set(behaviour, time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with a owner, duration, and completion callback.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="action">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            Set(behaviour, time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with an owner, duration, and multiple lifecycle callbacks.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="timerActions">A <see cref="TimerActions"/> container providing multi-stage callbacks such as Start, Update, and Cancel.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
    
            Set(behaviour, time, timerActions, hasLoop, isUnscaled);
        }
        
        #endregion Constructors
        
        #region Properties
        
        /// <summary>/// <summary>
        /// The delegate executed every frame while the timer is active. 
        /// Provides a normalized value (0 to 1) representing completion progress.
        /// </summary>

        /// The delegate executed when the timer reaches zero or is forced to complete.
        /// </summary>
        public event Action Completed
        {
            add => _onCompleted += value;
            remove => _onCompleted -= value;
        }
        
        /// <summary>
        /// The delegate executed every frame while the timer is active. 
        /// Provides a normalized value (0 to 1) representing completion progress.
        /// </summary>
        public event Action<float> Updated
        {
            add => _onUpdated += value;
            remove => _onUpdated -= value;
        }

        /// <summary>
        /// Gets or sets the container for lifecycle callbacks. 
        /// Lazily initializes a new instance if currently null.
        /// </summary>
        public TimerActions Actions
        {
            get => _actions ??= new TimerActions();
            set => _actions = value;
        }

        /// <summary>
        /// The <see cref="MonoBehaviour"/> owner of this timer used for lifecycle binding.
        /// </summary>
        public MonoBehaviour Behaviour => _behaviour;

        /// <summary>
        /// The total duration assigned to the timer in seconds.
        /// </summary>
        public float Duration => _duration;

        /// <summary>
        /// The amount of time in seconds that has passed since the timer started.
        /// </summary>
        public float ElapsedTime => _duration - _timeRemaining;

        /// <summary>
        /// Indicates whether the timer is configured to restart automatically upon completion.
        /// </summary>
        public bool HasLoop => _canLoop;

        /// <summary>
        /// Indicates whether this timer is bound to a <see cref="MonoBehaviour"/> reference.
        /// </summary>
        public bool HasReference => _hasReference;

        /// <summary>
        /// Indicates whether the timer uses <c>Time.unscaledDeltaTime</c> for its calculations.
        /// </summary>
        public bool HasUnscaledTime => _useUnscaledTime;

        /// <summary>
        /// Indicates whether the timer has been moved to the <see cref="TimerState.Cancelled"/> state.
        /// </summary>
        public bool IsCancelled => _state == TimerState.Cancelled;

        /// <summary>
        /// Indicates whether the timer is currently waiting out its initial delay period.
        /// </summary>
        public bool IsDelayPhase => _isDelayPhase;

        /// <summary>
        /// Returns true if the associated <see cref="MonoBehaviour"/> owner has been destroyed.
        /// </summary>
        public bool IsDestroyed => !_behaviour;

        /// <summary>
        /// Indicates whether the timer has reached its conclusion.
        /// </summary>
        /// <remarks>
        /// This property returns <c>false</c> if the timer is currently cancelled, suspended, or in a delay phase. 
        /// It returns <c>true</c> if the timer has been flagged as dirty or if the remaining time has lapsed.
        /// </remarks>
        public bool IsDone
        {
            get
            {
                if (_state is TimerState.Cancelled or TimerState.Suspended) return false;
                if (_isDelayPhase) return false;
                if (_isDirty) return true;

                return _timeRemaining <= 0f;
            }
            private set => _isDirty = value;
        }

        /// <summary>
        /// Indicates whether the timer is currently in the <see cref="TimerState.Suspended"/> state.
        /// </summary>
        public bool IsSuspended => _state == TimerState.Suspended;

        /// <summary>
        /// Indicates whether the timer was paused manually by user logic rather than a system-level event.
        /// </summary>
        public bool IsSuspendedManually => _isSuspendedManually;
        
        /// <summary>
        /// Provides global access to the <see cref="TimerManager"/> singleton instance.
        /// </summary>
        public static TimerManager Manager => TimerManager.Instance;

        /// <summary>
        /// The current operational <see cref="TimerState"/> of this instance.
        /// </summary>
        public TimerState State => _state;

        /// <summary>
        /// Gets the current game time, choosing between scaled and unscaled time based on configuration.
        /// </summary>
        public float Time => _useUnscaledTime ? StaticTime.UnscaledTime : StaticTime.ScaledTime;

        /// <summary>
        /// The amount of time in seconds remaining until the timer completes its current cycle.
        /// </summary>
        public float TimeRemaining => _timeRemaining;

        #endregion Properties

        #region Methods

        /// <summary>
        /// Registers this timer with the <see cref="TimerManager"/> for active processing.
        /// </summary>
        private void AddTimer()
        {
            var manager = Manager;
            if (manager) manager.AddTimer(this);
        }

        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> bound to a <see cref="MonoBehaviour"/> lifecycle.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, bool isUnscaled = false)
        {
            return new Timer(behaviour, false, isUnscaled);
        }

        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> with specific looping and scaling settings, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, bool hasLoop, bool isUnscaled)
        {
            return new Timer(behaviour, hasLoop, isUnscaled);
        }
        
        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> with a duration, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(behaviour, time, hasLoop, isUnscaled);
        }
        
        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> with a duration and completion callback, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="onCompleted">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, float time, Action onCompleted, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(behaviour, time, onCompleted, hasLoop, isUnscaled);
        }
        
        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> with a duration and multiple lifecycle callbacks, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="callbacks">A <see cref="TimerActions"/> container providing multi-stage callbacks such as Start, Update, and Cancel.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(behaviour, time, callbacks, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Stops the timer immediately and triggers the cancellation callback.
        /// </summary>
        public void Cancel()
        {
            _state = TimerState.Cancelled;
            _actions?.Cancell?.Invoke();
        }

        /// <summary>
        /// Globally cancels all active timers managed by the <see cref="TimerManager"/>.
        /// </summary>
        public static void CancelAllTimers()
        {
            var manager = Manager;
            if (manager) manager.CancelAllTimers();
        }

        /// <summary>
        /// Globally cancels all timers associated with a specific <see cref="MonoBehaviour"/>.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to cancel.</param>
        public static void CancelAllTimers(MonoBehaviour behaviour)
        {
            var manager = Manager;
            if (manager) manager.CancelAllTimers(behaviour);
        }

        /// <summary>
        /// Evaluates if the timer is valid and capable of progressing based on its state and owner's lifecycle.
        /// </summary>
        /// <returns>True if the timer can continue ticking; otherwise, false.</returns>
        /// <remarks>
        /// This method handles automatic suspension/resumption if the <see cref="MonoBehaviour"/> owner 
        /// is disabled/enabled, and triggers <see cref="Cancel"/> if the owner has been destroyed.
        /// </remarks>
        public bool CanProcess()
        {
            if (!_hasReference) return _state != TimerState.Suspended;

            if (!_behaviour)
            {
                Cancel();
                return false;
            }

            var isOwnerActive = _behaviour.isActiveAndEnabled;
            
            switch (isOwnerActive)
            {
                case false when _state != TimerState.Suspended:
                    Suspend( false);
                    return false;
                case true when _state == TimerState.Suspended && !_isSuspendedManually:
                    Resume();
                    return true;
                default:
                    return _state != TimerState.Suspended;
            }
        }

        /// <summary>
        /// Compares the remaining time of this timer with another to facilitate sorting within the <see cref="TimerManager"/>.
        /// </summary>
        public int CompareTo(Timer other) => _timeRemaining.CompareTo(other._timeRemaining);
        
        /// <summary>
        /// Checks if the specified timer is currently tracked by the <see cref="TimerManager"/>.
        /// </summary>
        public static bool ContainsTimer([NotNull] Timer timer)
        {
            var manager = Manager;
            return manager && manager.ContainsTimer(timer);
        }

        /// <summary>
        /// Transition the timer to the completed state and executes all completion delegates.
        /// </summary>
        public void ExecuteComplete()
        {
            SetState(TimerState.Completed);
            
            _onCompleted?.Invoke();
        }

        /// <summary>
        /// Executes update delegates, passing the current completion percentage (0 to 1).
        /// </summary>
        public void ExecuteUpdate()
        {
            var percentDone = PercentageDone();
            
            _onUpdated?.Invoke(percentDone);
        }

        /// <summary>
        /// Adds additional time to the current duration and remaining time.
        /// </summary>
        /// <param name="addedTime">The amount of time in seconds to add.</param>
        public void ExtendDuration(float addedTime)
        {
            if (addedTime <= 0f) return;
            
            _duration += addedTime;
            _timeRemaining += addedTime;

            if (!(_timeRemaining > 0f)) return;
            
            _isDirty = false;
            
            if (_state == TimerState.Completed) _state = TimerState.Activated;
        }

        /// <summary>
        /// Forces the timer to reach its end state immediately.
        /// </summary>
        public void ForceCompletion()
        {
            _timeRemaining = 0f;
            IsDone = true;
        }

        /// <summary>
        /// Centralized internal method to set the core operational parameters of the timer.
        /// </summary>
        private void Initialize(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            _duration = time;
            _timeRemaining = time;
            _canLoop = hasLoop;
            _useUnscaledTime = isUnscaled;
            _state = TimerState.Enabled;
        }

        /// <summary>
        /// Resets the timer to its full duration and restarts the countdown.
        /// </summary>
        public void ExecuteRestart()
        {
            var overflow = _timeRemaining;

            _isDirty = false;
            _timeRemaining = _duration + overflow;
            _state = TimerState.Activated;
            
            _actions?.Restart?.Invoke();
        }

        /// <summary>
        /// Updates the timer with a new total duration and resets progress.
        /// </summary>
        /// <param name="duration">The new duration in seconds.</param>
        public void NewDuration(float duration)
        {
            _duration = duration;

            var shouldActivate = _state is TimerState.Activated or TimerState.Completed;
            var targetState = shouldActivate ? TimerState.Activated : TimerState.Disable;
            
            ResetProgress(targetState);

            if (targetState != TimerState.Activated) return;
            if (ContainsTimer(this)) AddTimer();
        }

        /// <summary>
        /// Calculates the completion progress of the timer as a normalized value.
        /// </summary>
        /// <returns>A value between 0 and 1, where 1 indicates the timer has finished.</returns>
        public float PercentageDone()
        {
            if (_duration <= 0f) return 1f;
            
            var percent = 1f - _timeRemaining / _duration;
            return Mathf.Clamp01(percent);
        }

        /// <summary>
        /// Calculates the completion progress using a SmoothStep interpolation for non-linear movement.
        /// </summary>
        /// <returns>A value between 0 and 1 with smoothed easing at the start and end.</returns>
        public float PercentageDoneSmoothStep() => Mathf.SmoothStep(0f, 1f, PercentageDone());

        /// <summary>
        /// Manages the transition logic when a timer completes its initial delay phase.
        /// </summary>
        /// <returns>True if the timer is currently in or transitioning out of a delay phase; otherwise, false.</returns>
        public bool ProcessDelay()
        {
            if (!IsDelayPhase) return false;

            if (!IsDone) return true;
            
            _isDelayPhase = false;
            _timeRemaining = _duration;

            return true;
        }

        /// <summary>
        /// Static factory method to create and return a new <see cref="Timer"/> instance.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        public static Timer Record(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Static factory method to create and return a new <see cref="Timer"/> instance with a completion callback.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="action">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Record(float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Static factory method to create and return a new <see cref="Timer"/> instance with multiple lifecycle callbacks.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="timerActions">A <see cref="TimerActions"/> container providing multi-stage callbacks such as Start, Update, and Cancel.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Record(float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            return new Timer(time, timerActions, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Resets the timer's progress and core configuration without clearing the owner or callbacks.
        /// </summary>
        public void Reload()
        {
            // ResetConfiguration();
            ResetProgress(TimerState.Enabled, _duration);
        }

        /// <summary>
        /// Fully resets the timer instance, clearing all data, references, and callbacks to prepare for object pooling.
        /// </summary>
        public void Reset()
        {
            ResetConfiguration();
            ResetBehaviour();
            ResetProgress();
            
            _onUpdated = null;
            _onCompleted = null;
            _actions?.Reset();
        }

        /// <summary>
        /// Clears the <see cref="MonoBehaviour"/> owner reference and disables lifecycle tracking.
        /// </summary>
        private void ResetBehaviour()
        {
            _behaviour = null;
            _hasReference = false;
        }

        /// <summary>
        /// Resets operational flags and duration to their default values.
        /// </summary>
        private void ResetConfiguration()
        {
            _duration = 0f;
            _canLoop = false;
            _useUnscaledTime = false;
            _isSuspendedManually = false;
        }

        /// <summary>
        /// Internal helper to reset temporal progress and state.
        /// </summary>
        /// <param name="state">The <see cref="TimerState"/> to apply after the reset.</param>
        /// <param name="duration">Optional new duration to set; defaults to the current <c>_duration</c>.</param>
        private void ResetProgress(TimerState state = TimerState.Disable, float duration = 0)
        {
            _state = state;
            _isDirty = false;
            _timeRemaining = duration == 0 ? _duration : duration;
            
            _isDelayPhase = false;
            _delayDuration = 0f;
        }

        /// <summary>
        /// Resets the timer to its starting duration and resumes the countdown.
        /// </summary>
        public void Restart() => NewDuration(_duration);

        /// <summary>
        /// Updates the timer with a new duration and restarts the countdown.
        /// </summary>
        /// <param name="duration">The new duration of the timer in seconds.</param>
        public void Restart(float duration) => NewDuration(duration);

        /// <summary>
        /// Resumes a suspended timer and triggers the resumption callback.
        /// </summary>
        public void Resume()
        {
            _isSuspendedManually = false;
            _state = TimerState.Activated;
            _actions?.Resumed?.Invoke();
        }

        /// <summary>
        /// Globally resumes all timers managed by the <see cref="TimerManager"/> that were not manually suspended.
        /// </summary>
        public static void ResumeAllTimers()
        {
            var manager = Manager;
            if (manager) manager.ResumeAllTimers();
        }

        /// <summary>
        /// Globally resumes all timers associated with a specific <see cref="MonoBehaviour"/> that were not manually suspended.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to resume.</param>
        public static void ResumeAllTimers(MonoBehaviour behaviour)
        {
            var manager = Manager;
            if (manager) manager.ResumeAllTimers(behaviour);
        }

        /// <summary>
        /// Configures the timer with a duration and operational settings.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        private void Set(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Configures the timer with a duration, completion callback, and operational settings.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="onComplete">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        private void Set(float time, Action onComplete, bool hasLoop = false, bool isUnscaled = false)
        {
            _onCompleted = onComplete;
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Configures the timer with a duration, multiple lifecycle callbacks, and operational settings.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="callbacks">A <see cref="TimerActions"/> container providing multi-stage callbacks such as Start, Update, and Cancel.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        private void Set(float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            Actions.Set(callbacks);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Configures the timer with an owner, duration, and operational settings.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        private void Set(MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            SetBehaviour(behaviour);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Configures the timer with an owner, duration, completion callback, and operational settings.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="onComplete">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        private void Set(MonoBehaviour behaviour, float time, Action onComplete, bool hasLoop = false, bool isUnscaled = false)
        {
            _onCompleted = onComplete;
            SetBehaviour(behaviour);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Configures the timer with an owner, duration, multiple lifecycle callbacks, and operational settings.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="callbacks">A <see cref="TimerActions"/> container providing multi-stage callbacks such as Start, Update, and Cancel.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c> instead of the standard game clock.</param>
        private void Set(MonoBehaviour behaviour, float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            Actions.Set(callbacks);
            SetBehaviour(behaviour);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Directly sets the current operational state of the timer.
        /// </summary>
        /// <param name="state">The new <see cref="TimerState"/> to apply.</param>
        public void SetState(TimerState state) => _state = state;

        /// <summary>
        /// Pauses the timer's progression and triggers the suspension callback.
        /// </summary>
        /// <param name="isManual">Determines if the suspension was triggered by user logic rather than a system-level pause.</param>
        public void Suspend(bool isManual = true)
        {
            _isSuspendedManually = isManual;
            _state = TimerState.Suspended;
            _actions?.Suspend?.Invoke();
        }

        /// <summary>
        /// Globally suspends all active timers managed by the <see cref="TimerManager"/>.
        /// </summary>
        public static void SuspendAllTimers()
        {
            var manager = Manager;
            if (manager) manager.SuspendAllTimers();
        }

        /// <summary>
        /// Globally suspends all timers associated with a specific <see cref="MonoBehaviour"/>.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to suspend.</param>
        public static void SuspendAllTimers(MonoBehaviour behaviour)
        {
            var manager = Manager;
            if (manager) manager.SuspendAllTimers(behaviour);
        }

        /// <summary>
        /// Progresses the timer countdown and handles transitions between delay and active phases.
        /// </summary>
        /// <param name="deltaTime">The time slice to subtract from the remaining duration, typically provided by the <see cref="TimerManager"/>.</param>
        /// <remarks>
        /// This method only executes if the timer is in the <see cref="TimerState.Activated"/> state. 
        /// If a delay phase is active, it calculates the overflow time to ensure the transition to the 
        /// main duration remains temporally accurate.
        /// </remarks>
        public void Tick(float deltaTime)
        {
            if (_state != TimerState.Activated) return;
            
            _timeRemaining -= deltaTime;
            
            if (!IsDelayPhase) return;
            if (_timeRemaining > 0f) return;
            
            var overflow = _timeRemaining;
            _timeRemaining = _duration + overflow;
            _isDelayPhase = false;
        }

        #endregion Methods    
    
        #region Builder Pattern

        /// <summary>
        /// Initializes and returns a new <see cref="Timer"/> instance for further configuration.
        /// </summary>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public Timer Build() => new Timer();
        
        /// <summary>
        /// Associates the timer with a <see cref="MonoBehaviour"/> owner for lifecycle tracking.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer BindTo([NotNull] MonoBehaviour behaviour) => SetBehaviour(behaviour);
        
        /// <summary>
        /// Configures the timer to follow standard game time scaling.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Gametime() => SetUnscaledTime(false);
        
        /// <summary>
        /// Sets the total duration of the timer.
        /// </summary>
        /// <param name="duration">The duration of the timer in seconds.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Length(float duration) => SetDuration(duration);
        
        /// <summary>
        /// Configures the timer to automatically reset and restart upon completion.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Loop() => SetLoop(true);
        
        /// <summary>
        /// Configures the timer to execute only once without looping.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Once() => SetLoop(false);

        /// <summary>
        /// Adds a callback to be executed if the timer is manually cancelled.
        /// </summary>
        /// <param name="action">The delegate to execute upon cancellation.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnCancell(Action action)
        {
            Actions.Cancell += action;
            return this;
        }

        /// <summary>
        /// Adds a callback to be executed when the timer reaches completion.
        /// </summary>
        /// <param name="action">The delegate to execute upon completion.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        // public Timer OnCompleted(Action action)
        // {
        //     Actions.Completed += action;
        //     return this;
        // }

        /// <summary>
        /// Adds a callback to be executed whenever the timer restarts (e.g., during a loop).
        /// </summary>
        /// <param name="action">The delegate to execute upon restart.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnRestart(Action action)
        {
            Actions.Restart += action;
            return this;
        }

        /// <summary>
        /// Adds a callback to be executed when a suspended timer is resumed.
        /// </summary>
        /// <param name="action">The delegate to execute upon resumption.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnResumed(Action action)
        {
            Actions.Resumed += action;
            return this;
        }

        /// <summary>
        /// Adds a callback to be executed when the timer is suspended/paused.
        /// </summary>
        /// <param name="action">The delegate to execute upon suspension.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnSuspend(Action action)
        {
            Actions.Suspend += action;
            return this;
        }

        /// <summary>
        /// Adds a callback to be executed every frame while the timer is active.
        /// </summary>
        /// <param name="action">The delegate to execute, providing the current completion percentage (0 to 1).</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        // public Timer OnUpdated(Action<float> action)
        // {
        //     Actions.Updated += action;
        //     return this;
        // }

        /// <summary>
        /// Pauses the timer's progression.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Pause()
        {
            Suspend();
            return this;
        }

        /// <summary>
        /// Internal helper to prepare the timer state and register it with the manager.
        /// </summary>
        /// <param name="initialState">The <see cref="TimerState"/> to set upon priming.</param>
        /// <param name="initialDuration">The duration to initialize with.</param>
        private void Prime(TimerState initialState, float initialDuration = 0f)
        {
            ResetProgress(initialState, initialDuration);
            
            if (ContainsTimer(this)) AddTimer();
        }
        
        /// <summary>
        /// Configures the timer to use <c>Time.unscaledDeltaTime</c>.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Realtime() => SetUnscaledTime(true);

        /// <summary>
        /// Assigns a <see cref="TimerActions"/> container to the timer.
        /// </summary>
        /// <param name="actions">The container providing multi-stage callbacks.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided actions container is null.</exception>
        private Timer SetActions([NotNull] TimerActions actions)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            return this;
        }

        /// <summary>
        /// Assigns the owner of the timer and enables lifecycle tracking.
        /// </summary>
        /// <param name="behaviour">The owner of this timer. The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetBehaviour([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
    
            _behaviour = behaviour;
            _hasReference = true;
    
            return this;
        }

        /// <summary>
        /// Sets the total duration of the timer.
        /// </summary>
        /// <param name="duration">The duration in seconds.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetDuration(float duration)
        {
            _duration = duration;
            return this;
        }

        /// <summary>
        /// Configures whether the timer should automatically reset and restart upon completion.
        /// </summary>
        /// <param name="canLoop">Determines if the timer should loop.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetLoop(bool canLoop)
        {
            _canLoop = canLoop;
            return this;
        }

        /// <summary>
        /// Configures whether the timer should use unscaled or standard game time.
        /// </summary>
        /// <param name="useUnscaledTime">Determines if the timer should use <c>Time.unscaledDeltaTime</c>.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetUnscaledTime(bool useUnscaledTime)
        {
            _useUnscaledTime = useUnscaledTime;
            return this;
        }

        /// <summary>
        /// Activates the timer and begins the countdown.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Start()
        {
            Prime(TimerState.Activated);
            _actions?.Restart?.Invoke();
            
            return this;
        }

        /// <summary>
        /// Sets a new duration and activates the timer immediately.
        /// </summary>
        /// <param name="duration">The duration of the timer in seconds.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Start(float duration)
        {
            _duration = duration;
            
            Prime(TimerState.Activated, duration);
            _actions?.Restart?.Invoke();
            
            return this;
        }

        /// <summary>
        /// Activates the timer after a specified delay period.
        /// </summary>
        /// <param name="delay">The delay duration in seconds before the main timer starts.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer StartDelayed(float delay)
        {
            if (delay <= 0f) return Start();

            _isDirty = false;
            _isDelayPhase = true;
            _delayDuration = delay;
            _timeRemaining = delay;
            _state = TimerState.Activated;
            
            if (ContainsTimer(this)) AddTimer();

            return this;
        }

        /// <summary>
        /// Primes the timer in a <see cref="TimerState.Suspended"/> state, requiring a manual resume to start.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer StartPaused()
        {
            Prime(TimerState.Suspended);
            Suspend();
            
            return this;
        }

        /// <summary>
        /// Stops the timer and triggers cancellation logic.
        /// </summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Stop()
        {
            Cancel();
            return this;
        }
        
        /// <summary>
        /// Associates a set of lifecycle callbacks with the timer.
        /// </summary>
        /// <param name="actions">A <see cref="TimerActions"/> container providing multi-stage callbacks.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer WithActions([NotNull] TimerActions actions) => SetActions(actions);

        #endregion Builder Pattern
    }

    /// <summary>
    /// A container for lifecycle callbacks used to handle various events throughout a timer's existence.
    /// </summary>
    public sealed class TimerActions
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerActions"/> class with empty callbacks.
        /// </summary>
        public TimerActions() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimerActions"/> class by copying delegates from an existing container.
        /// </summary>
        /// <param name="actions">The source <see cref="TimerActions"/> to copy from.</param>
        public TimerActions(TimerActions actions) => Set(actions);
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TimerActions"/> class with specific lifecycle delegates.
        /// </summary>
        /// <param name="onCompleted">The delegate to execute when the timer completes.</param>
        /// <param name="onUpdated">The delegate to execute every frame, providing completion percentage (0 to 1).</param>
        /// <param name="onSuspend">The delegate to execute when the timer is paused.</param>
        /// <param name="onResumed">The delegate to execute when the timer resumes.</param>
        /// <param name="onCancell">The delegate to execute if the timer is manually cancelled.</param>
        /// <param name="onRestart">The delegate to execute when the timer resets for a new loop.</param>
        public TimerActions(
            // Action onCompleted = null, 
            // Action<float> onUpdated = null, 
            Action onSuspend = null, 
            Action onResumed = null, 
            Action onCancell = null, 
            Action onRestart = null)
        {
            Set(onSuspend, onResumed, onCancell, onRestart);
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Clears all assigned callbacks by resetting them to empty delegates.
        /// </summary>
        public void Reset() => Set();

        /// <summary>
        /// Copies delegates from another <see cref="TimerActions"/> instance into this container.
        /// </summary>
        /// <param name="actions">The source container providing the callbacks.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided actions container is null.</exception>
        public void Set([NotNull] TimerActions actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            
            Set(
                // actions.Completed,
                // actions.Updated,
                actions.Suspend,
                actions.Resumed,
                actions.Cancell,
                actions.Restart);
        }

        /// <summary>
        /// Assigns specific delegates to the container's lifecycle events.
        /// </summary>
        /// <remarks>
        /// Null parameters are automatically replaced with empty delegates to prevent <see cref="NullReferenceException"/> during invocation.
        /// </remarks>
        /// <param name="onCompleted">The delegate to execute when the timer completes.</param>
        /// <param name="onUpdated">The delegate to execute every frame, providing completion percentage (0 to 1).</param>
        /// <param name="onSuspend">The delegate to execute when the timer is paused.</param>
        /// <param name="onResumed">The delegate to execute when the timer resumes.</param>
        /// <param name="onCancell">The delegate to execute if the timer is manually cancelled.</param>
        /// <param name="onRestart">The delegate to execute when the timer resets for a new loop.</param>
        public void Set(
            // Action onCompleted = null, 
            // Action<float> onUpdated = null, 
            Action onSuspend = null, 
            Action onResumed = null, 
            Action onCancell = null, 
            Action onRestart = null)
        {
            // Completed = onCompleted ?? delegate { };
            // Updated = onUpdated ?? delegate { };
            Suspend = onSuspend ?? delegate { };
            Resumed = onResumed ?? delegate { };
            Cancell = onCancell ?? delegate { };
            Restart = onRestart ?? delegate { };
        }

        #endregion Methods

        #region Events

        /// <summary>
        /// The delegate executed when the timer reaches zero or is forced to complete.
        /// </summary>
        // public Action Completed = delegate { };

        /// <summary>
        /// The delegate executed every frame while the timer is active. 
        /// Provides a normalized value (0 to 1) representing completion progress.
        /// </summary>
        // public Action<float> Updated = delegate { };

        /// <summary>
        /// The delegate executed when the timer enters a suspended state, either manually or automatically.
        /// </summary>
        public Action Suspend = delegate { };

        /// <summary>
        /// The delegate executed when a suspended timer is returned to an active state.
        /// </summary>
        public Action Resumed = delegate { };

        /// <summary>
        /// The delegate executed when the timer is explicitly stopped via a Cancel or Stop command.
        /// </summary>
        public Action Cancell = delegate { };

        /// <summary>
        /// The delegate executed when the timer resets its duration, typically during a loop cycle.
        /// </summary>
        public Action Restart = delegate { };

        #endregion Events
    }
    
    /// <summary>
    /// Defines the various operational states a <see cref="Timer"/> can occupy during its lifecycle.
    /// </summary>
    public enum TimerState
    {
        /// <summary>
        /// The timer is inactive and will not be processed by the manager.
        /// </summary>
        Disable = 0,
        
        /// <summary>
        /// The timer has been initialized but has not yet begun its first countdown tick.
        /// </summary>
        Enabled = 1,

        /// <summary>
        /// The timer is currently active and progressing toward completion.
        /// </summary>
        Activated = 2,

        /// <summary>
        /// The timer is paused. It retains its current progress but does not decrement time.
        /// </summary>
        Suspended = 3,

        /// <summary>
        /// The timer has been prematurely stopped and will no longer be processed.
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// The timer has successfully reached its target duration.
        /// </summary>
        Completed = 5
    }
}
