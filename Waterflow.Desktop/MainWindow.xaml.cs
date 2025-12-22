using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Waterflow.Core;

namespace Waterflow.Desktop
{
    public partial class MainWindow : Window
    {
        // --- Native Constants & Structs ---
        private const int WM_USER = 0x0400;
        private const int WM_WATERFLOW_SIMULATE_CLICK = WM_USER + 1002;
        private const uint INJECTED_EVENT_SIGNATURE = 0xFF998877;
        private const int INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public MOUSEINPUT mi; }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx; public int dy; public uint mouseData; public uint dwFlags;
            public uint time; public UIntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        // --- Fields ---
        private Win32HookWrapper _hook;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += (s, e) => _hook?.Stop();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _hook = new Win32HookWrapper();

                // Handle events from the hook
                _hook.OnShowWheel += (x, y) => this.Title = $"Signal: Show Wheel at {x},{y}";
                _hook.OnGestureExecute += () => Console.WriteLine("Gesture Executed");

                // Hook into the window message loop
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(hwnd).AddHook(WndProc);

                // Install the C++ mouse hook
                if (_hook.Start(hwnd))
                    Console.WriteLine("Hook installed. Drag right-click to test.");
                else
                    MessageBox.Show("Failed to install hook.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Init Error: {ex.Message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 1. Handle simulate click request from C++ DLL
            if (msg == WM_WATERFLOW_SIMULATE_CLICK)
            {
                // We ignore coordinates (lParam) and click in-place to prevent drift
                PerformInPlaceRightClick();
                handled = true;
                return IntPtr.Zero;
            }

            // 2. Pass other messages to the wrapper for gesture logic
            _hook?.FilterMessage(msg, wParam, lParam);

            return IntPtr.Zero;
        }

        /// <summary>
        /// Simulates a right click at the CURRENT mouse position without moving it.
        /// </summary>
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