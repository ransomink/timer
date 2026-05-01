using UnityEngine;

namespace Ransom
{
    /// <summary>
    /// Provides a centralized collection of Timer Group IDs.
    /// This class is partial, allowing you to extend it with your own game-specific groups.
    /// Negative IDs are reserved for internal system use. 0 is Uncategorized.
    /// </summary>
    public static partial class TimerGroups
    {
        /// <summary> Default group for all timers.</summary>
        public const int None     =  0;
        
        /// <summary> Reserved for timers that should persist or behave globally.</summary>
        public const int Global   =  1;
        
        /// <summary> Reserved for internal system or engine-level timers.</summary>
        public const int Internal = -1;
        
        /// <summary> Reserved for debug or development use.</summary>
        public const int Debug    = -2;
        
        // Use Animator.StringToHash for guaranteed unique, cross-platform deterministic IDs.
        public static readonly int UI = Animator.StringToHash("UI");
    }
}
