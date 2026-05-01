using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ransom.Tests
{
    [TestFixture]
    public class TimerManagerPerformanceTests
    {
        private DebugSettings    _debug;
        private TimerManager     _manager;
        private TimerTemplate    _template;
        private UpdateDispatcher _dispatcher;
        
        #region Setup
        
        [SetUp]
        public void Setup()
        {
            // UnitySynchronizationContextDispatcher.Initialize();
            _manager         = ScriptableObject.CreateInstance<TimerManager>();
            _template        = ScriptableObject.CreateInstance<TimerTemplate>();
            _template.name   = "[TEST] PoolableTimer";
            _template.Source = new Timer();

            var go      = new GameObject("[TEST] UpdateDispatcher");
            _dispatcher = go.AddComponent<UpdateDispatcher>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager)
            {
                _manager.Shutdown();
                
                Object.DestroyImmediate(_manager);
                _manager = null;
            }
            
            if (_dispatcher)
            {
                Object.DestroyImmediate(_dispatcher.gameObject);
                _dispatcher = null;
            }

            if (_template)
                Object.DestroyImmediate(_template);
            
            AppLifecycle.SetApplicationToQuit(false);
        }

        #endregion Setup

        // ------------------------------------------------------------------
        // Helper — initializes the pool via the internal InitPool path.
        // Because _timerTemplate is a [SerializeField], we can only inject it
        // through reflection in tests (the Inspector sets it in production).
        // ------------------------------------------------------------------
        private void EnablePool()
        {
            var field = typeof(TimerManager)
                .GetField("_timerTemplate",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            field?.SetValue(_manager, _template);

            // Re-run InitPool so the ClassObjectPool is constructed.
            var method = typeof(TimerManager)
                .GetMethod("InitPool",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            method?.Invoke(_manager, null);
        }

        #region Active Storage Tests

        [Test]
        public void AddTimer_MarksManagerAsDirty()
        {
            var timer = Timer.Record(5f);
            _manager.AddTimer(timer);

            var field = typeof(TimerManager)
                .GetField("_isDirty",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
            var isDirty = field?.GetValue(_manager);
            
            Assert.IsTrue(_manager.ContainsTimer(timer));
            Assert.IsTrue(isDirty is bool dirty && dirty,
                "Adding a timer should mark the manager as dirty, prompting a sort on next update.");
        }

        [Test]
        public void AddTimer_Duplicate_DoesNotAddTwice()
        {
            var timer = Timer.Record(5f);
            
            _manager.AddTimer(timer);
            _manager.AddTimer(timer);
            
            Assert.AreEqual(1, _manager.Timers.Count, "Duplicate: AddTimer should not increase the count.");
        }

        [Test]
        public void RemoveAtFast_HashSet_SyncsWithArray()
        {
            var timer = Timer.Record(5f);
            _manager.AddTimer(timer);
            
            timer.Cancel();
            _manager.OnUpdate(0.1f);
            
            Assert.IsFalse(_manager.ContainsTimer(timer), "HashSet should no longer contain the removed timer.");
        }

        [Test]
        public void RemoveAtFast_PerformsSwapWithLast()
        {
            var t1 = Timer.Record(1f);
            var t2 = Timer.Record(2f);
            var t3 = Timer.Record(3f);

            _manager.AddTimer(t1);
            _manager.AddTimer(t2);
            _manager.AddTimer(t3);

            // Cancel t1 so OnUpdate removes it via RemoveAtFast.
            // Expected: t3 swaps into index 0, count drops to 2.
            t1.Cancel();
            
            _manager.OnUpdate(0.1f);

            Assert.IsFalse(_manager.ContainsTimer(t1), "Cancelled timer should be removed from the active set.");
            Assert.AreEqual(2, _manager.Timers.Count, "Count should be {2} after removing one timer.");
            Assert.AreEqual(t3, _manager.Timers[0], "t3 should have swapped into index 0.");
        }

        [Test]
        public void Sorting_Prioritizes_LowestTimeRemaining()
        {
            var longTimer = Timer.Record(100f);
            var shortTimer = Timer.Record(1f);

            _manager.AddTimer(longTimer);
            _manager.AddTimer(shortTimer);
            
            for (var i = 0; i < _manager.Timers.Count; i++)
            {
                Debug.Log($"Timer at index [{ i.ToLog() }]: { _manager.Timers[i].TimeRemaining.ToLog() } seconds remaining");
            }

            _manager.SortTimers();
            
            for (var i = 0; i < _manager.Timers.Count; i++)
            {
                Debug.Log($"Timer at index [{ i.ToLog() }]: { _manager.Timers[i].TimeRemaining.ToLog() } seconds remaining");
            }

            Assert.AreEqual(shortTimer, _manager.Timers[0],
                "Timer with the lowest TimeRemaining should be the at index 0 after sort.");
        }

        [Test]
        public void SortTimers_ClearsDirtyFlag()
        {
            var timer = Timer.Record(5f);
            _manager.AddTimer(timer);
            _manager.SortTimers();
            _manager.OnUpdate(0.1f);
            
            var isDirty = typeof(TimerManager)
                .GetField("_isDirty",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_manager);
            
            Assert.IsTrue(isDirty is bool dirty && !dirty,
                "After sorting and processing timers, the dirty flag should be cleared.");
            Assert.IsTrue(_manager.ContainsTimer(timer), "Timer should still be tracked after a clean update cycle.");
        }
        
        [UnityTest]
        public IEnumerator Timer_AutoCancels_OnOwnerDestroyed()
        {
            var go    = new GameObject("Owner");
            var owner = go.AddComponent<EmptyBehaviour>();
            var timer = Timer.Bind(owner, 5f).Start();

            Object.DestroyImmediate(go);
    
            var canProcess = timer.CanProcess();
    
            Assert.IsFalse(canProcess, "CanProcess should return false after the owner is destroyed.");
            Assert.IsTrue(timer.IsCancelled, "Timer should transition to Cancelled when its owner is destroyed.");
            
            yield return null;
        }

        #endregion Active Storage Tests

        #region ClassObjectPool Tests

        [Test]
        public void Pool_IsNotReady_WithoutTemplate()
        {
            Assert.IsFalse(_manager.IsPoolReady, "Pool should not be ready when no TimerTemplate is assigned.");
        }
        
        [Test]
        public void Pool_IsReady_AfterTemplateAssigned()
        {
            EnablePool();
            Assert.IsTrue(_manager.IsPoolReady, "Pool should be ready after InitPool runs with a valid template.");
        }

        [Test]
        public void RentTimer_WithoutPool_AllocatesNewInstance()
        {
            var timer = _manager.RentTimer();
            
            Assert.AreEqual(0, _manager.Timers.Count, "RentTimer should not add to active timers when pool is not ready.");
            Assert.IsNotNull(timer, "RentTimer should allocate a new instance even if the pool is not ready.");
        }

        [Test]
        public void RentTimer_WithPool_ReturnsPooledInstance()
        {
            EnablePool();
            var countBefore = _manager.PooledTimerCount;
            var timer = _manager.RentTimer();
            
            Assert.IsNotNull(timer, "RentTimer should return a valid instance when the pool is ready.");
            Assert.AreEqual(countBefore - 1, _manager.PooledTimerCount, "RentTimer should decrease the pooled count by one.");
            Assert.AreEqual(0, _manager.Timers.Count, "RentTimer should not be active until Start is called.");
            
            timer.Start(5f);
            
            Assert.AreEqual(1, _manager.Timers.Count, "Starting the timer should add it to the active set.");
        }
        
        [Test]
        public void ReturnTimer_IncreasesPooledCount()
        {
            EnablePool();
            var timer = _manager.RentTimer();
            var countBefore = _manager.PooledTimerCount;
            
            _manager.ReturnTimer(timer);
            
            Assert.AreEqual(countBefore + 1, _manager.PooledTimerCount,
                "Returning a timer should increase the inactive pool count by one.");
        }

        [Test]
        public void ReturnTimer_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.ReturnTimer(null),
                "ReturnTimer(null) should be a no-op, not an exception.");
        }

        [Test]
        public void ReturnTimer_ResetsTimerState()
        {
            var go    = new GameObject("Owner");
            var owner = go.AddComponent<EmptyBehaviour>();
            var timer = _manager.RentTimer();
            
            timer.BindTo(owner).Length(5f).Loop().Start();
            timer.Reset();
            
            _manager.ReturnTimer(timer);

            var recycled = _manager.RentTimer();
                
            Assert.IsFalse(recycled.HasLoop, "A recycled timer should have HasLoop cleared.");
            Assert.IsFalse(recycled.HasReference, "A recycled timer should have HasReference cleared.");
            Assert.AreEqual(0f, recycled.Duration, "A recycled timer should have its duration reset to 0.");
            Assert.AreEqual(TimerState.Disable, recycled.State, "A recycled timer should be in Disable state.");
        }

        [Test]
        public void ReturnTimer_WithoutPool_DoesNotThrow()
        {
            var timer = new Timer();
            Assert.DoesNotThrow(() => _manager.ReturnTimer(timer),
                "ReturnTimer should not throw even if the pool is not initialized.");
        }

        [Test]
        public void Shutdown_ClearsPoolAndActiveTimers()
        {
            EnablePool();
            
            _manager.WarmPool(5);
            _manager.AddTimer(Timer.Record(1f));
            _manager.Shutdown();
            
            Assert.AreEqual(0, _manager.PooledTimerCount, "Shutdown should empty the timer pool.");
            Assert.AreEqual(0, _manager.Timers.Count, "Shutdown should clear all active timers.");
            Assert.IsFalse(_manager.IsPoolReady, "Pool should not be ready after shutdown.");
        }

        [Test]
        public void Timer_DeInit_ReturnsToPool()
        {
            EnablePool();

            var timer = _manager.RentTimer();
            _manager.AddTimer(timer);
            
            var countBefore = _manager.PooledTimerCount;

            // DeInit should call Pool.Release → Reset → push back to stack.
            timer.DeInit();

            Assert.AreEqual(countBefore + 1, _manager.PooledTimerCount, "DeInit should return the timer to the pool.");
        }

        [Test]
        public void WarmPool_PreallocatesInactiveInstances()
        {
            EnablePool();
            const int warmCount = 5;
            
            _manager.WarmPool(warmCount);
            
            Assert.GreaterOrEqual(_manager.PooledTimerCount, warmCount,
                $"After WarmPool ({warmCount}), at least {warmCount} instances " +
                "should be available in the pool.");
        }

        [Test]
        public void WarmPool_WithoutTemplate_LogsWarningAndDoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.WarmPool(5),
                "WarmPool without a template should not throw.");
        }

        #endregion ClassObjectPool Tests

        #region GroupId Tests

        [Test]
        public void CancelAllTimersByGroupId_OnlyCancelTargetGroup()
        {
            EnablePool();
            
            var timerGroup1A = _manager.RentTimer().Group(1).Start(10f);
            var timerGroup1B = _manager.RentTimer().Group(1).Start(10f);
            var timerGroup2A = _manager.RentTimer().Group(2).Start(10f);
            
            _manager.CancelAllTimers(1);
            
            Assert.AreEqual(TimerState.Cancelled, timerGroup1A.State, "Timer1A should be cancelled.");
            Assert.AreEqual(TimerState.Cancelled, timerGroup1B.State, "Timer1B should be cancelled.");
            Assert.AreEqual(TimerState.Activated, timerGroup2A.State, "Timer2A should remain activated.");
        }

        [Test]
        public void SuspendAllTimersByGroupId_OnlySuspendsTargetGroup()
        {
            EnablePool();
            
            var timerGroup99 = _manager.RentTimer().Group(99).Start(10f);
            var timerUncategorized = _manager.RentTimer().Start(10f);
            
            _manager.SuspendAllTimers(99);
            
            Assert.AreEqual(TimerState.Suspended, timerGroup99.State, "Timer99 should be suspended.");
            Assert.AreEqual(TimerState.Activated, timerUncategorized.State, "Uncategorized timer should remain activated.");
        }

        #endregion GroupId Tests
    }
}
