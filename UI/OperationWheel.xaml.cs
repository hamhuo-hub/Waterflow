using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Waterflow.UI
{
    /// <summary>
    /// 操作轮盘 - 任务交互界面
    /// </summary>
    public partial class OperationWheel : Window
    {
        public event Action? NewTaskRequested;
        public event Action? SuspendRequested;
        public event Action? CompleteRequested;
        
        private IntPtr _previousForegroundWindow;
        private const double Radius = 150.0;
        private const double CenterX = 150.0;
        private const double CenterY = 150.0;
        
        public OperationWheel()
        {
            InitializeComponent();
        }
        
        /// <summary>
        /// 在指定位置显示轮盘
        /// </summary>
        public void ShowWheel(Point screenPosition)
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
            
            // 设置窗口位置（轮盘中心在鼠标位置）
            Left = logicalPoint.X - Width / 2;
            Top = logicalPoint.Y - Height / 2;
            
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
            WheelCanvas.Focus();
        }
        
        private void WheelCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWithAnimation();
            }
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideWithAnimation();
            }
        }
        
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击的是窗口背景（不是 Canvas 内的元素），关闭轮盘
            if (e.OriginalSource == this)
            {
                HideWithAnimation();
            }
        }
        
        private void Sector_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Path path)
            {
                path.Opacity = 1.0;
            }
        }
        
        private void Sector_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Path path)
            {
                path.Opacity = 0.8;
            }
        }
        
        private void WheelCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(WheelCanvas);
            var angle = GetAngleFromCenter(position);
            
            // 高亮当前扇形区域（可选）
            // 这里可以根据角度高亮不同的扇形
        }
        
        private void WheelCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            // 重置所有扇形的透明度
            ResetSectorOpacity();
        }
        
        private void WheelCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(WheelCanvas);
            var sector = GetSectorFromPosition(position);
            
            // 隐藏轮盘
            HideWithAnimation();
            
            // 触发相应的事件
            switch (sector)
            {
                case WheelSector.NewTask:
                    NewTaskRequested?.Invoke();
                    break;
                case WheelSector.Suspend:
                    SuspendRequested?.Invoke();
                    break;
                case WheelSector.Complete:
                    CompleteRequested?.Invoke();
                    break;
                case WheelSector.None:
                    // 点击中心或外部，直接关闭
                    break;
            }
        }
        
        /// <summary>
        /// 根据鼠标位置判断点击了哪个扇形区域
        /// </summary>
        private WheelSector GetSectorFromPosition(Point position)
        {
            // 计算相对于中心的位置
            var dx = position.X - CenterX;
            var dy = position.Y - CenterY;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            
            // 如果距离太近（中心区域）或太远（外部），返回 None
            if (distance < 30 || distance > Radius)
            {
                return WheelSector.None;
            }
            
            // 计算角度（Atan2 返回 -180 到 180 度，0度在右侧，逆时针为正）
            // 我们需要转换为：0度在上方，顺时针为正
            var angle = Math.Atan2(dx, -dy) * 180.0 / Math.PI;
            
            // 转换为 0-360 度（上方为 0 度，顺时针为正）
            if (angle < 0) angle += 360;
            
            // 判断扇形区域（每个扇形 90 度）
            // 上方：315-45 度（跨越 0 度）
            if (angle >= 315 || angle < 45)
            {
                return WheelSector.NewTask; // 上方：新建任务
            }
            // 右侧：45-135 度
            else if (angle >= 45 && angle < 135)
            {
                return WheelSector.Suspend; // 右侧：任务挂起
            }
            // 下方：135-225 度
            else if (angle >= 135 && angle < 225)
            {
                return WheelSector.Complete; // 下方：任务完成
            }
            // 左侧：225-315 度
            else
            {
                return WheelSector.Reserved; // 左侧：预留
            }
        }
        
        private double GetAngleFromCenter(Point position)
        {
            var dx = position.X - CenterX;
            var dy = position.Y - CenterY;
            return Math.Atan2(-dy, dx) * 180.0 / Math.PI;
        }
        
        private void ResetSectorOpacity()
        {
            NewTaskSector.Opacity = 0.8;
            SuspendSector.Opacity = 0.8;
            CompleteSector.Opacity = 0.8;
            ReservedSector.Opacity = 0.8;
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
                Opacity = 1.0; // 重置透明度，为下次显示做准备
                ResetSectorOpacity();
                
                // 归还焦点到原窗口
                Win32Helper.RestoreForeground(_previousForegroundWindow);
            };
            
            BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        
        /// <summary>
        /// 扇形区域枚举
        /// </summary>
        private enum WheelSector
        {
            None,
            NewTask,    // 上方
            Suspend,    // 右侧
            Complete,   // 下方
            Reserved    // 左侧
        }
    }
}

