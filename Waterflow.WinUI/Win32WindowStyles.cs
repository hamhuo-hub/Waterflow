using System;
using System.Runtime.InteropServices;

namespace Waterflow.WinUI;

internal static class Win32WindowStyles
{
    private const int GWL_EXSTYLE = -20;

    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_NOACTIVATE = unchecked((int)0x08000000);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateEllipticRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    public static void MakeToolWindow(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        ApplyFrame(hwnd);
    }

    public static void MakeClickThroughOverlay(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        ApplyFrame(hwnd);
    }

    public static void HideWindow(IntPtr hwnd)
    {
        _ = ShowWindow(hwnd, SW_HIDE);
    }

    public static void ShowNoActivate(IntPtr hwnd)
    {
        _ = ShowWindow(hwnd, SW_SHOWNOACTIVATE);
    }

    /// <summary>
    /// Best-effort bring-to-front + focus, used for "launcher-like" UX.
    /// Windows may deny focus stealing in some cases; this method tries the common workaround.
    /// </summary>
    public static void TryBringToForeground(IntPtr hwnd)
    {
        bool attached = false;
        uint attachedToThread = 0;
        try
        {
            var fg = GetForegroundWindow();
            if (fg != IntPtr.Zero)
            {
                uint fgThread = GetWindowThreadProcessId(fg, out _);
                uint curThread = GetCurrentThreadId();

                if (fgThread != curThread)
                {
                    attached = AttachThreadInput(curThread, fgThread, true);
                    if (attached)
                        attachedToThread = fgThread;
                }
            }

            _ = BringWindowToTop(hwnd);
            _ = SetForegroundWindow(hwnd);
        }
        catch
        {
            // best-effort
        }
        finally
        {
            if (attached && attachedToThread != 0)
            {
                try
                {
                    uint curThread = GetCurrentThreadId();
                    _ = AttachThreadInput(curThread, attachedToThread, false);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    /// <summary>
    /// Shapes the window to an ellipse based on its current client size (removes the ugly square boundary).
    /// Note: if the call happens too early (client rect 0), it returns false and can be retried later.
    /// </summary>
    public static bool TryApplyEllipticRegionToClient(IntPtr hwnd)
    {
        if (!GetClientRect(hwnd, out var rc))
            return false;

        int w = rc.Right - rc.Left;
        int h = rc.Bottom - rc.Top;
        if (w <= 0 || h <= 0)
            return false;

        // +1 to include the right/bottom edge.
        IntPtr region = CreateEllipticRgn(0, 0, w + 1, h + 1);
        if (region == IntPtr.Zero)
            return false;

        // On success, the system owns the region handle; do NOT delete it.
        int result = SetWindowRgn(hwnd, region, true);
        if (result == 0)
        {
            _ = DeleteObject(region);
            return false;
        }

        return true;
    }

    private static void ApplyFrame(IntPtr hwnd)
    {
        _ = SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_NOACTIVATE);
    }
}


