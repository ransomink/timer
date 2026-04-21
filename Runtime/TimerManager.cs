using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// A centralized system for managing, updating, and optimizing the execution of
    /// multiple <see cref="Timer"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This manager utilizes a <see cref="ScriptableSingleton{T}"/> pattern to provide
    /// global access while remaining persistent across scenes. It implements
    /// <see cref="IUpdate"/> to integrate with a central update loop, reducing the
    /// overhead of having multiple <c>Update</c> calls in the scene.
    /// </para>
    /// <para>
    /// <b>Performance Features:</b>
    /// <list type="bullet">
    /// <item><b>Raw Array Iteration:</b> The active timer collection is backed by a
    /// plain <c>Timer[]</c> rather than <c>List&lt;Timer&gt;</c>, eliminating
    /// per-element bounds-check overhead in the update loop.</item>
    /// <item><b>Span Traversal:</b> <c>OnUpdate</c> slices the live portion of the
    /// backing array into a <c>Span&lt;Timer&gt;</c>, removing the remaining indexer
    /// indirection at the call site.</item>
    /// <item><b>O(1) Containment:</b> A parallel <c>HashSet&lt;Timer&gt;</c> makes
    /// <see cref="ContainsTimer"/> and duplicate-add prevention O(1) instead of O(N).
    /// </item>
    /// <item><b>Throttled Sorting:</b> Re-prioritizes the timer collection based on
    /// frame or time intervals rather than every frame.</item>
    /// <item><b>O(1) Removals:</b> Uses a swap-with-last technique to discard finished
    /// timers without shifting array elements.</item>
    /// <item><b>Timer Object Pool:</b> Recycles <see cref="Timer"/> instances via an
    /// internal <c>Stack&lt;Timer&gt;</c>, eliminating per-timer heap allocation and
    /// GC pressure in steady-state gameplay.</item>
    /// </list>
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = TimerLib.Folders.TimerFileName, 
        menuName = TimerLib.Folders.TimerMenuName, 
        order = 0
    )]
    public sealed class TimerManager : ScriptableSingleton<TimerManager>, IUpdate
    {
        #region Fields
        
        private const int DefaultCapacity = 1024;
        
        private readonly HashSet<Timer> _timerSet = new HashSet<Timer>(DefaultCapacity);

        [Header(TimerLib.Headers.Config)]
        [SerializeField][Tooltip(TimerLib.Tooltips.UseFrames)] private bool _useFrames = true;
        [SerializeField][Tooltip(TimerLib.Tooltips.IntervalFrames)] private int _intervalFrames = 60;
        
        [Space]
        [SerializeField][Tooltip(TimerLib.Tooltips.UseSeconds)] private bool _useSeconds;
        [SerializeField][Tooltip(TimerLib.Tooltips.IntervalSeconds)] private float _intervalSeconds = 1f;
        
        [Space]
        [Header("Pool")]
        [SerializeField]
        [Tooltip("Assign a TimerTemplate asset to enable ClassObjectPool-backed " +
                 "Timer recycling with Inspector-configurable defaults.")]
        private TimerTemplate _timerTemplate;

        private Timer[]                _timerBuffer = new Timer[DefaultCapacity];
        private int                    _timerCount;
        private ClassObjectPool<Timer> _timerPool;
        
        private bool  _isDirty;
        private int   _frameCounter;
        private float _timeSinceLastSort;
        
        #endregion Fields
        
        #region Properties
        
        public static bool IsQuittingApplication { get; private set; }
        
        /// <summary>
        /// Provides a read-only view over all currently active and managed Timers.
        /// </summary>
        /// <remarks>
        /// Returns an <see cref="ArraySegment{T}"/> wrapping the live slice of the
        /// internal backing array. <see cref="ArraySegment{T}"/> implements
        /// <see cref="IReadOnlyList{T}"/>, so existing call sites (.Count, [i]) work
        /// without modification. No allocation occurs on each access.
        /// </remarks>
        public ArraySegment<Timer> Timers => new ArraySegment<Timer>(_timerBuffer, 0, _timerCount);
        
        /// <summary>
        /// Number of <see cref="Timer"/> instances currently inactive in the pool.
        /// Returns 0 when pooling is disabled.
        /// </summary>
        public int PooledTimerCount => _timerPool?.Inactive ?? 0;
        
        /// <summary>
        /// Number of <see cref="Timer"/> instances currently active (spawned) from
        /// the pool. Returns 0 when pooling is disabled.
        /// </summary>
        public int ActivePooledTimerCount => _timerPool?.Active ?? 0;
        
        /// <summary>
        /// Whether the <see cref="ClassObjectPool{T}"/> has been initialized.
        /// </summary>
        public bool IsPoolReady => _timerPool != null;
        
        #endregion Properties

        #region Unity Callbacks

        private void OnValidate()
        {
            if (_useFrames) _useSeconds = false;
            if (_useSeconds) _useFrames = false;
        }

        /// <summary>
        /// Processes and updates all active Timers managed by this system.
        /// </summary>
        /// <param name="deltaTime">The current game clock delta time.</param>
        /// <remarks>
        /// Iterates the live timer slice in reverse via <see cref="Span{T}"/> for
        /// bounds-check-free access. Removal is safe because
        /// <see cref="RemoveAtFast"/> swaps the removed slot with the last element,
        /// and reverse iteration re-visits the swapped element on the next step.
        /// <br/>
        /// <b>Re-entrancy:</b> Not re-entrant. Callbacks must not add or remove
        /// timers during <c>OnUpdate</c>.
        /// </remarks>
        public void OnUpdate(float deltaTime)
        {
            if (_isDirty) HandleSorting(deltaTime);
            if (_timerCount == 0) return;
            
            var unscaledDeltaTime = StaticTime.UnscaledDeltaTime;
            var span = _timerBuffer.AsSpan(0, _timerCount);

            for (var i = span.Length - 1; i >= 0; --i)
            {
                var timer = span[i];

                if (IsInvalid(timer)) { RemoveAtFast(i); continue; }
                if (!timer.CanProcess()) continue;
                
                var dt = timer.HasUnscaledTime ? unscaledDeltaTime : deltaTime;
                timer.Tick(dt);
                
                if (!timer.IsDone)
                {
                    if (!timer.IsDelayPhase) timer.ExecuteUpdate();
                    continue;
                }
                
                timer.ExecuteUpdate();
                timer.ExecuteComplete();
                
                if (!timer.HasLoop) { RemoveAtFast(i); continue; }
                
                timer.ExecuteRestart();
                
                _isDirty = true;
            }
        }
        
        private void OnApplicationQuit() => IsQuittingApplication = true;
        
        #endregion Unity Callbacks

        #region Lifecycle Hooks

        protected override void OnAfterInitialized()
        {
            IsQuittingApplication = false;
        }

        protected override void OnInstanceReady()
        {
            InitPool();
            
            if (Application.isPlaying && UpdateDispatcher.Instance)
                UpdateDispatcher.Instance.AddBaseUpdate(this);
        }
        
        protected override void OnShutdown()
        {
            if (Application.isPlaying && UpdateDispatcher.Instance)
                UpdateDispatcher.Instance.RemoveBaseUpdate(this);
            
            ClearTimerStorage();
            
            _timerPool?.Dispose();
            _timerPool = null;
        }

        #endregion Lifecycle Hooks

        #region Pool Methods

        /// <summary>
        /// Initializes the <see cref="ClassObjectPool{T}"/> from the assigned
        /// <see cref="TimerTemplate"/>. Called automatically by
        /// <see cref="OnInstanceReady"/>.
        /// </summary>
        private void InitPool()
        {
            if (!_timerTemplate) return;

            _timerPool = new ClassObjectPool<Timer>(
                prefab: _timerTemplate,
                preload: DefaultCapacity / 2,
                capacity: DefaultCapacity,
                canExpand: true,
                canRecycle: false,
                onCreated: source =>
                {
                    var instance = new Timer();
                    instance.Create(source);
                    return instance;
                },
                onDespawn: timer => timer.Reset()
            );
        }

        /// <summary>
        /// Retrieves a <see cref="Timer"/> from the <see cref="ClassObjectPool{T}"/>,
        /// or allocates a plain <c>new Timer()</c> when pooling is disabled.
        /// </summary>
        /// <returns>
        /// A timer in <see cref="TimerState.Disable"/> state with template defaults
        /// applied. Configure it via the builder API before calling
        /// <see cref="Timer.Start()"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Timer RentTimer()
        {
            return _timerPool != null ? _timerPool.Get() : new Timer();
        }

        /// <summary>
        /// Returns a <see cref="Timer"/> to the pool for reuse.
        /// </summary>
        /// <param name="timer">The timer to return. Silently ignored if null.</param>
        /// <remarks>
        /// <see cref="Timer.Reset"/> is called automatically by the pool's
        /// <c>Despawn</c> callback — do not call it manually beforehand. Do not hold
        /// references to <paramref name="timer"/> after this call.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReturnTimer([NotNull] Timer timer)
        {
            if (timer is null || _timerPool is null) return;
            
            _timerPool.Release(timer);
        }

        /// <summary>
        /// Pre-warms the pool by pre-allocating the specified number of
        /// <see cref="Timer"/> instances. Call during scene load to prevent
        /// first-use allocation spikes.
        /// </summary>
        /// <param name="count">Number of instances to pre-allocate.</param>
        public void WarmPool(int count)
        {
            if (_timerPool is null)
            {
                DebugExtensions.Warn(
                    $"{"WarmPool".ToType()} called but no timerTemplate is assigned. " +
                    "Assign a TimerTemplate asset in the Inspector to enable pooling.");
                return;
            }
            
            _timerPool.PreCache(count);
        }

        #endregion Pool Methods

        #region Methods
        
        /// <summary>
        /// Registers a new Timer with the manager.
        /// </summary>
        /// <param name="timer">The Timer instance to add.</param>
        /// <remarks>
        /// Duplicate adds are silently ignored via the parallel <see cref="HashSet{T}"/>
        /// containment check, making this method idempotent. Adding a timer marks the
        /// manager dirty for the next scheduled sort.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTimer([NotNull] Timer timer)
        {
            if (!_timerSet.Add(timer)) return;
            if (_timerCount == _timerBuffer.Length)
            {
                Debug.LogError(
                    "<color=red><b>[ERROR]</b></color> " +
                    $"TimerManager backing array capacity ({_timerBuffer.Length}). " +
                    "exceeded. Resizing — consider increasing DefaultCapacity.");
                Array.Resize(ref _timerBuffer, _timerBuffer.Length * 2);
            }

            _timerBuffer[_timerCount++] = timer;
            _isDirty = true;
        }

        /// <summary>Cancels all currently active Timers.</summary>
        public void CancelAllTimers()
        {
            for (var i = _timerCount - 1; i >= 0; --i) _timerBuffer[i].Cancel();
        }

        /// <summary>
        /// Cancels all active Timers associated with a specific
        /// <see cref="MonoBehaviour"/>.
        /// </summary>
        public void CancelAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            for (var i = _timerCount - 1; i >= 0; --i)
            {
                var timer = _timerBuffer[i];
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Cancel();
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the timer is currently tracked by the manager.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsTimer([NotNull] Timer timer) => _timerSet.Contains(timer);

        /// <summary>Resumes all currently suspended Timers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResumeAllTimers()
        {
            for (var i = 0; i < _timerCount; ++i) _timerBuffer[i].Resume();
        }

        /// <summary>
        /// Resumes all suspended Timers associated with a specific
        /// <see cref="MonoBehaviour"/>.
        /// </summary>
        public void ResumeAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            for (var i = 0; i < _timerCount; ++i)
            {
                var timer = _timerBuffer[i];
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Resume();
            }
        }

        public void Shutdown() => OnShutdown();

        /// <summary>
        /// Sorts the active timer slice by ascending
        /// <see cref="Timer.TimeRemaining"/> using
        /// <see cref="Timer.CompareTo"/> and clears the dirty flag.
        /// </summary>
        /// <remarks>
        /// Only the live slice <c>[0, _timerCount)</c> of the backing
        /// array is sorted — unused tail slots are never touched.
        /// Skips the sort when zero or one timer is active.
        /// </remarks>
        public void SortTimers()
        {
            if (_timerCount > 1)
            {
                Array.Sort(_timerBuffer, 0, _timerCount);
            }

            _isDirty = false;
        }

        /// <summary>Suspends all currently active Timers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SuspendAllTimers()
        {
            for (var i = 0; i < _timerCount; ++i) _timerBuffer[i].Suspend();
        }

        /// <summary>
        /// Suspends all active Timers associated with a specific
        /// <see cref="MonoBehaviour"/>.
        /// </summary>
        public void SuspendAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            for (var i = 0; i < _timerCount; ++i)
            {
                var timer = _timerBuffer[i];
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Suspend();
            }
        }
        
        /// <summary>
        /// Clears the active timer storage, releasing all GC references.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearTimerStorage()
        {
            Array.Clear(_timerBuffer, 0, _timerCount);
            _timerSet.Clear();
            _timerCount = 0;
        }

        /// <summary>
        /// Increments the frame counter and triggers a sort when the interval elapses.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FrameCounter()
        {
            if (++_frameCounter < _intervalFrames) return;
            
            SortTimers();
            _isDirty = false;
            _frameCounter = 0;
        }

        /// <summary>
        /// Orchestrates the throttled re-sorting of the Timer collection.
        /// Called only when <see cref="_isDirty"/> is <c>true</c>.
        /// </summary>
        private void HandleSorting(float deltaTime)
        {
            if (_useFrames && !_useSeconds) FrameCounter();
            else SecondsTimer(deltaTime);
        }

        /// <summary>
        /// Determines whether a Timer is no longer valid for processing.
        /// </summary>
        /// <param name="timer">The Timer instance to validate.</param>
        /// <returns>True if the Timer is cancelled or its associated MonoBehaviour reference has been destroyed; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInvalid([NotNull] Timer timer)
            => timer.IsCancelled || (timer.HasReference && timer.IsDestroyed);

        /// <summary>
        /// Removes a Timer at the specified index using the "Swap-with-Last"
        /// technique for O(1) performance, keeping both the array and the set in sync.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveAtFast(int index)
        {
            var lastIndex = --_timerCount;
            var lastTimer = _timerBuffer[lastIndex];
            var timerToRemove = _timerBuffer[index];

            _timerSet.Remove(timerToRemove);
    
            if (index < lastIndex)
            {
                _timerBuffer[index] = lastTimer;
                _isDirty = true;
            }
            
            _timerBuffer[lastIndex] = null;
            
            timerToRemove.DeInit();
        }

        /// <summary>
        /// Accumulates elapsed time and triggers a sort when the interval elapses.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SecondsTimer(float deltaTime)
        {
            _timeSinceLastSort += deltaTime;

            if (_timeSinceLastSort < _intervalSeconds) return;
            
            SortTimers();
            _isDirty = false;
            _timeSinceLastSort = 0f;
        }

        /// <summary>
        /// Returns <c>true</c> if a Timer reference is null, unbound, or its bound
        /// GameObject has been destroyed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TimerIsDestroyed([CanBeNull] Timer timer)
            => timer is null || !timer.HasReference || timer.IsDestroyed;
        
        #endregion Methods
    }
}
