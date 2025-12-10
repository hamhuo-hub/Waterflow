using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Waterflow.UI
{
    /// <summary>
    /// 全局鼠标钩子 - 捕获右键向上拖拽手势
    /// </summary>
    public class GlobalMouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONUP = 0x0205;
        
        private LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private Point _rightButtonDownPos;
        private bool _isRightButtonDown = false;
        private const double DRAG_THRESHOLD = -50.0; // 向上拖拽阈值（像素）
        
        public event Action<Point>? RightDragUpDetected;
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        public GlobalMouseHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }
        
        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
            return SetWindowsHookEx(WH_MOUSE_LL, proc,
                GetModuleHandle(curModule?.ModuleName), 0);
        }
        
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var point = new Point(hookStruct.pt.x, hookStruct.pt.y);
                
                if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    _isRightButtonDown = true;
                    _rightButtonDownPos = point;
                }
                else if (wParam == (IntPtr)WM_MOUSEMOVE && _isRightButtonDown)
                {
                    var deltaY = point.Y - _rightButtonDownPos.Y;
                    
                    // 检测向上拖拽
                    if (deltaY < DRAG_THRESHOLD)
                    {
                        _isRightButtonDown = false;
                        
                        // 在 UI 线程上触发事件
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            RightDragUpDetected?.Invoke(point);
                        });
                    }
                }
                else if (wParam == (IntPtr)WM_RBUTTONUP)
                {
                    _isRightButtonDown = false;
                }
            }
            
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
        
        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}


