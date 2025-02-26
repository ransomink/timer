using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ransom.Tests
{
    public class Timer_RuntimeTests
    {
        private const float Delta = .0055f;

        [UnityTest]
        public IEnumerator Timer_CheckTimers()
        {
            var updateDispatcher = Resources.Load("Prefabs/S_UpdateDispatcher");
            var timerManager = Resources.Load("Prefabs/S_TimerExecutionOrder");
            Object.Instantiate(updateDispatcher);
            Object.Instantiate(timerManager);

            var dur    = 2f;
            var time   = Time.time;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            Assert.That(SO_TimerManager.Timers.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Timer_ExtendDuration()
        {
            var dur    = 2f;
            var time   = Time.time;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            var stamp  = timer.TimeRemaining;
            timer.ExtendDuration(dur);

            Assert.AreEqual(dur + dur, timer.Duration, Delta);
            Assert.AreEqual(stamp + dur, timer.TimeRemaining, Delta);
            Assert.AreEqual(time + dur + dur, timer.EndTime, Delta);
        }

        [UnityTest]
        public IEnumerator Timer_HasRestarted()
        {
            var updateDispatcher = Resources.Load("Prefabs/S_UpdateDispatcher");
            var timerManager = Resources.Load("Prefabs/S_TimerExecutionOrder");
            Object.Instantiate(updateDispatcher);
            Object.Instantiate(timerManager);

            var dur = 1f;
            var actions = new TimerActions();
            actions.Completed = () => Debug.Log($"Timer Completed.");

            var timer = new Timer(dur, actions);

            yield return new WaitForSeconds(dur);

            Assert.That(timer.State == TimerState.Completed, Is.True);

            actions.Restart += () => Debug.Log($"Timer Started.");
            timer.Restart();

            Assert.That(timer.Duration, Is.EqualTo(dur));
            Assert.That(timer.State == TimerState.Activated, Is.True);
        }

        [UnityTest]
        public IEnumerator Timer_HasResumed()
        {
            var dur    = 2f;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            var stamp  = timer.TimeRemaining;
            timer.Suspend();

            yield return new WaitForSeconds(3f);

            Assert.That(timer.IsSuspended, Is.True);

            timer.Resume();

            Assert.That(timer.IsSuspended, Is.False);
            Assert.That(Time.time + stamp, Is.EqualTo(timer.EndTime));

            yield return new WaitForSeconds(stamp);

            Assert.That(timer.IsDone, Is.True);
            Assert.That(timer.State == TimerState.Completed, Is.True);
        }
        
        [UnityTest]
        public IEnumerator Timer_IsCancelled()
        {
            var dur    = 2f;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            var stamp  = timer.TimeRemaining;
            timer.Cancel();

            yield return new WaitForSeconds(stamp);

            Assert.That(timer.IsDone, Is.False);
            Assert.That(timer.IsCancelled, Is.True);
            Assert.That(timer.IsSuspended, Is.False);
            Assert.That(timer.State == TimerState.Cancelled, Is.True);
            Assert.That(stamp, Is.EqualTo(timer.TimeRemaining));
        }
        
        [UnityTest]
        public IEnumerator Timer_IsComplete()
        {
            var dur    = 2f;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur);

            Assert.That(timer.IsDone, Is.True);
            Assert.That(timer.State == TimerState.Completed, Is.True);
        }
        
        [UnityTest]
        public IEnumerator Timer_IsSuspended()
        {
            var dur    = 2f;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            var stamp  = timer.TimeRemaining;
            timer.Suspend();

            yield return new WaitForSeconds(stamp);

            Assert.That(timer.IsDone, Is.False);
            Assert.That(timer.IsCancelled, Is.False);
            Assert.That(timer.IsSuspended, Is.True);
            Assert.That(timer.State == TimerState.Suspended, Is.True);
            Assert.That(timer.TimeRemaining, Is.EqualTo(stamp));
        }

        [UnityTest]
        public IEnumerator Timer_IsLooping()
        {
            var updateDispatcher = Resources.Load("Prefabs/S_UpdateDispatcher");
            var timerManager = Resources.Load("Prefabs/S_TimerExecutionOrder");
            Object.Instantiate(updateDispatcher);
            Object.Instantiate(timerManager);

            var dur = 1f;
            var count = 0;
            var actions = new TimerActions();
            actions.Restart += () => AddToCount();
            actions.Restart += () => Debug.Log($"Timer Started.");
            actions.Completed = () => Debug.Log($"Timer Completed.");

            var timer = new Timer(dur, actions, true);

            yield return new WaitForSeconds(2f);

            void AddToCount()
            {
                count++;
            }

            Assert.That(count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Timer_NewDuration()
        {
            var dur    = 2f;
            var time   = Time.time;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            timer.NewDuration(dur);
            var curTime = Time.time;

            Assert.That(timer.Duration,  Is.EqualTo(dur));
            Assert.That(timer.StartTime, Is.EqualTo(curTime));
            Assert.That(timer.EndTime,   Is.EqualTo(curTime + dur));
            Assert.That(timer.StartTime, Is.Not.EqualTo(time));
            Assert.That(timer.EndTime,   Is.Not.EqualTo(time + dur));
        }

        [UnityTest]
        public IEnumerator Timer_OnUpdate()
        {
            var timerManager = Resources.Load("Prefabs/S_TimerExecutionOrder");
            Object.Instantiate(timerManager);

            var isRunning = false;
            var x = Timer.Record(1f, () => Debug.Log("1 second timer completed after 1 second."));
            var y = Timer.Record(.995f, () => Debug.Log("1 second timer completed after .995 seconds."));
            var a = Timer.Record(1f, () => Debug.Log("This is a 1 second timer on a loop."), true);
            var b = Timer.Record(2f, () => Debug.Log("This is a 2 second timer suspended until 4 seconds."));
                    Timer.Record(1f, () => b.Suspend(true));
                    Timer.Record(1f, () => Debug.Log($"This is a 2 second timer suspended | Time Remaining: {b.TimeRemaining}"));
                    Timer.Record(1f, () => Assert.That(b.IsSuspended, Is.True, "This is a 2 second timer suspended after 1 second."));
                    Timer.Record(3f, b.Resume);
                    Timer.Record(3f, () => Assert.That(b.IsSuspended, Is.False, "This is a 2 second timer resumed after 3 seconds."));
            var c = Timer.Record(3f, () => Debug.Log("This is a 3 second timer."));
            var d = Timer.Record(4f, () => Debug.Log("This is a 4 second timer cancelled. You should not see this."));
                    Timer.Record(3f, d.Cancel);
                    Timer.Record(3f, () => Assert.That(d.IsCancelled, Is.True));
            var e = Timer.Record(4f, () => Debug.Log($"This is a 4 second timer. You will see this."));
                    Timer.Record(4f, () => Debug.Log($"1 second timer end time: {a.EndTime - a.Duration}"));
                    Timer.Record(4f, () => Debug.Log($"2 second timer end time: {b.EndTime}"));
                    Timer.Record(4f, () => Debug.Log($"4 second timer end time: {e.EndTime}"));
            var f = Timer.Record(5f, () => isRunning = false);
            isRunning = true;

            while (isRunning) { yield return null; }

            Assert.That(SO_TimerManager.Timers.Count, Is.GreaterThan(0));
            Assert.That(SO_TimerManager.Timers.Count, Is.EqualTo(1));
            Assert.That(b.IsDone, Is.True);
            Assert.That(c.IsDone, Is.True);
            Assert.That(d.IsDone, Is.False);
            Assert.That(e.IsDone, Is.True);
        }
        
        [UnityTest]
        public IEnumerator Timer_PercentageDone()
        {
            var dur    = 2f;
            var time   = Time.time;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            Assert.AreEqual(0.5f, timer.PercentageDone(), Delta);
        }

        [UnityTest]
        public IEnumerator Timer_Start()
        {
            var dur    = 2f;
            var time   = Time.time;
            var timer  = new Timer(true, false);
            yield return new WaitForSeconds(1f);

            timer.Start(dur);

            Assert.That(timer.State == TimerState.Activated, Is.True);
            Assert.AreEqual(time + (1f + dur), timer.EndTime, Delta);
        }

        [UnityTest]
        public IEnumerator Timer_SuspendedAndResumed()
        {
            var updateDispatcher = Resources.Load("Prefabs/S_UpdateDispatcher");
            var timerManager = Resources.Load("Prefabs/S_TimerExecutionOrder");
            var upd  = Object.Instantiate(updateDispatcher) as GameObject;
            var mgr  = Object.Instantiate(timerManager) as GameObject;
            var comp = mgr.AddComponent<MonoBehaviourTest>();

            var dur = 4f;
            var timer = new Timer(false, false);
            var actions = new TimerActions();
            actions.Resumed = () => Debug.Log($"Timer Resumed.");
            actions.Suspend = () => Debug.Log($"Timer Suspended.");
            timer.Set(comp, dur, actions);

            yield return new WaitForSeconds(2f);

            comp.enabled = false;

            yield return null;

            Assert.That(timer.Behaviour.enabled, Is.False);
            Assert.That(timer.IsSuspendedManually, Is.False);
            Assert.That(timer.State == TimerState.Suspended, Is.True);

            yield return new WaitForSeconds(1f);

            comp.enabled = true;
            
            yield return null;

            Assert.That(timer.Behaviour.enabled, Is.True);
            Assert.That(timer.State == TimerState.Activated, Is.True);

            yield return new WaitForSeconds(2f);

            Assert.That(timer.State == TimerState.Completed, Is.True);
        }
        
        [UnityTest]
        public IEnumerator Timer_TimeRemaining()
        {
            var dur    = 2f;
            var timer  = new Timer(dur);
            yield return new WaitForSeconds(dur / 2f);

            Assert.AreEqual(dur / 2f, timer.TimeRemaining, Delta);
        }
    }
}
