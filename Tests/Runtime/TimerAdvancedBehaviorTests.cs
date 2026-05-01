using NUnit.Framework;
using UnityEngine;

namespace Ransom.Tests
{
    [TestFixture]
    public class TimerAdvancedBehaviorTests
    {
        private TimerManager     _manager;
        private UpdateDispatcher _dispatcher;

        #region Setup
        
        [SetUp]
        public void Setup()
        {
            _manager    = ScriptableObject.CreateInstance<TimerManager>();
            
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
            
            AppLifecycle.SetApplicationToQuit(false);
        }

        #endregion Setup

        [Test]
        public void Timer_UnscaledTime_ProcessesCorrectly()
        {
            var timer = _manager.RentTimer().Realtime().Start(5f);
            
            Assert.IsTrue(timer.HasUnscaledTime, "Timer should be flagged as unscaled.");
            
            _manager.OnUpdate(0f);
            
            Assert.IsTrue(timer.TimeRemaining <= 5f, "Manager should have subtracted unscaled delta time.");
        }

        [Test]
        public void TimerSequence_PlayStepsInOrder()
        {
            var stepCounter = 0;
            var sequenceComplete = false;

            var sequence = new TimerSequence()
                .Append(1f, () => stepCounter++)
                .Append(1f, () => stepCounter++)
                .OnComplete(() => sequenceComplete = true)
                .Play();
            
            _manager.OnUpdate(1f);
            Assert.AreEqual(1, stepCounter, "First step callback failed.");
            Assert.IsFalse(sequenceComplete);
            
            _manager.OnUpdate(1f);
            Assert.AreEqual(2, stepCounter, "Second step callback failed.");
            Assert.IsTrue(sequenceComplete, "Sequence completion callback failed.");
            
            Assert.AreEqual(0, _manager.Timers.Count, "Timer should be removed and returned to pool after completion.");
        }

        [Test]
        public void TimerSequence_Cancel_StopsExecution()
        {
            var stepCounter = 0;
            
            var sequence = new TimerSequence()
                .Append(1f, () => stepCounter++)
                .Append(1f, () => stepCounter++)
                .Play();
            
            _manager.OnUpdate(1f);
            
            sequence.Cancel();
            
            _manager.OnUpdate(1f);
            
            Assert.AreEqual(1, stepCounter, "Step 2 executed even though sequence was cancelled.");
        }
    }
}
