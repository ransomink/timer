namespace Ransom
{
    /// <summary>
    /// Centralized constants for the Ransom.Timer package.
    /// </summary>
    public static class TimerLib
    {
        public static class Folders
        {
            public const string Root = "Ransom/ScriptableObject/Manager/";
            public const string TimerFileName = "TimerManager";
            public const string TimerMenuName = Root + "Timer";
        }
        
        public static class Headers
        {
            public const string Config = "CONFIG";
            public const string Data = "DATA";
            public const string State = "STATE";
        }
        
        public static class Tooltips
        {
            public const string State = "The current active state.";
            public const string GroupId = "Categorizes the timer for bulk operations like cancellation.";
            public const string IsDirty =
                "If the state was modified externally without following the regular complete conditions.";
        
            public const string CanLoop = "If configured to automatically restart after reaching its end.";
            public const string HasReference = "If the life cycle is assoicated with a MonoBehaviour reference.";
            public const string UseUnscaledTime =
                "If configured to operate independently of the timescale, making it unaffected by pause or slow-motion.";

            public const string IsSuspendedManually = "If manually suspended. Can be resumed using the 'Resume' method.";
            public const string StartTime = "The recorded start time in seconds.";
            public const string Duration = "The length of time in seconds.";
            public const string EndTime = "The time in seconds until completion.";
            public const string SuspendedTime = "The time in seconds when the timer was suspended.";
            public const string TimeRemaining = "The remaining time in seconds until completion.";
            public const string IsDelayPhase = "If currently in the delay phase before the main duration starts.";
            public const string DelayDuration = "The length of the initial delay in seconds.";
            public const string UseFrames = "Use frame-based timing.";
            public const string UseSeconds = "Use time-based timing.";
            public const string IntervalFrames = "The number of frames between each operation.";
            public const string IntervalSeconds = "The number of seconds between each operation.";
        }
    }
}
