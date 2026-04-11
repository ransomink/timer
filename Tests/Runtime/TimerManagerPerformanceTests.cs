using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ransom.Tests
{
    [TestFixture]
    public class TimerManagerPerformanceTests
    {
        private TimerManager _manager;
        
        [SetUp]
        public void Setup()
        {
            // UnitySynchronizationContextDispatcher.Initialize();
            
            new GameObject("UpdateDispatcher", typeof(UpdateDispatcher));

            _manager = TimerManager.Instance;
        }

        [Test]
        public void AddTimer_MarksManagerAsDirty()
        {
            var timer = Timer.Record(5f);
            _manager.AddTimer(timer);

            // Use reflection or a public debug property to verify _isDirty if private
            Assert.IsTrue(_manager.ContainsTimer(timer));
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

            // Remove the first timer (t1)
            // Internally should swap t1 with t3, then remove tail
            // This is handled inside OnUpdate if t1 becomes invalid
            t1.Cancel();
            
            _manager.OnUpdate(0.1f);

            Assert.IsFalse(_manager.ContainsTimer(t1));
            Assert.AreEqual(2, _manager.Timers.Count);
            Assert.AreEqual(t3, _manager.Timers[0]); // t3 moved to index 0
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

            Assert.AreEqual(shortTimer, _manager.Timers[0]);
        }
        
        [UnityTest]
        public IEnumerator Timer_AutoCancels_OnOwnerDestroyed()
        {
            var go = new GameObject("Owner");
            var owner = go.AddComponent<EmptyBehaviour>();
            var timer = Timer.Bind(owner, 5f).Start();

            Object.DestroyImmediate(go);
    
            var canProcess = timer.CanProcess();
    
            Assert.IsFalse(canProcess);
            Assert.IsTrue(timer.IsCancelled);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (TimerManager.Instance)
                TimerManager.Instance.Shutdown();
            
            if (UpdateDispatcher.Instance)
                Object.DestroyImmediate(UpdateDispatcher.Instance.gameObject);
            
            AppLifecycle.SetApplicationToQuit(false);
        }
    }
}
