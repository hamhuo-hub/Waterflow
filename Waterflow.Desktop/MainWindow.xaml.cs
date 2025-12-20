using System.Windows;
using Waterflow.Core;
using System.Windows.Interop;

namespace Waterflow.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Win32HookWrapper _hook;
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // init wrapper
                _hook = new Win32HookWrapper();

                // regi event
                _hook.OnShowWheel += (x, y) =>
                {
                
                    Console.WriteLine($"【成功】收到 C++ 信号：在 ({x},{y}) 显示轮盘");
                    this.Title = $"信号接收成功! 坐标: {x},{y}";
                };

                _hook.OnGestureExecute += () =>
                {
                    Console.WriteLine("【成功】执行动作！");
                };

                // get handle
                var helper = new WindowInteropHelper(this);
                IntPtr hwnd = helper.Handle;

                // listen
                HwndSource source = HwndSource.FromHwnd(hwnd);
                source.AddHook(WndProc);

                // start
                bool success = _hook.Start(hwnd);
                if (success)
                {
                    Console.WriteLine("钩子安装成功！请按住右键拖拽...");
                }
                else
                {
                    MessageBox.Show("钩子安装失败！");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化错误: {ex.Message}");
            }

        }


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_hook != null)
            {
                _hook.FilterMessage(msg, wParam, lParam);
            }
            return IntPtr.Zero;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _hook?.Stop();
        }
    }
}