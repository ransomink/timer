using NUnit.Framework;
using UnityEngine;

namespace Ransom.Tests
{
    [TestFixture]
    public class TimerStateTests
    {
        [SetUp]
        public void Setup()
        {
            // UnitySynchronizationContextDispatcher.Initialize();

            new GameObject("UpdateDispatcher", typeof(UpdateDispatcher));
        }        

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
