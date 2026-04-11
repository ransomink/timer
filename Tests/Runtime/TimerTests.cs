using NUnit.Framework;
using UnityEngine;

namespace Ransom.Tests
{
    [TestFixture]
    public class TimerTests
    {
        private const float DefaultDuration = 5f;
        
        #region Setup

        [SetUp]
        public void Setup()
        {
            new GameObject("UpdateDispatcher", typeof(UpdateDispatcher));
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

        #endregion Setup

        #region Initialization Tests

        [Test]
        public void Timer_Record_SetsCorrectDuration()
        {
            var timer = Timer.Record(DefaultDuration);
            
            Assert.AreEqual(DefaultDuration, timer.Duration);
            Assert.AreEqual(TimerState.Enabled, timer.State);
            Assert.AreEqual(0f, timer.PercentageDone());
        }

        [Test]
        public void Timer_Bind_RegistersOwnerReference()
        {
            var go = new GameObject("TestOwner");
            var owner = go.AddComponent<EmptyBehaviour>();
            var timer = Timer.Bind(owner, DefaultDuration);
            
            Assert.IsTrue(timer.HasReference);
            
            Object.DestroyImmediate(go);
        }

        #endregion Initialization Tests

        #region Execution Logic Tests

        [Test]
        public void Timer_Update_IncrementsElapsedTimeAndProgress()
        {
            var timer = Timer.Record(10f).Start();

            timer.Tick(DefaultDuration);

            Assert.AreEqual(DefaultDuration, timer.ElapsedTime);
            Assert.AreEqual(DefaultDuration/10f, timer.PercentageDone(), 0.001f);
            Assert.AreEqual(DefaultDuration, timer.TimeRemaining);
        }

        [Test]
        public void Timer_Update_TransitionsToCompleted_WhenDurationReached()
        {
            var timer = Timer.Record(DefaultDuration).Start();
            
            timer.Tick(DefaultDuration);

            if (timer.IsDone) timer.ExecuteComplete();

            Assert.AreEqual(TimerState.Completed, timer.State);
            Assert.IsTrue(timer.IsDone);
            Assert.AreEqual(1f, timer.PercentageDone());
        }

        [Test]
        public void Timer_Update_ReturnsFalse_WhenNotActivated()
        {
            var timer = Timer.Record(DefaultDuration);
            
            timer.Tick(1f);

            Assert.AreEqual(DefaultDuration, timer.TimeRemaining);
            Assert.AreEqual(0f, timer.ElapsedTime, "Timer should not process updates until Started.");
        }

        #endregion Execution Logic Tests

        #region Event Tests

        [Test]
        public void Timer_CompletedEvent_InvokedOnCompletion()
        {
            var wasInvoked = false;
            var timer = Timer.Record(DefaultDuration).Start();

            timer.Completed += () => wasInvoked = true;
            timer.Tick(DefaultDuration);
            
            if (timer.IsDone) timer.ExecuteComplete();

            Assert.IsTrue(wasInvoked, "The 'Completed' event was not triggered.");
        }

        [Test]
        public void Timer_CancelledEvent_InvokedOnCancel()
        {
            var wasInvoked = false;
            var timer = Timer.Record(DefaultDuration).Start();

            timer.Actions.Cancell += () => wasInvoked = true;
            timer.Cancel();

            Assert.IsTrue(wasInvoked, "The 'Cancell' event was not triggered.");
        }

        #endregion Event Tests

        #region Control Methods Tests

        [Test]
        public void Timer_Reload_RestoresInitialState()
        {
            var timer = Timer.Record(DefaultDuration).Start();

            timer.Tick(1f);
            timer.Reload();

            Assert.AreEqual(0f, timer.ElapsedTime);
            Assert.AreEqual(TimerState.Enabled, timer.State);
            Assert.IsFalse(timer.IsDone);
        }

        [Test]
        public void Timer_Reset_ClearsAllDataAndConfiguration()
        {
            var wasInvoked = false;
            var go = new GameObject("Owner");
            var owner = go.AddComponent<EmptyBehaviour>();
    
            var timer = Timer.Bind(owner, 10f)
                .Loop()
                .Start();

            timer.Completed += () => wasInvoked = true;
            timer.Tick(5f);
            timer.Reset();

            Assert.AreEqual(TimerState.Disable, timer.State, "State should be Disable.");
            Assert.IsFalse(timer.IsDone, "IsDone should be false.");
    
            Assert.AreEqual(0f, timer.ElapsedTime, "Elapsed time should be 0.");
            Assert.AreEqual(0f, timer.Duration, "Duration should be wiped to 0.");
            Assert.AreEqual(0f, timer.TimeRemaining, "TimeRemaining should be wiped to 0.");
    
            Assert.IsFalse(timer.HasLoop, "Looping flag should be cleared.");
            Assert.IsFalse(timer.HasReference, "Owner reference flag should be cleared.");
            Assert.IsTrue(timer.IsDestroyed, "Behaviour reference should be null.");
    
            timer.ForceCompletion();
            timer.ExecuteComplete();
    
            Assert.IsFalse(wasInvoked, "Callbacks should be cleared after Reset.");
    
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Timer_ForceCompletion_ImmediatelyFinalizes()
        {
            var wasInvoked = false;
            var timer = Timer.Record(DefaultDuration).Start();

            timer.Completed += () => wasInvoked = true;
            timer.ForceCompletion();
            
            if (timer.IsDone) timer.ExecuteComplete();

            Assert.AreEqual(TimerState.Completed, timer.State);
            Assert.AreEqual(1f, timer.PercentageDone());
            Assert.IsTrue(wasInvoked, "The 'Completed' event was not triggered.");
        }

        #endregion Control Methods Tests

        #region Advanced Behavior Tests

        [Test]
        public void Timer_Looping_ResetsAfterCompletion()
        {
            var restartCount = 0;
            var timer = Timer.Record(DefaultDuration).Loop().Start();

            timer.Actions.Restart += () => restartCount++;
            timer.Tick(DefaultDuration * 1.5f);
            
            if (timer.IsDone) timer.ExecuteRestart();

            Assert.AreEqual(TimerState.Activated, timer.State);
            Assert.AreEqual(1, restartCount, "The 'Restart' event was not triggered.");
            Assert.AreEqual(DefaultDuration * 0.5f, timer.ElapsedTime, 0.001f);
        }

        #endregion Advanced Behavior Tests
    }
}
