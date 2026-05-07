using UnityEngine;

namespace Game.GameFlow
{
    /// <summary>
    /// 联机双开时失焦窗口也必须持续跑帧，否则敌人 AI、实时同步和加载进度都会跟着变慢。
    /// </summary>
    public static class RuntimeFrameRatePolicy
    {
        private const int TargetFrameRate = 120;
        private static RuntimeFrameRatePolicyRunner _runner;
        private static bool _windowsRealtimePolicyApplied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            EnsureRunner();
            ApplyPolicy();
        }

        internal static void ApplyPolicy()
        {
            Application.runInBackground = true;
            Application.backgroundLoadingPriority = ThreadPriority.High;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            UnityEngine.Rendering.OnDemandRendering.renderFrameInterval = 1;
            ApplyThreadPriority();
            ApplyWindowsRealtimePolicy();
        }

        private static void EnsureRunner()
        {
            if (_runner != null)
                return;

            GameObject runnerObject = new GameObject(nameof(RuntimeFrameRatePolicy));
            Object.DontDestroyOnLoad(runnerObject);
            _runner = runnerObject.AddComponent<RuntimeFrameRatePolicyRunner>();
        }

        private static void ApplyThreadPriority()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                System.Threading.ThreadPriority priority = System.Threading.Thread.CurrentThread.Priority;
                if (priority == System.Threading.ThreadPriority.Lowest ||
                    priority == System.Threading.ThreadPriority.BelowNormal ||
                    priority == System.Threading.ThreadPriority.Normal)
                {
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
                }
            }
            catch
            {
            }
#endif
        }

        private static void ApplyWindowsRealtimePolicy()
        {
            if (_windowsRealtimePolicyApplied)
                return;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                Win32TimeBeginPeriod(1);
            }
            catch
            {
            }

            try
            {
                using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
                if (process.PriorityClass == System.Diagnostics.ProcessPriorityClass.Idle ||
                    process.PriorityClass == System.Diagnostics.ProcessPriorityClass.BelowNormal ||
                    process.PriorityClass == System.Diagnostics.ProcessPriorityClass.Normal)
                {
                    process.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
                }

                var throttling = new Win32ProcessPowerThrottlingState
                {
                    Version = Win32ProcessPowerThrottlingCurrentVersion,
                    ControlMask = Win32ProcessPowerThrottlingExecutionSpeed,
                    StateMask = 0,
                };
                Win32SetProcessInformation(
                    process.Handle,
                    Win32ProcessInformationClass.ProcessPowerThrottling,
                    ref throttling,
                    (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32ProcessPowerThrottlingState>());
            }
            catch
            {
            }
#endif

            _windowsRealtimePolicyApplied = true;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint Win32ProcessPowerThrottlingCurrentVersion = 1;
        private const uint Win32ProcessPowerThrottlingExecutionSpeed = 0x1;

        private enum Win32ProcessInformationClass
        {
            ProcessPowerThrottling = 4,
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct Win32ProcessPowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Win32SetProcessInformation(
            System.IntPtr process,
            Win32ProcessInformationClass processInformationClass,
            ref Win32ProcessPowerThrottlingState processInformation,
            uint processInformationSize);

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint Win32TimeBeginPeriod(uint periodMilliseconds);
#endif
    }

    internal sealed class RuntimeFrameRatePolicyRunner : MonoBehaviour
    {
        private const float ReapplyIntervalSeconds = 0.5f;
        private float _nextReapplyTime;

        private void Awake()
        {
            RuntimeFrameRatePolicy.ApplyPolicy();
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextReapplyTime)
                return;

            _nextReapplyTime = Time.realtimeSinceStartup + ReapplyIntervalSeconds;
            RuntimeFrameRatePolicy.ApplyPolicy();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            RuntimeFrameRatePolicy.ApplyPolicy();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            RuntimeFrameRatePolicy.ApplyPolicy();
        }
    }
}
