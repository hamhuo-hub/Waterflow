using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using Waterflow.Core;
using WinRT.Interop;

namespace Waterflow.WinUI;

public sealed partial class StickyNoteWindow : Window
{
    private readonly IntPtr _hwnd;
    private AppWindow? _appWindow;
    private OverlappedPresenter? _presenter;
    private bool _everActivated;
    private DateTimeOffset _suppressBlurDismissUntilUtc;
    private bool _isHiding;
    private bool _inputLoaded;
    private int _focusRetryToken;

    public StickyNoteWindow()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        Win32WindowStyles.MakeToolWindow(_hwnd);

        Input.Loaded += (_, __) => _inputLoaded = true;

        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                // During focus-steal attempts Windows may emit a transient Deactivated.
                // Never call Close() from Activated handler (can crash WinUI); hide asynchronously instead.
                if (_everActivated && DateTimeOffset.UtcNow >= _suppressBlurDismissUntilUtc)
                {
                    RequestHide();
                }
                return;
            }

            _everActivated = true;
            FocusInputWithRetry();
        };
    }

    public void ShowAt(int screenX, int screenY)
    {
        _isHiding = false;
        _everActivated = false;
        _suppressBlurDismissUntilUtc = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(250);

        Activate();
        EnsureAppWindowInitialized();

        var size = _appWindow!.Size;
        var pos = ComputePosition(screenX, screenY, size.Width, size.Height);
        _appWindow.Move(pos);

        // Defer focus-steal to avoid re-entrancy into Activated while we're still in ShowAt call stack.
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_isHiding) return;
            Win32WindowStyles.TryBringToForeground(_hwnd);
            FocusInputWithRetry();
        });
    }

    private void EnsureAppWindowInitialized()
    {
        // NOTE:
        // Calling AppWindow.Resize too early (e.g., during ctor) can crash (AccessViolation) on some machines.
        // We only initialize AppWindow after the WinUI Window has been activated at least once.
        if (_appWindow != null)
            return;

        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        _presenter = _appWindow.Presenter as OverlappedPresenter;
        if (_presenter != null)
        {
            _presenter.IsResizable = false;
            _presenter.IsMaximizable = false;
            _presenter.IsMinimizable = false;
            _presenter.IsAlwaysOnTop = true;
            _presenter.SetBorderAndTitleBar(false, false);
        }

        // Make sure size matches our XAML card.
        _appWindow.Resize(new Windows.Graphics.SizeInt32(360, 220));
    }

    private void FocusInputWithRetry()
    {
        int token = ++_focusRetryToken;
        DispatcherQueue.TryEnqueue(() => TryFocusOnce(token, remaining: 10));
    }

    private void TryFocusOnce(int token, int remaining)
    {
        if (_isHiding) return;
        if (token != _focusRetryToken) return;

        // First show: TextBox may not be fully in the visual tree yet.
        if (!_inputLoaded)
        {
            if (remaining <= 0) return;
            _ = Task.Delay(25).ContinueWith(_ => DispatcherQueue.TryEnqueue(() => TryFocusOnce(token, remaining - 1)));
            return;
        }

        bool ok = Input.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        if (ok)
        {
            try { Input.SelectAll(); } catch { }
            return;
        }

        if (remaining <= 0) return;
        _ = Task.Delay(40).ContinueWith(_ => DispatcherQueue.TryEnqueue(() => TryFocusOnce(token, remaining - 1)));
    }

    private static Windows.Graphics.PointInt32 ComputePosition(int screenX, int screenY, int width, int height)
    {
        // Prefer to show above the cursor (since gesture is upward), with a small gap.
        int left = screenX - (width / 2);
        int top = screenY - height - 16;

        var display = DisplayArea.GetFromPoint(
            new Windows.Graphics.PointInt32(screenX, screenY),
            DisplayAreaFallback.Primary);

        var wa = display.WorkArea;
        left = Math.Clamp(left, wa.X, wa.X + wa.Width - width);
        top = Math.Clamp(top, wa.Y, wa.Y + wa.Height - height);

        return new Windows.Graphics.PointInt32(left, top);
    }

    private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            Submit();
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            RequestHide();
        }
    }

    private void Submit()
    {
        var title = Input.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        TaskDispatcher.Instance.CreateTask(title);
        RequestHide();
    }

    private void RequestHide()
    {
        if (_isHiding) return;
        _isHiding = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                Input.Text = string.Empty;
            }
            catch
            {
                // ignore
            }

            Win32WindowStyles.HideWindow(_hwnd);
        });
    }
}


