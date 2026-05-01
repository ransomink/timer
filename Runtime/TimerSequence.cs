using System;
using System.Collections.Generic;

namespace Ransom
{
    [Serializable]
    public class TimerSequence
    {
        public readonly struct Step
        {
            public readonly float  Duration;
            public readonly Action Callback;
            
            public Step(float duration, Action callback)
            {
                Duration = duration;
                Callback = callback;
            }
        }
        
        #region Fields
        
        private readonly List<Step> _steps = new List<Step>(8);
        private readonly Action     _cachedOnStepCompleted;
        
        private Action _onSequenceCompleted;
        private int    _currentStepIndex;
        private Timer  _internalTimer;
        private bool   _isUnscaled;

        #endregion Fields

        #region Constructors

        public TimerSequence(bool isUnscaled = false)
        {
            _isUnscaled = isUnscaled;
            _cachedOnStepCompleted = HandleOnStepCompleted;
        }

        #endregion Constructors
        
        #region Properties
        
        public bool IsRunning { get; private set; }

        #endregion Properties

        #region Methods

        public TimerSequence Append(float duration, Action onCompleted = null)
        {
            _steps.Add(new Step(duration, onCompleted));
            return this;
        }

        public void Cancel()
        {
            if (_internalTimer is null) return;
            
            _internalTimer.Cancel();
            _internalTimer = null;
        }
        
        public TimerSequence OnComplete(Action onSequenceCompleted)
        {
            _onSequenceCompleted = onSequenceCompleted;
            return this;
        }
    
        public void Pause() => _internalTimer?.Pause();

        public TimerSequence Play()
        {
            if (_steps.Count == 0) return this;
            if (IsRunning) Cancel();

            _internalTimer = TimerManager.Instance.RentTimer();
            
            IsRunning = true;
            PlayNextStep();
            return this;
        }

        public void Resume() => _internalTimer?.Resume();

        private void HandleOnStepCompleted()
        {
            _steps[_currentStepIndex - 1].Callback?.Invoke();
            
            PlayNextStep();
        }

        private void PlayNextStep()
        {
            if (_currentStepIndex >= _steps.Count)
            {
                Cancel();
                IsRunning = false;
                
                _currentStepIndex = 0;
                _onSequenceCompleted?.Invoke();
                
                return;
            }
            
            _internalTimer.Reset();
            
            if (_isUnscaled) _internalTimer.Realtime();
            
            var currentStep = _steps[_currentStepIndex++];

            _internalTimer.OnCompleted(_cachedOnStepCompleted);
            _internalTimer.Start(currentStep.Duration);
        }

        #endregion Methods
    }
}
