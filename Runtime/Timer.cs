using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// A high-performance, pooled timing utility designed for Data-Oriented (DOD) execution.
    /// </summary>
    /// <remarks>
    /// The <see cref="Timer"/> class provides a zero-allocation way to manage time-based 
    /// operations in Unity. It integrates with a centralized <see cref="TimerManager"/> 
    /// to minimize Update overhead and utilizes the <see cref="IPoolableClass{T}"/> 
    /// interface to prevent memory fragmentation through object recycling.
    /// 
    /// <para><b>Key Features:</b></para>
    /// <list type="bullet">
    /// <item><b>Fluent API:</b> Support for method chaining (e.g., <c>Timer.Record(5f).Loop().Start()</c>).</item>
    /// <item><b>Unscaled Support:</b> Toggleable between <c>Time.deltaTime</c> and <c>Time.unscaledDeltaTime</c>.</item>
    /// <item><b>Sequencing:</b> Can be used with <see cref="TimerSequence"/> for complex, multi-step logic chains.</item>
    /// <item><b>Group Tagging:</b> Supports integer-based Group IDs for bulk cancellation and management.</item>
    /// <item><b>Lifecycle Binding:</b> Can be bound to a <see cref="MonoBehaviour"/>
    /// to automatically cancel when the object is destroyed.
    /// </item>
    /// </list>
    /// </remarks>
    [Serializable]
    public sealed class Timer : IComparable<Timer>, IPoolableClass<Timer>
    {
        #region Fields

        [Header(TimerLib.Headers.State)]
        [SerializeField] [Tooltip(TimerLib.Tooltips.State)]           private TimerState _state = TimerState.Disable;
        [SerializeField] [Tooltip(TimerLib.Tooltips.GroupId)]         private int  _groupId;
        [SerializeField] [Tooltip(TimerLib.Tooltips.IsDirty)]         private bool _isDirty;
        [SerializeField] [Tooltip(TimerLib.Tooltips.CanLoop)]         private bool _canLoop;
        [SerializeField] [Tooltip(TimerLib.Tooltips.HasReference)]    private bool _hasReference;
        [SerializeField] [Tooltip(TimerLib.Tooltips.UseUnscaledTime)] private bool _useUnscaledTime;
        [SerializeField] [Tooltip(TimerLib.Tooltips.IsSuspendedManually)] private bool _isSuspendedManually;
        
        [Header(TimerLib.Headers.Data)]
        [SerializeField] [Tooltip(TimerLib.Tooltips.Duration)]      [ReadOnly] private float _duration;
        [SerializeField] [Tooltip(TimerLib.Tooltips.SuspendedTime)] [ReadOnly] private float _suspendedTime;
        [SerializeField] [Tooltip(TimerLib.Tooltips.TimeRemaining)] [ReadOnly] private float _timeRemaining;
        [Space]
        [SerializeField] [Tooltip(TimerLib.Tooltips.IsDelayPhase)]  [ReadOnly] private bool  _isDelayPhase;
        [SerializeField] [Tooltip(TimerLib.Tooltips.DelayDuration)] [ReadOnly] private float _delayDuration;

        private ClassObjectPool<Timer> _pool;
        
        private Action        _onCompleted;
        private Action<float> _onUpdated;

        private TimerActions  _actions;
        private MonoBehaviour _behaviour;
        private float         _durationReciprocal;

        #endregion Fields

        #region Constructors

        public Timer() { }
        
        /// <summary>Initializes a new instance of the <see cref="Timer"/> class.</summary>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <remarks>
        /// This constructor chains to the primary constructor with the looping parameter set to <c>false</c> by default.
        /// Use this for one-shot timers that need to ignore or respect the Unity Time Scale.
        /// </remarks>
        public Timer(bool isUnscaled = false) : this(false, isUnscaled) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with specific looping and time-scaling behaviors.
        /// </summary>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <remarks>
        /// This constructor sets the timer to the <see cref="TimerState.Enabled"/> state, allowing it to be 
        /// added to the <c>TimerManager</c> for processing. Note that a duration must still be set 
        /// (via <see cref="SetDuration"/> or similar) before the timer can effectively progress.
        /// </remarks>
        public Timer(bool hasLoop, bool isUnscaled)
        {
            _state           = TimerState.Enabled;
            _canLoop         = hasLoop;
            _useUnscaledTime = isUnscaled;
        }

        /// <summary>Initializes a new instance of the <see cref="Timer"/> class with a specified duration.</summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>Initializes a new instance of the <see cref="Timer"/> class with a completion callback.</summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="action">The delegate to execute when the timer finishes.</param>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer(float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            Set(time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with a comprehensive set of callbacks.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="timerActions">A <see cref="TimerActions"/> container providing multi-stage callbacks,
        /// such as Start, Update, and Cancel.
        /// </param>
        /// <param name="hasLoop">Whether the timer should automatically restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer(float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            Set(time, timerActions, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class bound to a <see cref="MonoBehaviour"/> lifecycle.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer([NotNull] MonoBehaviour behaviour, bool isUnscaled = false)
            : this(behaviour, false, isUnscaled) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with an owner, looping, and time-scaling settings.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer([NotNull] MonoBehaviour behaviour, bool hasLoop, bool isUnscaled)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            SetBehaviour(behaviour);
            
            _state           = TimerState.Enabled;
            _canLoop         = hasLoop;
            _useUnscaledTime = isUnscaled;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with an owner and a duration.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer([NotNull] MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
    
            Set(behaviour, time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class with an owner, duration, and completion callback.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="action">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer(
            [NotNull] MonoBehaviour behaviour,
            float time,
            Action action,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            Set(behaviour, time, action, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Timer"/> class
        /// with an owner, duration, and multiple lifecycle callbacks.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.</param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="timerActions">A <see cref="TimerActions"/> container providing multi-stage callbacks,
        /// such as Start, Update, and Cancel.
        /// </param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public Timer(
            [NotNull] MonoBehaviour behaviour,
            float time,
            TimerActions timerActions,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
    
            Set(behaviour, time, timerActions, hasLoop, isUnscaled);
        }

        #endregion Constructors
        
        #region Properties
        
        /// <summary>The delegate executed when the timer reaches zero or is forced to complete.</summary>
        public event Action Completed
        {
            add    => _onCompleted += value;
            remove => _onCompleted -= value;
        }
        
        /// <summary>
        /// The delegate executed every frame while the timer is active.
        /// Provides a normalized value (0–1) representing completion progress.
        /// </summary>
        public event Action<float> Updated
        {
            add    => _onUpdated += value;
            remove => _onUpdated -= value;
        }

        /// <summary>Gets or sets the container for lifecycle callbacks. Lazily initialized.</summary>
        public TimerActions Actions
        {
            get => _actions ??= new TimerActions();
            set => _actions = value;
        }

        /// <summary>The <see cref="MonoBehaviour"/> owner of this timer used for lifecycle binding.</summary>
        public MonoBehaviour Behaviour => _behaviour;

        /// <summary>The total duration assigned to the timer in seconds.</summary>
        public float Duration => _duration;

        /// <summary>The amount of time in seconds that has passed since the timer started.</summary>
        public float ElapsedTime => _duration - _timeRemaining;
        
        /// <summary>The category ID assigned to this timer for bulk operations. Defaults to 0 (uncategorized).</summary>
        public int GroupId => _groupId;

        /// <summary>Indicates whether the timer is configured to restart automatically upon completion.</summary>
        public bool HasLoop => _canLoop;

        /// <summary>Indicates whether this timer is bound to a <see cref="MonoBehaviour"/> reference.</summary>
        public bool HasReference => _hasReference;

        /// <summary>Indicates whether the timer uses <c>Time.unscaledDeltaTime</c> for its calculations.</summary>
        public bool HasUnscaledTime => _useUnscaledTime;

        /// <summary>Indicates whether the timer has been moved to the <see cref="TimerState.Cancelled"/> state.</summary>
        public bool IsCancelled => _state == TimerState.Cancelled;

        /// <summary>Indicates whether the timer is currently waiting out its initial delay period.</summary>
        public bool IsDelayPhase => _isDelayPhase;

        /// <summary>Returns true if the associated <see cref="MonoBehaviour"/> owner has been destroyed.</summary>
        public bool IsDestroyed => !_behaviour;

        /// <summary>Indicates whether the timer has reached its conclusion.</summary>
        /// <remarks>
        /// This property returns <c>false</c> if the timer is currently cancelled, suspended, or in a delay phase. 
        /// It returns <c>true</c> if the timer has been flagged as dirty or if the remaining time has lapsed.
        /// </remarks>
        public bool IsDone
        {
            get
            {
                if (_state is TimerState.Disable
                           or TimerState.Cancelled
                           or TimerState.Suspended) return false;
                
                if (_isDirty) return !_isDelayPhase;

                return !_isDelayPhase && _timeRemaining <= 0f;
            }
            private set => _isDirty = value;
        }

        /// <summary>Indicates whether the timer is currently in the <see cref="TimerState.Suspended"/> state.</summary>
        public bool IsSuspended => _state == TimerState.Suspended;

        /// <summary>
        /// Indicates whether the timer was paused manually by user logic rather than a system-level event.
        /// </summary>
        public bool IsSuspendedManually => _isSuspendedManually;
        
        /// <summary>Provides global access to the <see cref="TimerManager"/> singleton instance.</summary>
        public static TimerManager Manager => TimerManager.Instance;

        /// <summary>The current operational <see cref="TimerState"/> of this instance.</summary>
        public TimerState State => _state;

        /// <summary>
        /// Gets the current game time, choosing between scaled and unscaled time based on configuration.
        /// </summary>
        public float Time => _useUnscaledTime ? StaticTime.UnscaledTime : StaticTime.ScaledTime;

        /// <summary>The amount of time in seconds remaining until the timer completes its current cycle.</summary>
        public float TimeRemaining => _timeRemaining;

        #endregion Properties

        #region Pool Methods

        /// <summary>
        /// The pool this timer was issued from. <c>null</c> for timers that were allocated
        /// directly rather than obtained via <see cref="ClassObjectPool{T}.Get"/>.
        /// </summary>
        public ClassObjectPool<Timer> Pool => _pool;

        /// <summary>
        /// Copies template configuration from <paramref name="source"/> into this
        /// instance. Called by <see cref="ClassObjectPool{T}"/> during allocation
        /// to apply the <see cref="TimerTemplate"/> asset's default settings.
        /// Only value-type configuration fields are copied — no callbacks, no owner
        /// reference, and no runtime state.
        /// </summary>
        /// <param name="source">
        /// The template timer whose serialized settings should be mirrored.
        /// Pass <c>null</c> to leave all fields at their constructor defaults.
        /// </param>
        public void Create(Timer source)
        {
            if (source is null) return;

            // Copy only the configuration fields exposed by the template.
            _groupId = source._groupId;
            _canLoop = source._canLoop;
            _useUnscaledTime = source._useUnscaledTime;
        }

        /// <summary>
        /// Returns this timer to its originating pool. If the timer was not obtained from a pool, this is a no-op.
        /// </summary>
        /// <remarks>
        /// Do not call <see cref="Reset"/> before <see cref="DeInit"/> —
        /// the pool's <c>Despawn</c> callback is responsible for resetting
        /// the instance so that Reset runs exactly once per release.
        /// </remarks>
        public void DeInit() => _pool?.Release(this);

        /// <summary>Stores the pool reference so this timer can self-release via <see cref="DeInit"/>.</summary>
        /// <param name="pool">The pool that owns this instance.</param>
        public void OnInit(ClassObjectPool<Timer> pool) => _pool = pool;

        #endregion Pool Methods

        #region Helper Methods

        private static Timer GetPooledTimer()
        {
            var manager = Manager;
            return manager ? manager.RentTimer() : new Timer();
        }

        #endregion

        #region Methods

        /// <summary>Registers this timer with the <see cref="TimerManager"/> for active processing.</summary>
        private void AddTimer()
        {
            var manager = Manager;
            if (manager) manager.AddTimer(this);
        }

        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> bound to a <see cref="MonoBehaviour"/> lifecycle.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(behaviour, 0f, false, isUnscaled);
            
            return timer;
        }

        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/>
        /// with specific looping and scaling settings, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
            /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind([NotNull] MonoBehaviour behaviour, bool hasLoop, bool isUnscaled)
        {
            var timer = GetPooledTimer();
            timer.Set(behaviour, 0f, hasLoop, isUnscaled);
            
            return timer;
        }
        
        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/> with a duration, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
            /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind(
            [NotNull] MonoBehaviour behaviour,
            float time,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(behaviour, time, hasLoop, isUnscaled);
            
            return timer;
        }
        
        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/>
        /// with a duration and completion callback, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
            /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="onCompleted">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind(
            [NotNull] MonoBehaviour behaviour,
            float time,
            Action onCompleted,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(behaviour, time, onCompleted, hasLoop, isUnscaled);
            
            return timer;
        }
        
        /// <summary>
        /// Static factory method to create and return a <see cref="Timer"/>
        /// with a duration and multiple lifecycle callbacks, bound to an owner.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
            /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="callbacks">A <see cref="TimerActions"/> container providing multi-stage callbacks,
        /// such as Start, Update, and Cancel.
        /// </param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Bind(
            [NotNull] MonoBehaviour behaviour,
            float time,
            TimerActions callbacks,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(behaviour, time, callbacks, hasLoop, isUnscaled);
            
            return timer;
        }

        /// <summary>Stops the timer immediately and triggers the cancellation callback.</summary>
        public void Cancel()
        {
            _state = TimerState.Cancelled;
            _actions?.Cancel?.Invoke();
        }

        /// <inheritdoc cref="TimerManager.CancelAllTimers()"/>
        public static void CancelAllTimers()
        {
            var manager = Manager;
            if (manager) manager.CancelAllTimers();
        }
        
        /// <summary>Globally cancels all timers matching the specified Group ID.</summary>
        public static void CancelAllTimers(int groupId)
        {
            var manager = Manager;
            if (manager) manager.CancelAllTimers(groupId);
        }

        /// <inheritdoc cref="TimerManager.CancelAllTimers(MonoBehaviour)"/>
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
        /// Handles automatic suspension/resumption based on the owner <see cref="MonoBehaviour"/> state. 
        /// Triggers <see cref="Cancel"/> if the owner has been destroyed.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanProcess()
        {
            if (!_hasReference) return _state != TimerState.Suspended;

            if (!_behaviour)
            {
                Cancel();
                return false;
            }

            if (_behaviour.isActiveAndEnabled)
            {
                if (_state == TimerState.Suspended && !_isSuspendedManually) Resume();
                return _state != TimerState.Suspended;
            }
            
            if (_state != TimerState.Suspended) Suspend( false); 
            return false;
        }

        /// <summary>
        /// Compares the remaining time with another timer to facilitate sorting within the <see cref="TimerManager"/>.
        /// </summary>
        public int CompareTo(Timer other) => _timeRemaining.CompareTo(other._timeRemaining);
        
        /// <summary>Checks if the specified timer is tracked by the <see cref="TimerManager"/>.</summary>
        public static bool ContainsTimer([NotNull] Timer timer)
        {
            var manager = Manager;
            return manager && manager.ContainsTimer(timer);
        }

        /// <summary>Transitions the timer to <see cref="TimerState.Completed"/> and invokes completion delegates.</summary>
        public void ExecuteComplete()
        {
            SetState(TimerState.Completed);
            
            _onCompleted?.Invoke();
        }

        /// <summary>Resets the timer to its full duration and restarts the countdown.</summary>
        public void ExecuteRestart()
        {
            var overflow   = _timeRemaining;
            _isDirty       = false;
            _timeRemaining = _duration + overflow;
            _state         = TimerState.Activated;
            
            _actions?.Restart?.Invoke();
        }

        /// <summary>Invokes update delegates with the current normalized progress (0–1).</summary>
        public void ExecuteUpdate()
        {
            var percentDone = PercentageDone();
            
            _onUpdated?.Invoke(percentDone);
        }

        /// <summary>Adds time to the current duration and remaining time.</summary>
        /// <param name="addedTime">Amount in seconds to add. Ignored if ≤ 0.</param>
        public void ExtendDuration(float addedTime)
        {
            if (addedTime <= 0f) return;
            
            _duration      += addedTime;
            _timeRemaining += addedTime;
            _durationReciprocal = 1f / _duration;

            if (!(_timeRemaining > 0f)) return;
            
            _isDirty = false;
            
            if (_state == TimerState.Completed) _state = TimerState.Activated;
        }

        /// <summary>Forces the timer to reach its end state immediately.</summary>
        public void ForceCompletion()
        {
            _timeRemaining = 0f;
            IsDone = true;
        }

        /// <summary>Centralized internal method to set the core operational parameters of the timer.</summary>
        private void Initialize(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            _duration           = time;
            _durationReciprocal = time > 0f ? 1f / time : 0f;
            _timeRemaining      = time;
            _canLoop            = hasLoop;
            _useUnscaledTime    = isUnscaled;
            _state              = TimerState.Enabled;
        }

        /// <summary>Updates the timer with a new total duration and resets progress.</summary>
        /// <param name="duration">The new duration in seconds.</param>
        public void NewDuration(float duration)
        {
            _duration = duration;

            var shouldActivate = _state is TimerState.Activated or TimerState.Completed;
            var targetState    = shouldActivate ? TimerState.Activated : TimerState.Disable;
            
            ResetProgress(targetState);

            if (targetState != TimerState.Activated) return;
            if (ContainsTimer(this)) AddTimer();
        }

        /// <summary>Returns normalized completion progress clamped to [0, 1].</summary>
        /// <returns>A value between 0 and 1, where 1 indicates the timer has finished.</returns>
        public float PercentageDone()
        {
            return _duration <= 0f ? 1f : Mathf.Clamp01(1f - _timeRemaining * _durationReciprocal);
        }

        /// <summary>Returns completion progress using SmoothStep easing.</summary>
        /// <returns>A value between 0 and 1 with smoothed easing at the start and end.</returns>
        public float PercentageDoneSmoothStep() => Mathf.SmoothStep(0f, 1f, PercentageDone());

        /// <summary>
        /// Handles transition out of the delay phase. Returns <c>true</c> if still in or transitioning from delay.
        /// </summary>
        public bool ProcessDelay()
        {
            if (!IsDelayPhase) return false;
            if (!IsDone)       return true;
            
            _isDelayPhase  = false;
            _timeRemaining = _duration;

            return true;
        }

        /// <summary>Static factory method to create and return a new <see cref="Timer"/> instance.</summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        public static Timer Record(float time, bool hasLoop = false, bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(time, hasLoop, isUnscaled);
            
            return timer;
        }

        /// <summary>
        /// Static factory method to create and return a new <see cref="Timer"/> instance with a completion callback.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="action">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Record(float time, Action action, bool hasLoop = false, bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(time, action, hasLoop, isUnscaled);
            
            return timer;
        }

        /// <summary>
        /// Static factory method to create and return a new <see cref="Timer"/> instance with multiple lifecycle callbacks.
        /// </summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="timerActions">A <see cref="TimerActions"/> container providing multi-stage callbacks,
        /// such as Start, Update, and Cancel.
        /// </param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public static Timer Record(float time, TimerActions timerActions, bool hasLoop = false, bool isUnscaled = false)
        {
            var timer = GetPooledTimer();
            timer.Set(time, timerActions, hasLoop, isUnscaled);
            
            return timer;
        }

        /// <summary>Resets the timer to its starting duration without clearing owner or callbacks.</summary>
        public void Reload() => ResetProgress(TimerState.Enabled, _duration);

        /// <summary>
        /// Fully resets the timer, clearing all data, references, and callbacks.
        /// Prepares the instance for object pooling.
        /// </summary>
        public void Reset()
        {
            // Order matters: Reset configuration (Duration = 0) before Progress
            ResetConfiguration();
            ResetBehaviour();
            ResetProgress(duration: 0f);
            
            _actions?.Reset();
            _onUpdated   = null;
            _onCompleted = null;
        }

        /// <summary>Clears the <see cref="MonoBehaviour"/> owner reference and disables lifecycle tracking.</summary>
        private void ResetBehaviour()
        {
            _behaviour    = null;
            _hasReference = false;
        }

        /// <summary>Resets operational flags and duration to their default values.</summary>
        private void ResetConfiguration()
        {
            _duration            = 0f;
            _durationReciprocal  = 0f;
            _groupId             = 0;
            _canLoop             = false;
            _useUnscaledTime     = false;
            _isSuspendedManually = false;
        }

        /// <summary>Resets temporal progress and state.</summary>
        /// <param name="state">The <see cref="TimerState"/> to apply after reset.</param>
        /// <param name="duration">
        /// Optional duration override. Pass <c>null</c> to retain the current <c>_duration</c>.
        /// Explicitly passing <c>0f</c> sets remaining time to zero.
        /// </param>
        private void ResetProgress(TimerState state = TimerState.Disable, float? duration = null)
        {
            _state              = state;
            _isDirty            = false;
            _isDelayPhase       = false;
            _delayDuration      = 0f;
            _suspendedTime      = 0f;
            
            // Hard-reset (Object pool), duration is 0.
            // Re-started (Loop/Reload), duration is the current _duration.
            _timeRemaining      = duration ?? _duration;
            
            // Reciprocal is used for PercentageDone().
            _durationReciprocal = _timeRemaining > 0f ? 1f / _timeRemaining : 0f;
        }

        /// <summary>Restarts the timer using its current duration.</summary>
        public void Restart() => NewDuration(_duration);

        /// <summary>Restarts the timer with a new duration.</summary>
        /// <param name="duration">The new duration of the timer in seconds.</param>
        public void Restart(float duration) => NewDuration(duration);

        /// <summary>Resumes a suspended timer and triggers the resumption callback.</summary>
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
        
        /// <summary>Globally resumes all suspended timers matching the specified Group ID.</summary>
        public static void ResumeAllTimers(int groupId)
        {
            var manager = Manager;
            if (manager) manager.ResumeAllTimers(groupId);
        }

        /// <summary>
        /// Globally resumes all timers associated with
        /// a specific <see cref="MonoBehaviour"/> that were not manually suspended.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to resume.</param>
        public static void ResumeAllTimers(MonoBehaviour behaviour)
        {
            var manager = Manager;
            if (manager) manager.ResumeAllTimers(behaviour);
        }

        /// <summary>Configures the timer with a duration and operational settings.</summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        private void Set(float time, bool hasLoop = false, bool isUnscaled = false) 
            => Initialize(time, hasLoop, isUnscaled);

        /// <summary>Configures the timer with a duration, completion callback, and operational settings.</summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="onComplete">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        private void Set(float time, Action onComplete, bool hasLoop = false, bool isUnscaled = false)
        {
            _onCompleted = onComplete;
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>Configures the timer with a duration, multiple lifecycle callbacks, and operational settings.</summary>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="callbacks">A <see cref="TimerActions"/> container providing multi-stage callbacks,
        /// such as Start, Update, and Cancel.
        /// </param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        private void Set(float time, TimerActions callbacks, bool hasLoop = false, bool isUnscaled = false)
        {
            Actions.Set(callbacks);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>Configures the timer with an owner, duration, and operational settings.</summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        private void Set(MonoBehaviour behaviour, float time, bool hasLoop = false, bool isUnscaled = false)
        {
            SetBehaviour(behaviour);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>Configures the timer with an owner, duration, completion callback, and operational settings.</summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="onComplete">The delegate to execute when the timer completes.</param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        private void Set(
            MonoBehaviour behaviour,
            float time,
            Action onComplete,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            _onCompleted = onComplete;
            SetBehaviour(behaviour);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>
        /// Configures the timer with an owner, duration, multiple lifecycle callbacks, and operational settings.
        /// </summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <param name="time">The duration of the timer in seconds.</param>
        /// <param name="callbacks">A <see cref="TimerActions"/> container providing multi-stage callbacks,
        /// such as Start, Update, and Cancel.
        /// </param>
        /// <param name="hasLoop">Determines if the timer should automatically reset and restart upon completion.</param>
        /// <param name="isUnscaled">Determines if the timer should use <c>Time.unscaledDeltaTime</c>
        /// instead of the standard game clock.
        /// </param>
        private void Set(
            MonoBehaviour behaviour,
            float time,
            TimerActions callbacks,
            bool hasLoop = false,
            bool isUnscaled = false)
        {
            Actions.Set(callbacks);
            SetBehaviour(behaviour);
            Initialize(time, hasLoop, isUnscaled);
        }

        /// <summary>Directly sets the operational state of the timer.</summary>
        /// <param name="state">The new <see cref="TimerState"/> to apply.</param>
        public void SetState(TimerState state) => _state = state;

        /// <summary>Pauses the timer and triggers the suspension callback.</summary>
        /// <param name="isManual"><c>true</c> if paused by user logic; <c>false</c> if paused by a system event.</param>
        public void Suspend(bool isManual = true)
        {
            _isSuspendedManually = isManual;
            _state = TimerState.Suspended;
            _actions?.Suspend?.Invoke();
        }

        /// <summary>Globally suspends all active timers managed by the <see cref="TimerManager"/>.</summary>
        public static void SuspendAllTimers()
        {
            var manager = Manager;
            if (manager) manager.SuspendAllTimers();
        }
        
        /// <summary>Globally suspends all active timers matching the specified Group ID.</summary>
        public static void SuspendAllTimers(int groupId)
        {
            var manager = Manager;
            if (manager) manager.SuspendAllTimers(groupId);
        }

        /// <summary>Globally suspends all timers associated with a specific <see cref="MonoBehaviour"/>.</summary>
        /// <param name="behaviour">The owner of the timers to suspend.</param>
        public static void SuspendAllTimers(MonoBehaviour behaviour)
        {
            var manager = Manager;
            if (manager) manager.SuspendAllTimers(behaviour);
        }

        /// <summary>Progresses the timer countdown and handles transitions between delay and active phases.</summary>
        /// <param name="deltaTime">The time slice to subtract from the remaining duration,
        /// typically provided by the <see cref="TimerManager"/>.
        /// </param>
        /// <remarks>
        /// This method only executes if the timer is in the <see cref="TimerState.Activated"/> state. 
        /// If a delay phase is active, it calculates the overflow time to ensure the transition to the 
        /// main duration remains temporally accurate.
        /// </remarks>
        public void Tick(float deltaTime)
        {
            if (_state != TimerState.Activated) return;
            
            _timeRemaining -= deltaTime;
            
            if (!_isDelayPhase || _timeRemaining > 0f) return;
            
            _timeRemaining = _duration + _timeRemaining;
            _isDelayPhase  = false;
        }

        #endregion Methods
    
        #region Builder Pattern

        /// <summary>Initializes and returns a new <see cref="Timer"/> instance for further configuration.</summary>
        /// <returns>A new <see cref="Timer"/> instance.</returns>
        public Timer Build() => GetPooledTimer();
        
        /// <summary>Associates the timer with a <see cref="MonoBehaviour"/> owner for lifecycle tracking.</summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer BindTo([NotNull] MonoBehaviour behaviour) => SetBehaviour(behaviour);
        
        /// <summary>Configures the timer to follow standard game time scaling.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer GameTime() => SetUnscaledTime(false);
        
        /// <summary>Assigns a group ID to the timer for bulk cancellation or suspension.</summary>
        /// <param name="groupId">The integer ID representing the group.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Group(int groupId)
        {
            _groupId = groupId;
            return this;
        }
        
        /// <summary>Sets the total duration of the timer.</summary>
        /// <param name="duration">The duration of the timer in seconds.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Length(float duration) => SetDuration(duration);
        
        /// <summary>Configures the timer to automatically reset and restart upon completion.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Loop() => SetLoop(true);
        
        /// <summary>Configures the timer to execute only once without looping.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Once() => SetLoop(false);

        /// <summary>Adds a callback to be executed when the timer is cancelled.</summary>
        /// <param name="action">The delegate to execute upon cancellation.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnCancel(Action action)
        {
            Actions.Cancel += action;
            return this;
        }

        /// <summary>Adds a callback to be executed when the timer reaches completion.</summary>
        /// <param name="action">The delegate to execute upon completion.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnCompleted(Action action)
        {
            _onCompleted += action;
            return this;
        }

        /// <summary>Adds a callback to be executed when the timer restarts.</summary>
        /// <param name="action">The delegate to execute upon restart.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnRestart(Action action)
        {
            Actions.Restart += action;
            return this;
        }

        /// <summary>Adds a callback to be executed when the timer resumes.</summary>
        /// <param name="action">The delegate to execute upon resumption.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnResumed(Action action)
        {
            Actions.Resumed += action;
            return this;
        }

        /// <summary>Adds a callback to be executed when the timer is suspended.</summary>
        /// <param name="action">The delegate to execute upon suspension.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnSuspend(Action action)
        {
            Actions.Suspend += action;
            return this;
        }

        /// <summary>Adds a callback to be executed every frame while the timer is active.</summary>
        /// <param name="action">The delegate to execute, providing the current completion percentage (0 to 1).</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer OnUpdated(Action<float> action)
        {
            _onUpdated += action;
            return this;
        }

        /// <summary>Pauses the timer's progression.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Pause()
        {
            Suspend();
            return this;
        }

        /// <summary>Prepares the timer state and register it with the manager.</summary>
        /// <param name="initialState">The <see cref="TimerState"/> to set upon priming.</param>
        /// <param name="initialDuration">The duration to initialize with. Pass null to use the current _duration.</param>
        private void Prime(TimerState initialState, float? initialDuration = null)
        {
            ResetProgress(initialState, initialDuration);
            
            if (!ContainsTimer(this)) AddTimer();
        }
        
        /// <summary>Configures the timer to use <c>Time.unscaledDeltaTime</c>.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Realtime() => SetUnscaledTime(true);

        /// <summary>Assigns a <see cref="TimerActions"/> container to the timer.</summary>
        /// <param name="actions">The container providing multi-stage callbacks.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided actions container is null.</exception>
        private Timer SetActions([NotNull] TimerActions actions)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            return this;
        }

        /// <summary>Assigns the owner of the timer and enables lifecycle tracking.</summary>
        /// <param name="behaviour">The owner of this timer.
        /// The timer will automatically invalidate if this <see cref="MonoBehaviour"/> is destroyed.
        /// </param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetBehaviour([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
    
            _behaviour    = behaviour;
            _hasReference = true;
    
            return this;
        }

        /// <summary>Sets the total duration of the timer.</summary>
        /// <param name="duration">The duration in seconds.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetDuration(float duration)
        {
            _duration = duration;
            return this;
        }

        /// <summary>Configures whether the timer should automatically restart upon completion.</summary>
        /// <param name="canLoop">Determines if the timer should loop.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetLoop(bool canLoop)
        {
            _canLoop = canLoop;
            return this;
        }

        /// <summary>Configures whether the timer should use unscaled or standard game time.</summary>
        /// <param name="useUnscaledTime">Determines if the timer should use <c>Time.unscaledDeltaTime</c>.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        private Timer SetUnscaledTime(bool useUnscaledTime)
        {
            _useUnscaledTime = useUnscaledTime;
            return this;
        }

        /// <summary>Activates the timer and begins the countdown.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Start()
        {
            Prime(TimerState.Activated);
            _actions?.Restart?.Invoke();
            
            return this;
        }

        /// <summary>Sets a new duration and activates the timer immediately.</summary>
        /// <param name="duration">The duration of the timer in seconds.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Start(float duration)
        {
            _duration = duration;
            
            Prime(TimerState.Activated, duration);
            _actions?.Restart?.Invoke();
            
            return this;
        }

        /// <summary>Activates the timer after a specified delay period.</summary>
        /// <param name="delay">The delay duration in seconds before the main timer starts.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer StartDelayed(float delay)
        {
            if (delay <= 0f) return Start();

            _isDirty       = false;
            _isDelayPhase  = true;
            _delayDuration = delay;
            _timeRemaining = delay;
            _state         = TimerState.Activated;
            
            if (!ContainsTimer(this)) AddTimer();

            return this;
        }

        /// <summary>Primes the timer in a suspended state, requiring a manual resume to begin.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer StartPaused()
        {
            Prime(TimerState.Suspended);
            Suspend();
            
            return this;
        }

        /// <summary>Stops the timer and triggers cancellation logic.</summary>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer Stop()
        {
            Cancel();
            return this;
        }
        
        /// <summary>Associates a set of lifecycle callbacks with the timer.</summary>
        /// <param name="actions">A <see cref="TimerActions"/> container providing multi-stage callbacks.</param>
        /// <returns>The current <see cref="Timer"/> instance for method chaining.</returns>
        public Timer WithActions([NotNull] TimerActions actions) => SetActions(actions);

        #endregion Builder Pattern
    }

    /// <summary>A container for lifecycle callbacks used throughout a <see cref="Timer"/>'s existence.</summary>
    public sealed class TimerActions
    {
        #region Constructors

        /// <summary>Initializes a new <see cref="TimerActions"/> instance with empty callbacks.</summary>
        public TimerActions() { }

        /// <summary>
        /// Initializes a new <see cref="TimerActions"/> by copying delegates from an existing instance.
        /// </summary>
        /// <param name="actions">The source <see cref="TimerActions"/> to copy from.</param>
        public TimerActions(TimerActions actions) => Set(actions);

        /// <summary>Initializes a new <see cref="TimerActions"/> with specific lifecycle delegates.</summary>
        /// <param name="onSuspend">Executes when the timer is paused.</param>
        /// <param name="onResumed">Executes when the timer resumes.</param>
        /// <param name="onCancel" >Executes when the timer is manually cancelled.</param>
        /// <param name="onRestart">Executes when the timer resets for a new loop.</param>
        public TimerActions(
            Action onSuspend = null, 
            Action onResumed = null, 
            Action onCancel  = null, 
            Action onRestart = null)
        {
            Set(onSuspend, onResumed, onCancel, onRestart);
        }

        #endregion Constructors

        #region Methods

        /// <summary>Clears all assigned callbacks by resetting them to empty delegates.</summary>
        public void Reset() => Set();

        /// <summary>Copies delegates from another <see cref="TimerActions"/> instance.</summary>
        /// <param name="actions">The source container providing the callbacks.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="actions"/> is null.</exception>
        public void Set([NotNull] TimerActions actions)
        {
            if (actions is null) throw new ArgumentNullException(nameof(actions));
            Set(actions.Suspend, actions.Resumed, actions.Cancel, actions.Restart);
        }

        /// <summary>Assigns specific delegates to the container's lifecycle events.</summary>
        /// <remarks>
        /// Null parameters default to empty delegates to prevent <see cref="NullReferenceException"/> during invocation.
        /// </remarks>
        /// <param name="onSuspend">Executes when the timer is paused.</param>
        /// <param name="onResumed">Executes when the timer resumes.</param>
        /// <param name="onCancel" >Executes when the timer is manually cancelled.</param>
        /// <param name="onRestart">Executes when the timer resets for a new loop.</param>
        public void Set(
            Action onSuspend = null, 
            Action onResumed = null, 
            Action onCancel  = null, 
            Action onRestart = null)
        {
            Suspend = onSuspend ?? delegate { };
            Resumed = onResumed ?? delegate { };
            Cancel  = onCancel  ?? delegate { };
            Restart = onRestart ?? delegate { };
        }

        #endregion Methods

        #region Events

        /// <summary>
        /// Invoked when the timer is explicitly stopped
        /// via <see cref="Timer.Cancel"/> or <see cref="Timer.Stop"/>.
        /// </summary>
        public Action Cancel  = delegate { };

        /// <summary>Invoked when the timer resets its duration, typically during a loop cycle.</summary>
        public Action Restart = delegate { };

        /// <summary>Invoked when a suspended timer resumes.</summary>
        public Action Resumed = delegate { };

        /// <summary>Invoked when the timer enters a suspended state.</summary>
        public Action Suspend = delegate { };

        #endregion Events
    }
    
    /// <summary>Defines the operational states a <see cref="Timer"/> can occupy during its lifecycle.</summary>
    public enum TimerState
    {
        /// <summary>Inactive — not processed by the manager.</summary>
        Disable   = 0,
        /// <summary>Initialized but not yet ticking.</summary>
        Enabled   = 1,
        /// <summary>Actively counting down.</summary>
        Activated = 2,
        /// <summary>Paused — retains progress but does not decrement.</summary>
        Suspended = 3,
        /// <summary>Prematurely stopped — no longer processed.</summary>
        Cancelled = 4,
        /// <summary>Successfully reached target duration.</summary>
        Completed = 5
    }
}
