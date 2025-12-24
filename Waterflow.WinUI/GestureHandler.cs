using System;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Waterflow.Core;

namespace Waterflow.WinUI
{
    /// <summary>
    /// Handles gesture communication and Win32 API interactions
    /// </summary>
    public class GestureHandler : IDisposable
    {
        private const int INPUT_MOUSE = 0;
        private const int WM_USER = 0x0400;
        private const int WM_WATERFLOW_SIMULATE_CLICK = WM_USER + 1002;
        private const uint INJECTED_EVENT_SIGNATURE = 0xFF998877;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private IntPtr _hwnd;
        private Win32HookWrapper? _hook;
        private SUBCLASSPROC _subclassProc;

        // --- gesture state (for higher-level gestures in managed code) ---
        private const int UPWARD_DRAG_THRESHOLD_PX = 120;
        private int _gestureStartX;
        private int _gestureStartY;
        private bool _isGestureActive;
        private bool _upwardDragTriggered;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public MOUSEINPUT mi; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx; public int dy; public uint mouseData; public uint dwFlags;
            public uint time; public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("comctl32.dll", SetLastError = true)]
        static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass);

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // Events
        public event Action<int, int>? OnShowWheel;
        public event Action<int, int>? OnGestureMove;
        public event Action? OnGestureExecute;
        public event Action<int, int>? OnUpwardDrag;

        public GestureHandler(IntPtr hwnd)
        {
            _hwnd = hwnd;
            _subclassProc = new SUBCLASSPROC(WindowSubclassProc);

            SetWindowSubclass(_hwnd, _subclassProc, 0, IntPtr.Zero);
            InitializeHook();
        }

        private void InitializeHook()
        {
            _hook = new Win32HookWrapper();

            _hook.OnShowWheel += (x, y) =>
            {
                _isGestureActive = true;
                _upwardDragTriggered = false;
                _gestureStartX = x;
                _gestureStartY = y;
                OnShowWheel?.Invoke(x, y);
            };

            _hook.OnGestureMove += (x, y) =>
            {
                if (_isGestureActive && !_upwardDragTriggered)
                {
                    TryEmitUpwardDrag(x, y);
                }
                OnGestureMove?.Invoke(x, y);
            };

            _hook.OnGestureExecute += () =>
            {
                _isGestureActive = false;
                _upwardDragTriggered = false;
                OnGestureExecute?.Invoke();
            };

            _hook.Start(_hwnd);
        }

        private void TryEmitUpwardDrag(int x, int y)
        {
            int dx = x - _gestureStartX;
            int dy = y - _gestureStartY;

            // Upward: dy < 0 and exceeds threshold; also prefer "mostly vertical" gestures.
            if (dy <= -UPWARD_DRAG_THRESHOLD_PX && Math.Abs(dy) >= Math.Abs(dx) * 2)
            {
                _upwardDragTriggered = true;
                OnUpwardDrag?.Invoke(x, y);
            }
        }

        private IntPtr WindowSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData)
        {
            if (uMsg == WM_WATERFLOW_SIMULATE_CLICK)
            {
                PerformInPlaceRightClick();
                return IntPtr.Zero;
            }
            _hook?.FilterMessage((int)uMsg, wParam, lParam);
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        private void PerformInPlaceRightClick()
        {
            INPUT[] inputs = new INPUT[2];

            // Input 0: Right Button Down
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
            inputs[0].mi.dwExtraInfo = (UIntPtr)INJECTED_EVENT_SIGNATURE; // Vital: Prevents hook loop

            // Input 1: Right Button Up
            inputs[1].type = INPUT_MOUSE;
            inputs[1].mi.dwFlags = MOUSEEVENTF_RIGHTUP;
            inputs[1].mi.dwExtraInfo = (UIntPtr)INJECTED_EVENT_SIGNATURE; // Vital

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public Point ScreenToWindowPoint(int screenX, int screenY)
        {
            // Get window position on screen using Win32 API
            RECT windowRect;
            if (GetWindowRect(_hwnd, out windowRect))
            {
                // Convert to window inner coordinates
                double windowX = screenX - windowRect.Left;
                double windowY = screenY - windowRect.Top;

                return new Point(windowX, windowY);
            }

            // Fallback: return screen coordinates if GetWindowRect fails
            return new Point(screenX, screenY);
        }

        public void Dispose()
        {
            _hook?.Stop();
            RemoveWindowSubclass(_hwnd, _subclassProc, 0);
        }
    }
}

