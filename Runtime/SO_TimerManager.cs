using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// A ScriptableObject for managing and updating Timers in an application.
    /// </summary>
    /// <remarks>
    /// The SO_TimerManager is a class that handles the creation, management, and update of Timers used
    /// throughout the application. It provides methods to add, cancel, suspend, resume, and perform
    /// various operations on Timers.
    /// </remarks>
    [CreateAssetMenu(
        fileName = Folder.Name_Timer + Folder.Name_Manager, 
        menuName = Folder.SO + Folder.Base_Manager + Folder.Name_Timer, 
        order = 0
    )]
        public sealed class SO_TimerManager : SO_Manager
    {
        #region Fields
        
        private const int Capacity = 1024;
        
        private static readonly List<Timer> TimerList = new List<Timer>(Capacity);

        [Header("CONFIG")]
        [SerializeField] private bool _useFrames = true;
        [SerializeField] private bool _useSeconds;
        
        [Header("DATA")]
        [SerializeField] private int _intervalFrames = 30;
        [SerializeField] private float _intervalSeconds = 1f;

        private int _frameCounter;
        private float _timeSinceLastSort;
        
        #endregion Fields
        
        #region Properties
        
        /// <summary>
        /// Gets an immutable list of all active Timers.
        /// </summary>
        /// <value>
        /// An immutable list of all active Timers.
        /// </value>
        /// <remarks>
        /// This property provides access to a read-only list of all Timers that are currently active and managed by the Timer Manager.
        /// </remarks>
        public static IReadOnlyList<Timer> Timers => TimerList;
        
        #endregion Properties

        #region Unity Callbacks

        private void OnValidate()
        {
            if (_useFrames) _useSeconds = false;
            else if (_useSeconds) _useFrames = false;
        }

        /// <summary>
        /// Updates and manages the state of timers during each frame.
        /// </summary>
        /// <remarks>
        /// This method is called during each frame update to manage the state of active timers.
        /// It sorts the timers based on their end times and checks their status, such as completion,
        /// cancellation, and suspension, to trigger associated actions. If a timer has looped, it is reset
        /// for the next iteration.
        /// </remarks>
        public override void OnUpdate()
        {
            if (_useFrames && !_useSeconds) FrameCounter();
            if (_useSeconds && !_useFrames) SecondsTimer();
            
            Timer timer;
            int index;
            var count = TimerList.Count;

            if (count == 0) return;

            for (index = 0; index < count; ++index)
            {
                timer = TimerList[index];
                
                if (TimerIsDestroyed()) continue;
                if (TimerIsCancelled()) continue;
                if (TimerIsSuspended()) continue;
                if (!timer.IsDone) continue;

                var p = timer.PercentageDone();
                timer.SetState(TimerState.Completed);
                timer.Actions.Updated?.Invoke(p);
                timer.Actions.Completed?.Invoke();
                
                if (!TimerHasLoop()) return;
                
                timer.LoopDuration();
            }

            return;

            bool TimerHasLoop()
            {
                if (timer.HasLoop) return true;
                
                timer = RemoveTimer();
                return false;
            }

            bool TimerIsCancelled()
            {
                if (!timer.IsCancelled) return false;
                
                timer = RemoveTimer();
                return true;
            }

            bool TimerIsDestroyed()
            {
                if (!timer.HasReference || !timer.IsDestroyed) return false;
                
                timer = RemoveTimer();
                return true;
            }

            bool TimerIsSuspended()
            {
                if (!timer.IsSuspended)
                {
                    if (!timer.HasReference || timer.Behaviour.enabled) return false;

                    timer.Suspend(false);
                    return true;
                }

                // TODO: Suspend timer when its host MonoBehaviour is disabled.
                if (timer.IsSuspendedManually) return true;

                // TODO: Resume Timer when its host MonoBehaviour is enabled.
                if (timer.HasReference && timer.Behaviour.enabled) timer.Resume();

                return true;
            }

            Timer RemoveTimer()
            {
                TimerList.RemoveAt(index);
                index--;
                count--;
                
                return null;
            }
        }
        
        #endregion Unity Callbacks

        #region Methods
        
        /// <summary>
        /// Adds a Timer to the end of the list of active Timers.
        /// </summary>
        /// <param name="timer">The Timer to add to the end of list. The value can be null.</param>
        /// <remarks>
        /// This method adds the specified Timer to the list of active Timers managed by the TimerManager.
        /// Once added, the Timer will be tracked and updated by the TimerManager.
        /// </remarks>
        public static void AddTimer([NotNull] Timer timer) => TimerList.Add(timer);

        /// <summary>
        /// Cancels all active Timers managed by the Timer Manager.
        /// </summary>
        /// <remarks>
        /// This method cancels all Timers that are currently active.
        /// It iterates through the list of active Timers and cancels each one.
        /// </remarks>
        public static void CancelAllTimers()
        {
            var count = TimerList.Count;
            for (var i = count - 1; i >= 0; --i)
            {
                TimerList[i].Cancel();
            }
        }

        /// <summary>
        /// Cancels all active Timers associated with a specific MonoBehaviour.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour whose Timers should be cancelled.</param>
        /// <remarks>
        /// This method iterates through the list of active Timers and cancels any owned by a specific MonoBehaviour.
        /// </remarks>
        /// <seealso cref="Timer"/>
        public static void CancelAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            var count = TimerList.Count;
            for (var i = count - 1; i >= 0; --i)
            {
                var timer = TimerList[i];
                
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Cancel();
            }
        }

        /// <summary>
        /// Determines whether a Timer is in the list of active Timers.
        /// </summary>
        /// <param name="timer">The Timer to locate in the list. The value can be null.</param>
        /// <returns>
        /// <c>true</c> if the Timer is found in the list; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method checks if the Timer is in the list of active Timers managed by the TimerManager.
        /// It returns <c>true</c> if the Timer is found in the list, indicating that it is actively managed by the TimerManager.
        /// Otherwise, it returns <c>false</c>.
        /// </remarks>
        public static bool Contains([NotNull] Timer timer) => TimerList.Contains(timer);

        private void FrameCounter()
        {
            _frameCounter++;

            if (_frameCounter < _intervalFrames) return;
            
            TimerList.Sort();
            _frameCounter = 0;
        }

        /// <summary>
        /// Resumes all suspended Timers.
        /// </summary>
        /// <remarks>
        /// This method iterates through all Timers and resumes any that were previously suspended.
        /// It effectively continues the countdown for all suspended Timers.
        /// </remarks>
        /// <seealso cref="Timer"/>
        public static void ResumeAllTimers()
        {
            var count = TimerList.Count;
            for (var i = 0; i < count; ++i)
            {
                TimerList[i].Resume();
            }
        }

        /// <summary>
        /// Resumes all suspended Timers associated with a specific MonoBehaviour.
        /// </summary>
        /// <remarks>
        /// This method iterates through all Timers and resumes any that were previously suspended
        /// and are associated with the given MonoBehaviour.
        /// It effectively continues the countdown for matching suspended Timers.
        /// </remarks>
        /// <param name="behaviour">The MonoBehaviour whose Timers should be resumed.</param>
        /// <seealso cref="Timer"/>
        public static void ResumeAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            var count = TimerList.Count;
            for (var i = 0; i < count; ++i)
            {
                var timer = TimerList[i];
                
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Resume();
            }
        }

        private void SecondsTimer()
        {
            _timeSinceLastSort += StaticTime.DeltaTime;

            if (_timeSinceLastSort < _intervalSeconds) return;
            
            TimerList.Sort();
            _timeSinceLastSort = 0f;
        }

        /// <summary>
        /// Suspends all active Timers.
        /// </summary>
        /// <remarks>
        /// This method iterates through all active Timers and suspends their countdown,
        /// effectively pausing its operation until resumed.
        /// </remarks>
        /// <seealso cref="Timer"/>
        public static void SuspendAllTimers()
        {
            var count = TimerList.Count;
            for (var i = 0; i < count; ++i)
            {
                TimerList[i].Suspend();
            }
        }

        /// <summary>
        /// Suspends all active Timers associated with a specific MonoBehaviour.
        /// </summary>
        /// <param name="behaviour">The MonoBehaviour whose Timers should be suspended.</param>
        /// <remarks>
        /// This method iterates through all active Timers and suspends the Timer's countdown,
        /// effectively pausing its operation until resumed.
        /// </remarks>
        /// <seealso cref="Timer"/>
        public static void SuspendAllTimers([NotNull] MonoBehaviour behaviour)
        {
            if (!behaviour) throw new ArgumentNullException(nameof(behaviour));
            
            var count = TimerList.Count;
            for (var i = 0; i < count; ++i)
            {
                var timer = TimerList[i];
                
                if (TimerIsDestroyed(timer)) continue;
                if (timer.Behaviour != behaviour) continue;

                timer.Suspend();
            }
        }

        private static bool TimerIsDestroyed([CanBeNull] Timer timer) => timer is null || !timer.HasReference || timer.IsDestroyed;
        
        #endregion Methods
    }
}
