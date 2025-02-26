using UnityEngine;

namespace Ransom
{
    public class TimerExecutionOrder : Singleton<TimerExecutionOrder>
    {
        #region Properties

        [field: Space]
        [field: SerializeField] private SO_TimerManager TimerManager { get; set; }
        
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
        
        protected virtual void OnFixedUpdate()
        {
            StaticTime.OnFixedUpdate();
        }
    
        protected virtual void OnUpdate()
        {
            StaticTime.OnUpdate();
            TimerManager.OnUpdate();
        }
        
        #endregion Unity Callbacks
    }
}
