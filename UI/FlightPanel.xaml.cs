using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Waterflow.Core;
using Waterflow.UI;

namespace Waterflow.UI
{
    /// <summary>
    /// FlightPanel - 悬浮输入框窗口（仪表盘 UI）
    /// </summary>
    public partial class FlightPanel : Window
    {
        private IntPtr _previousForegroundWindow;
        
        public FlightPanel()
        {
            InitializeComponent();
            Loaded += FlightPanel_Loaded;
        }
        
        private void FlightPanel_Loaded(object sender, RoutedEventArgs e)
        {
            TaskInputBox.Focus();
        }
        
        /// <summary>
        /// 在指定位置显示输入框
        /// </summary>
        public void ShowInputBox(Point screenPosition)
        {
            // 停止任何正在运行的动画
            BeginAnimation(UIElement.OpacityProperty, null);
            
            // 确保窗口处于可见状态
            Opacity = 1.0;
            Visibility = Visibility.Visible;
            
            // 记录当前前台窗口（用于焦点归还）
            _previousForegroundWindow = Win32Helper.SaveForegroundWindow();
            
            // DPI 感知的坐标转换
            var logicalPoint = Win32Helper.PhysicalToLogical(screenPosition, this);
            
            // 设置窗口位置（输入框在鼠标位置稍微偏移）
            Left = logicalPoint.X - Width / 2;
            Top = logicalPoint.Y - Height - 20;
            
            // 确保窗口在屏幕范围内
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            
            if (Left < 0) Left = 10;
            if (Top < 0) Top = 10;
            if (Left + Width > screenWidth) Left = screenWidth - Width - 10;
            if (Top + Height > screenHeight) Top = screenHeight - Height - 10;
            
            // 显示窗口并强制获得焦点
            Show();
            Win32Helper.ForceForeground(this);
            TaskInputBox.Focus();
            TaskInputBox.SelectAll();
        }
        
        private void TaskInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var title = TaskInputBox.Text;
                
                // 空输入直接关闭（视作误触）
                if (string.IsNullOrWhiteSpace(title))
                {
                    HideWithAnimation();
                    return;
                }
                
                // 步骤 5: 调用核心层创建任务
                TaskDispatcher.Instance.CreateTask(title);
                
                // 立即隐藏并归还焦点
                HideWithAnimation();
            }
            else if (e.Key == Key.Escape)
            {
                // 取消：直接关闭
                HideWithAnimation();
            }
        }
        
        private void TaskInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 如果失去焦点且不是按 Enter/Esc，也关闭窗口
            // 这处理了用户点击外部区域的情况
            if (!IsKeyboardFocusWithin)
            {
                HideWithAnimation();
            }
        }
        
        /// <summary>
        /// 播放收起动画并隐藏窗口
        /// </summary>
        private void HideWithAnimation()
        {
            // 如果窗口已经隐藏，直接返回
            if (Visibility != Visibility.Visible)
            {
                return;
            }
            
            // 简单的淡出动画
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(150)
            };
            
            fadeOut.Completed += (s, e) =>
            {
                // 停止动画
                BeginAnimation(UIElement.OpacityProperty, null);
                
                // 隐藏窗口
                Hide();
                Visibility = Visibility.Hidden;
                
                // 重置状态
                TaskInputBox.Clear();
                Opacity = 1.0; // 重置透明度，为下次显示做准备
                
                // 归还焦点到原窗口
                Win32Helper.RestoreForeground(_previousForegroundWindow);
            };
            
            BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
    }
}

