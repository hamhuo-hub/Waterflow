using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Waterflow.WinUI
{
    /// <summary>
    /// Main window that coordinates gesture handling and animations
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private IntPtr _hwnd;
        private GestureHandler? _gestureHandler;
        private DotAnimationManager? _animationManager;
        private RadialGradientBrush? _highlightBorder;
        private Microsoft.UI.Xaml.Shapes.Ellipse? _highlightDot;

        public MainWindow()
        {
            InitializeComponent();
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Initialize highlight elements when window is activated
            this.Activated += MainWindow_Activated;

            this.Closed += (s, e) =>
            {
                _gestureHandler?.Dispose();
            };
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Find and save highlight elements (only once)
            if (_highlightBorder == null || _highlightDot == null)
            {
                var content = this.Content as FrameworkElement;
                _highlightBorder = content?.FindName("HighlightBorder") as RadialGradientBrush;
                _highlightDot = content?.FindName("HighlightDot") as Microsoft.UI.Xaml.Shapes.Ellipse;
            }

            // Initialize managers (only once)
            if (_gestureHandler == null)
            {
                _gestureHandler = new GestureHandler(_hwnd);
                _gestureHandler.OnShowWheel += OnShowWheel;
                _gestureHandler.OnGestureMove += OnGestureMove;
                _gestureHandler.OnGestureExecute += OnGestureExecute;
            }

            if (_animationManager == null)
            {
                _animationManager = new DotAnimationManager(GlassDot, _highlightBorder, _highlightDot);
            }
        }

        private void OnShowWheel(int screenX, int screenY)
        {
            // test output: show coordinates in title bar
            this.Title = $"开始手势: {screenX}, {screenY}";

            // Convert screen coordinates to window coordinates
            var windowPoint = _gestureHandler!.ScreenToWindowPoint(screenX, screenY);

            // Show dot at position with animation
            _animationManager?.ShowAtPosition(windowPoint);
        }

        private void OnGestureMove(int screenX, int screenY)
        {
            // test output: show coordinates in title bar
            this.Title = $"拖动中: {screenX}, {screenY}";

            // Convert screen coordinates to window coordinates
            var windowPoint = _gestureHandler!.ScreenToWindowPoint(screenX, screenY);

            // Update dot position with elastic effect
            _animationManager?.UpdatePosition(windowPoint);
        }

        private void OnGestureExecute()
        {
            // test output: show status in title bar
            this.Title = "执行手势效果";

            // Hide dot with animation
            _animationManager?.Hide();
        }
    }
}
