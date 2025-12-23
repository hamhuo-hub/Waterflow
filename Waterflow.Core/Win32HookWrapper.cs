using System.Runtime.InteropServices;
using System.Windows.Threading;
namespace Waterflow.Core
{
    public class Win32HookWrapper
    {
        // Delegate
        public delegate void GestureEventHandler(int x, int y);
        public delegate void SimpleEventHandler();

        // define gesture
        private enum SignalType
        {
            None = 0,
            ShowWheel = 1,   // 唤出轮盘
            UpdatePos = 2,   // 更新位置
            Execute = 3,     // 确定执行
            Cancel = 4       // 取消
        }

        // define ID
        private const int WM_USER = 0x0400;
        public const int WM_WATERFLOW_MSG = WM_USER + 1001;

        
        // dll import
        [DllImport("Waterflow.Native.dll", EntryPoint = "InstallHook")]
        private static extern bool StartMonitor(IntPtr hwndTarget);

        [DllImport("Waterflow.Native.dll", EntryPoint = "UninstallHook")]
        private static extern void StopMonitor();


        // BroadCast
        public event GestureEventHandler? OnShowWheel = null;
        public event GestureEventHandler? OnGestureMove = null;
        public event SimpleEventHandler? OnGestureExecute = null;


        public bool Start(IntPtr hwnd) {
            // dll calling
            return StartMonitor(hwnd);
        }

        public void Stop()
        {
            StopMonitor();
        }

        // message transfer 
        public void FilterMessage(int msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg != WM_WATERFLOW_MSG) return;

            int signal = (int)wParam;
            // low 16bit is x, high 16bit is y
            int x = (short)((int)lParam & 0xFFFF);
            int y = (short)((int)lParam >> 16 & 0xFFFF);

            switch ((SignalType)signal)
            {
                case SignalType.ShowWheel:
                    OnShowWheel?.Invoke(x, y);
                    break;

                case SignalType.UpdatePos:
                    OnGestureMove?.Invoke(x, y);
                    break;

                case SignalType.Execute:
                    OnGestureExecute?.Invoke();
                    break;
            }

        }
    }
}
