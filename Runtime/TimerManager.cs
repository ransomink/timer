using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// A centralized system for managing, updating, and optimizing the execution of multiple <see cref="Timer"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This manager utilizes a <see cref="ScriptableSingleton{T}"/> pattern to provide global access while remaining 
    /// persistent across scenes. It implements <see cref="IUpdate"/> to integrate with a central update loop, 
    /// reducing the overhead of having multiple <c>Update</c> calls in the scene.
    /// </para>
    /// <para>
    /// <b>Performance Features:</b>
    /// <list type="bullet">
    /// <item><b>Throttled Sorting:</b> Re-prioritizes the timer collection based on frame or time intervals rather than every frame.</item>
    /// <item><b>O(1) Removals:</b> Uses a swap-with-last technique to discard finished timers without shifting array elements.</item>
    /// <item><b>Memory Efficiency:</b> Operates on a pre-allocated internal list to minimize Garbage Collector pressure.</item>
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
        
        private const int Capacity = 1024;
        
        private readonly List<Timer> _timers = new List<Timer>(Capacity);

        [Header(TimerLib.Headers.Config)]
        [SerializeField][Tooltip(TimerLib.Tooltips.UseFrames)] private bool _useFrames = true;
        [SerializeField][Tooltip(TimerLib.Tooltips.IntervalFrames)] private int _intervalFrames = 60;
        
        [Space]
        [SerializeField][Tooltip(TimerLib.Tooltips.UseSeconds)] private bool _useSeconds;
        [SerializeField][Tooltip(TimerLib.Tooltips.IntervalSeconds)] private float _intervalSeconds = 1f;

        private bool _isDirty;
        private int _frameCounter;
        private float _timeSinceLastSort;
        
        #endregion Fields
        
        #region Properties
        
        public static bool IsQuittingApplication { get; private set; }
        
        /// <summary>
        /// Provides read-only access to all currently active and managed Timers.
        /// </summary>
        /// <remarks>
        /// This property returns the internal list directly as an <see cref="IReadOnlyList{T}"/>. 
        /// This avoids the memory allocation of <c>AsReadOnly()</c> or a new <c>ReadOnlyCollection</c>, 
        /// but assumes the caller will not attempt to cast it back to a mutable list.
        /// </remarks>
        public IReadOnlyList<Timer> Timers => _timers;
        
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
        /// This method performs the following operations per frame:
        /// <list type="bullet">
        /// <item>Handles pending sort requests if the collection is dirty.</item>
        /// <item>Validates timer references and performs fast-cleanup of destroyed/cancelled timers.</item>
        /// <item>Processes the <see cref="Timer.Tick"/> using either scaled or unscaled time.</item>
        /// <item>Manages transitions between the Delay phase and the Active phase.</item>
        /// <item>Triggers Update and Completion callbacks.</item>
        /// <item>Handles automatic looping and re-sorting of recurring timers.</item>
        /// </list>
        /// Iteration is performed in reverse to allow for safe O(1) removals during the update cycle.
        /// </remarks>
        public void OnUpdate(float deltaTime)
        {
            if (_isDirty) HandleSorting(deltaTime);
            
            var count = _timers.Count;
            if (count == 0) return;
            
            var unscaledDeltaTime = StaticTime.UnscaledDeltaTime;

            for (var i = count - 1; i >= 0; --i)
            {
                var timer = _timers[i];

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
            if (Application.isPlaying && UpdateDispatcher.Instance)
                UpdateDispatcher.Instance.AddBaseUpdate(this);
        }
        
        protected override void OnShutdown()
        {
            if (Application.isPlaying && UpdateDispatcher.Instance)
                UpdateDispatcher.Instance.RemoveBaseUpdate(this);
            
            _timers.Clear();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Orchestrates the throttled re-sorting of the Timer collection.
        /// </summary>
        /// <param name="deltaTime">The current game clock delta time.</param>
        /// <remarks>
        /// This method implements a "Double-Gate" optimization:
        /// <list type="number">
        /// <item><b>Gate 1:</b> It exits immediately if <see cref="_isDirty"/> is false, meaning no timers have been added or looped.</item>
        /// <item><b>Gate 2:</b> It chooses between <see cref="FrameCounter"/> or <see cref="SecondsTimer"/> based on serialized configuration.</item>
        /// </list>
        /// Sorting is only executed when both a structural change has occurred AND the time/frame interval has elapsed.
        /// </remarks>
        private void HandleSorting(float deltaTime)
        {
            if (!_isDirty) return;

            if (_useFrames && !_useSeconds)
                FrameCounter();
            else
                SecondsTimer(deltaTime);
        }

        /// <summary>
        /// Determines whether a Timer is no longer valid for processing.
        /// </summary>
        /// <param name="timer">The Timer instance to validate.</param>
        /// <returns>True if the Timer is cancelled or its associated MonoBehaviour reference has been destroyed; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsInvalid([NotNull] Timer timer)
        {
            return timer.IsCancelled ||
                   timer is { HasReference: true, IsDestroyed: true };
        }

        /// <summary>
        /// Removes a Timer at the specified index using the "Swap-with-Last" technique for O(1) performance.
        /// </summary>
        /// <param name="index">The index of the Timer to remove.</param>
        /// <remarks>
        /// This method avoids the O(N) cost of shifting elements by overwriting the target index 
        /// with the last element in the list and then removing the last entry. 
        /// Note: This operation changes the order of the collection and marks the manager as dirty for re-sorting.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveAtFast(int index)
        {
            var lastIndex = _timers.Count - 1;
    
            if (index < lastIndex)
            {
                _timers[index] = _timers[lastIndex];
                _isDirty = true;
            }
    
            _timers.RemoveAt(lastIndex);
        }
        
        /// <summary>
        /// Registers a new Timer with the manager.
        /// </summary>
        /// <param name="timer">The Timer instance to add.</param>
        /// <remarks>
        /// Adding a timer marks the manager as dirty, ensuring that the new timer is 
        /// correctly positioned during the next scheduled sort operation.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTimer([NotNull] Timer timer)
        {
            _timers.Add(timer);
            _isDirty = true;
        }

        /// <summary>
        /// Cancels all currently active Timers managed by this system.
        /// </summary>
        public void CancelAllTimers()
        {
            var count = _timers.Count;
            for (var i = count - 1; i >= 0; --i)
            {
                _timers[i].Cancel();
            }
        }

        /// <summary>
        /// Cancels all active Timers associated with a specific <see cref="MonoBehaviour"/>.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to cancel.</param>
        public void CancelAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            var count = _timers.Count;
            for (var i = count - 1; i >= 0; --i)
            {
                var timer = _timers[i];
                
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Cancel();
            }
        }

        /// <summary>
        /// Checks if the specified Timer is currently being tracked by the manager.
        /// </summary>
        /// <param name="timer">The Timer to look for.</param>
        /// <returns>True if the timer exists in the internal collection; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsTimer([NotNull] Timer timer) => _timers.Contains(timer);

        /// <summary>
        /// Increments the frame counter and triggers a collection sort if the frame interval is reached.
        /// </summary>
        /// <remarks>
        /// This method is used when the manager is configured for frame-based throttling. 
        /// Sorting is an O(N log N) operation; by limiting it to specific frame intervals (e.g., every 60 frames), 
        /// we significantly reduce the per-frame CPU overhead.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FrameCounter()
        {
            _frameCounter++;

            if (_frameCounter < _intervalFrames) return;
            
            SortTimers();
            
            _isDirty = false;
            _frameCounter = 0;
        }

        /// <summary>
        /// Resumes all currently suspended Timers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResumeAllTimers()
        {
            var count = _timers.Count;
            for (var i = 0; i < count; ++i)
            {
                _timers[i].Resume();
            }
        }

        /// <summary>
        /// Resumes all suspended Timers associated with a specific <see cref="MonoBehaviour"/>.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to resume.</param>
        public void ResumeAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            var count = _timers.Count;
            for (var i = 0; i < count; ++i)
            {
                var timer = _timers[i];
                
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Resume();
            }
        }

        /// <summary>
        /// Tracks elapsed real-time and triggers a collection sort if the second-based interval is reached.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="StaticTime.DeltaTime"/> to track time independently of the frame rate. 
        /// It ensures that even if the frame rate fluctuates, the timer collection is re-prioritized 
        /// at a consistent temporal frequency.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SecondsTimer(float deltaTime)
        {
            _timeSinceLastSort += deltaTime;

            if (_timeSinceLastSort < _intervalSeconds) return;
            
            SortTimers();
            
            _isDirty = false;
            _timeSinceLastSort = 0f;
        }

        public void Shutdown()
        {
            OnShutdown();
        }
        
        public void SortTimers()
        {
            _timers.Sort();
            _isDirty = false;
        }

        /// <summary>
        /// Suspends all currently active Timers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SuspendAllTimers()
        {
            var count = _timers.Count;
            for (var i = 0; i < count; ++i)
            {
                _timers[i].Suspend();
            }
        }

        /// <summary>
        /// Suspends all active Timers associated with a specific <see cref="MonoBehaviour"/>.
        /// </summary>
        /// <param name="behaviour">The owner of the timers to suspend.</param>
        public void SuspendAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            var count = _timers.Count;
            for (var i = 0; i < count; ++i)
            {
                var timer = _timers[i];
                
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Suspend();
            }
        }

        /// <summary>
        /// Determines if a Timer reference is null or its bound GameObject has been destroyed.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TimerIsDestroyed([CanBeNull] Timer timer) => timer is null || !timer.HasReference || timer.IsDestroyed;
        
        #endregion Methods
    }
}
