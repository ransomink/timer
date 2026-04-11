using UnityEngine;

namespace Ransom
{
    public class TimerExecutionOrder : Singleton<TimerExecutionOrder>
    {
        #region Properties

        [field: Space]
        [field: SerializeField] private TimerManager TimerManager { get; set; }
        
        #endregion Properties

        #region Unity Callbacks

        protected virtual void OnEnable()
        {
            UpdateDispatcher.OnFixedUpdate += OnFixedUpdate;
            UpdateDispatcher.OnUpdate += OnUpdate;
        }

        protected virtual void OnDisable()
        {
            UpdateDispatcher.OnFixedUpdate -= OnFixedUpdate;
            UpdateDispatcher.OnUpdate -= OnUpdate;
        }
        
        protected virtual void OnFixedUpdate(float fixedDeltaTime)
        {
            StaticTime.OnFixedUpdate();
        }
    
        protected virtual void OnUpdate(float deltaTime)
        {
            StaticTime.OnUpdate();
            TimerManager.OnUpdate(deltaTime);
        }
        
        #endregion Unity Callbacks
    }
}
