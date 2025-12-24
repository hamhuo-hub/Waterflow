using Microsoft.UI.Xaml;
using System;
using Waterflow.Core;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Waterflow.WinUI;

/// <summary>
/// Invisible, click-through overlay window:
/// - Receives global mouse hook messages via Win32 (hwnd)
/// - Renders the global glass-dot effect above all apps
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly IntPtr _hwnd;
    private readonly GestureHandler _gestureHandler;
    private readonly GlassDotOverlay _dotOverlay;
    private StickyNoteWindow? _stickyNoteWindow;

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        // Hide from taskbar/Alt-Tab and keep it non-interactive (message host only).
        Win32WindowStyles.MakeToolWindow(_hwnd);

        _gestureHandler = new GestureHandler(_hwnd);
        _gestureHandler.OnShowWheel += OnShowWheel;
        _gestureHandler.OnGestureMove += OnGestureMove;
        _gestureHandler.OnGestureExecute += OnGestureExecute;
        _gestureHandler.OnUpwardDrag += OnUpwardDrag;

        _dotOverlay = new GlassDotOverlay();

        Closed += (_, __) =>
        {
            _gestureHandler.Dispose();
            _dotOverlay.Dispose();
        };
    }

    private void OnShowWheel(int screenX, int screenY)
    {
        _dotOverlay.ShowAt(screenX, screenY);
    }

    private void OnGestureMove(int screenX, int screenY)
    {
        _dotOverlay.UpdateMouse(screenX, screenY);
    }

    private void OnGestureExecute()
    {
        _dotOverlay.Hide();
    }

    private void OnUpwardDrag(int screenX, int screenY)
    {
        _dotOverlay.Hide();

        DispatcherQueue.TryEnqueue(() => ShowStickyNoteAt(screenX, screenY));
    }

    private void ShowStickyNoteAt(int screenX, int screenY)
    {
        if (_stickyNoteWindow is null)
        {
            _stickyNoteWindow = new StickyNoteWindow();
            _stickyNoteWindow.Closed += (_, __) => _stickyNoteWindow = null;
        }

        _stickyNoteWindow.ShowAt(screenX, screenY);
    }

    internal void HideAtStartup()
    {
        // Ensure nothing is visible after activation.
        Win32WindowStyles.HideWindow(_hwnd);
    }
}
