using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Waterflow.WinUI;

/// <summary>
/// Win32 layered window overlay that renders the glass dot with per-pixel alpha.
/// This avoids WinUI's limitations around true transparent overlay windows.
/// </summary>
internal sealed class GlassDotOverlay : IDisposable
{
    private const int WINDOW_SIZE = 72; // includes soft shadow

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = unchecked((int)0x08000000);
    private const int WS_EX_TOPMOST = 0x00000008;

    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;

    private const uint ULW_ALPHA = 0x00000002;
    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;

    private const uint BI_RGB = 0;
    private const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref POINT pptDst,
        ref SIZE psize,
        IntPtr hdcSrc,
        ref POINT pptSrc,
        int crKey,
        ref BLENDFUNCTION pblend,
        uint dwFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr hdc,
        ref BITMAPINFO pbmi,
        uint iUsage,
        out IntPtr ppvBits,
        IntPtr hSection,
        uint dwOffset);

    private readonly IntPtr _hwnd;
    private readonly IntPtr _screenDc;
    private readonly IntPtr _memDc;
    private readonly IntPtr _hBitmap;
    private readonly IntPtr _oldBitmap;
    private readonly IntPtr _bits;
    private readonly byte[] _buffer;

    private bool _disposed;
    private bool _visible;
    private int _startX;
    private int _startY;
    private int _mouseX;
    private int _mouseY;

    public GlassDotOverlay()
    {
        int exStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        _hwnd = CreateWindowExW(
            exStyle,
            "STATIC",
            string.Empty,
            WS_POPUP,
            0,
            0,
            WINDOW_SIZE,
            WINDOW_SIZE,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWindowExW failed for GlassDotOverlay.");

        _screenDc = GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDC failed.");

        _memDc = CreateCompatibleDC(_screenDc);
        if (_memDc == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateCompatibleDC failed.");

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = WINDOW_SIZE,
                biHeight = -WINDOW_SIZE, // top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB
            }
        };

        _hBitmap = CreateDIBSection(_memDc, ref bmi, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_hBitmap == IntPtr.Zero || _bits == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateDIBSection failed.");

        _oldBitmap = SelectObject(_memDc, _hBitmap);
        _buffer = new byte[WINDOW_SIZE * WINDOW_SIZE * 4];

        // Ensure hidden by default.
        _ = ShowWindow(_hwnd, SW_HIDE);
    }

    public void ShowAt(int screenX, int screenY)
    {
        _startX = screenX;
        _startY = screenY;
        _mouseX = screenX;
        _mouseY = screenY;
        _visible = true;

        RenderAndCommit();
        _ = ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
    }

    public void UpdateMouse(int screenX, int screenY)
    {
        if (!_visible) return;
        _mouseX = screenX;
        _mouseY = screenY;
        RenderAndCommit();
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        _ = ShowWindow(_hwnd, SW_HIDE);
    }

    private void RenderAndCommit()
    {
        RenderToBuffer();
        Marshal.Copy(_buffer, 0, _bits, _buffer.Length);

        int left = _startX - (WINDOW_SIZE / 2);
        int top = _startY - (WINDOW_SIZE / 2);

        var ptDst = new POINT { x = left, y = top };
        var ptSrc = new POINT { x = 0, y = 0 };
        var size = new SIZE { cx = WINDOW_SIZE, cy = WINDOW_SIZE };

        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA
        };

        _ = UpdateLayeredWindow(_hwnd, _screenDc, ref ptDst, ref size, _memDc, ref ptSrc, 0, ref blend, ULW_ALPHA);
    }

    private void RenderToBuffer()
    {
        // A tiny, cheap pixel shader in CPU:
        // - Rounded "glass" blob with soft shadow and a highlight that follows mouse direction.
        int w = WINDOW_SIZE;
        int h = WINDOW_SIZE;
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;

        // Dot + shadow geometry
        float radius = 28f;
        float shadow = 8f;

        // Highlight
        float relX = _mouseX - _startX;
        float relY = _mouseY - _startY;
        float maxH = radius * 0.75f;
        float len = MathF.Sqrt(relX * relX + relY * relY);
        if (len > maxH && len > 0.001f)
        {
            float s = maxH / len;
            relX *= s;
            relY *= s;
        }
        float hx = cx + relX;
        float hy = cy + relY;
        float hRad = radius * 0.55f;
        float hInv = 1f / (hRad * hRad);

        int idx = 0;
        for (int y = 0; y < h; y++)
        {
            float dy = y - cy;
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                float a = 0f;
                float r = 0f, g = 0f, b = 0f;

                // Shadow outside the dot
                if (dist > radius && dist < radius + shadow)
                {
                    float t = 1f - ((dist - radius) / shadow);
                    float sa = t * t * 0.22f; // subtle shadow
                    a = sa;
                    r = g = b = 0f;
                }

                // Main dot
                if (dist <= radius + 1.2f)
                {
                    // Soft edge
                    float edge = 1f;
                    float feather = 1.6f;
                    if (dist > radius - feather)
                    {
                        edge = Math.Clamp((radius - dist) / feather, 0f, 1f);
                    }

                    float t = Math.Clamp(dist / radius, 0f, 1f);
                    float baseAlpha = 0.75f * edge;

                    // Radial brightness (glass-like)
                    float bright = 0.92f - 0.22f * t;

                    // Highlight contribution
                    float dhx = x - hx;
                    float dhy = y - hy;
                    float hDist2 = dhx * dhx + dhy * dhy;
                    float hTerm = MathF.Exp(-hDist2 * hInv); // 0..1
                    bright = MathF.Min(1f, bright + 0.18f * hTerm);
                    float ha = 0.10f * hTerm;

                    // Slight rim
                    float rim = MathF.Pow(t, 2.4f);
                    bright = MathF.Min(1f, bright + 0.06f * rim);

                    // Color (cool white)
                    float cr = bright;
                    float cg = bright;
                    float cb = bright * 1.02f;

                    float dotA = MathF.Min(1f, baseAlpha + ha);

                    // Combine with shadow already computed (pre-multiplied alpha blend)
                    float outA = a + dotA * (1f - a);
                    if (outA > 0f)
                    {
                        // pre-multiplied combine
                        r = (r * a + cr * dotA * (1f - a)) / outA;
                        g = (g * a + cg * dotA * (1f - a)) / outA;
                        b = (b * a + cb * dotA * (1f - a)) / outA;
                        a = outA;
                    }
                    else
                    {
                        a = 0f;
                        r = g = b = 0f;
                    }
                }

                // Clamp and write BGRA (pre-multiplied)
                byte A = (byte)Math.Clamp((int)(a * 255f), 0, 255);
                byte R = (byte)Math.Clamp((int)(r * A), 0, 255);
                byte G = (byte)Math.Clamp((int)(g * A), 0, 255);
                byte B = (byte)Math.Clamp((int)(b * A), 0, 255);

                _buffer[idx++] = B;
                _buffer[idx++] = G;
                _buffer[idx++] = R;
                _buffer[idx++] = A;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { Hide(); } catch { }

        if (_oldBitmap != IntPtr.Zero)
            _ = SelectObject(_memDc, _oldBitmap);

        if (_hBitmap != IntPtr.Zero)
            _ = DeleteObject(_hBitmap);

        if (_memDc != IntPtr.Zero)
            _ = DeleteDC(_memDc);

        if (_screenDc != IntPtr.Zero)
            _ = ReleaseDC(IntPtr.Zero, _screenDc);

        if (_hwnd != IntPtr.Zero)
            _ = DestroyWindow(_hwnd);
    }
}


