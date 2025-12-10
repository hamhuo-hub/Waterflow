using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Waterflow.UI
{
    /// <summary>
    /// Win32 API 辅助类 - 处理焦点抢占和归还、DPI 转换
    /// </summary>
    public static class Win32Helper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        
        /// <summary>
        /// 记录当前前台窗口句柄
        /// </summary>
        public static IntPtr SaveForegroundWindow()
        {
            return GetForegroundWindow();
        }
        
        /// <summary>
        /// 强制将窗口置于前台并获得焦点
        /// </summary>
        public static void ForceForeground(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                window.Loaded += (s, e) => ForceForeground(window);
                return;
            }
            
            // 获取当前前台窗口的线程 ID
            var foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(foregroundHwnd, out uint foregroundThreadId);
                uint currentThreadId = GetCurrentThreadId();
                
                // 如果线程不同，则附加线程输入
                if (foregroundThreadId != currentThreadId)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, true);
                    SetForegroundWindow(hwnd);
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
                else
                {
                    SetForegroundWindow(hwnd);
                }
            }
            else
            {
                SetForegroundWindow(hwnd);
            }
            
            // 确保窗口置顶
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            
            window.Activate();
            window.Focus();
        }
        
        /// <summary>
        /// 归还焦点到指定窗口
        /// </summary>
        public static void RestoreForeground(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                SetForegroundWindow(hwnd);
            }
        }
        
        /// <summary>
        /// 将物理像素坐标转换为 WPF 逻辑坐标（DPI 感知）
        /// </summary>
        public static Point PhysicalToLogical(Point physicalPoint, Window window)
        {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget != null)
            {
                var dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                var dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                
                return new Point(
                    physicalPoint.X * 96.0 / dpiX,
                    physicalPoint.Y * 96.0 / dpiY
                );
            }
            
            return physicalPoint;
        }
    }
}

