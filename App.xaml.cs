using System;
using System.Windows;
using Waterflow.UI;

namespace Waterflow
{
    /// App.xaml Entry
    public partial class App : Application
    {
        private GlobalMouseHook? _mouseHook;
        private OperationWheel? _operationWheel;
        private FlightPanel? _flightPanel;
        
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 创建操作轮盘和输入框（但不立即显示）
            _operationWheel = new OperationWheel();
            _flightPanel = new FlightPanel();
            
            // 绑定轮盘事件
            _operationWheel.NewTaskRequested += OnNewTaskRequested;
            _operationWheel.SuspendRequested += OnSuspendRequested;
            _operationWheel.CompleteRequested += OnCompleteRequested;
            
            // 初始化全局鼠标钩子
            _mouseHook = new GlobalMouseHook();
            _mouseHook.RightDragUpDetected += OnRightDragUpDetected;
        }
        
        private void OnRightDragUpDetected(Point screenPosition)
        {
            // 在 UI 线程上显示操作轮盘
            Dispatcher.Invoke(() =>
            {
                _operationWheel?.ShowWheel(screenPosition);
            });
        }
        
        private void OnNewTaskRequested()
        {
            // 显示输入框（在轮盘关闭后）
            Dispatcher.Invoke(() =>
            {
                // 获取轮盘的中心位置作为输入框显示位置
                var wheelCenter = new Point(
                    _operationWheel!.Left + _operationWheel.Width / 2,
                    _operationWheel.Top + _operationWheel.Height / 2
                );
                _flightPanel?.ShowInputBox(wheelCenter);
            });
        }
        
        private void OnSuspendRequested()
        {
            // TODO: 实现任务挂起/焦点切换功能
            System.Diagnostics.Debug.WriteLine("任务挂起/焦点切换");
        }
        
        private void OnCompleteRequested()
        {
            // TODO: 实现任务完成功能
            System.Diagnostics.Debug.WriteLine("任务完成");
        }
        
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // 清理资源
            _mouseHook?.Dispose();
            _operationWheel?.Close();
            _flightPanel?.Close();
        }
    }
}

