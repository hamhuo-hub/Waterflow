using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Waterflow.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Waterflow.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // --NATIVE FIELD--
        private const int INPUT_MOUSE = 0;
        private const int WM_USER = 0x0400;
        private const int WM_WATERFLOW_SIMULATE_CLICK = WM_USER + 1002;
        private const uint INJECTED_EVENT_SIGNATURE = 0xFF998877;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        // Field
        private IntPtr _hwnd;
        private Win32HookWrapper _hook;

        private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, uint uIdSubclass, IntPtr dwRefData);
        private readonly SUBCLASSPROC _subclassProc;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public MOUSEINPUT mi; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx; public int dy; public uint mouseData; public uint dwFlags;
            public uint time; public UIntPtr dwExtraInfo;
        }

        [DllImport("comctl32.dll", SetLastError = true)]
        static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

        [DllImport("comctl32.dll", SetLastError = true)]
        static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("comctl32.dll", SetLastError = true)]
        static extern bool RemoveWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass);

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        public MainWindow()
        {
            InitializeComponent();
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this); // get handle
            _subclassProc = new SUBCLASSPROC(WindowSubclassProc); // AVOID GC

            SetWindowSubclass(_hwnd, _subclassProc, 0, IntPtr.Zero);

            InitializeHook();

            this.Closed += (s, e) =>
            {
                _hook?.Stop();
                RemoveWindowSubclass(_hwnd, _subclassProc, 0);
            };

        }

        private void InitializeHook()
        {
            _hook = new Win32HookWrapper();
            _hook.OnShowWheel += (x, y) => { this.Title = $"【开始】坐标: {x}, {y}"; };

            _hook.OnGestureMove += (x, y) =>
            {
                this.Title = $"【拖拽中】坐标: {x}, {y}";
            };

            _hook.OnGestureExecute += () =>
            {
                this.Title = "【执行】动作生效！";
            };

            _hook.Start(_hwnd);
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
    }
}