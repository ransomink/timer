using NUnit.Framework;
using UnityEngine;

namespace Ransom.Tests
{
    [TestFixture]
    public class TimerStateTests
    {
        private TimerManager _manager;
        private UpdateDispatcher _dispatcher;
        
        #region Setup
        
        [SetUp]
        public void Setup()
        {
            // UnitySynchronizationContextDispatcher.Initialize();
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
        public void Timer_InitializesTo_EnabledState()
        {
            var timer = Timer.Record(5f);
            Assert.AreEqual(TimerState.Enabled, timer.State);
        }

        [Test]
        public void Timer_Start_TransitionsTo_Activated()
        {
            var timer = Timer.Record(5f).Start();
            Assert.AreEqual(TimerState.Activated, timer.State);
        }

        [Test]
        public void Timer_Pause_TransitionsTo_Suspended()
        {
            var timer = Timer.Record(5f).Start().Pause();
            
            Assert.AreEqual(TimerState.Suspended, timer.State);
            Assert.IsTrue(timer.IsSuspended);
        }

        [Test]
        public void Timer_Cancel_TransitionsTo_Cancelled()
        {
            var timer = Timer.Record(5f);
            timer.Cancel();
            
            Assert.AreEqual(TimerState.Cancelled, timer.State);
            Assert.IsTrue(timer.IsCancelled);
        }

        [Test]
        public void Timer_ForceCompletion_SetsIsDoneTrue()
        {
            var timer = Timer.Record(10f).Start();
            timer.ForceCompletion();
            
            Assert.IsTrue(timer.IsDone);
        }
    }
}
