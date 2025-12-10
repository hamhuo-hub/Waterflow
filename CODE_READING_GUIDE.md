# Waterflow 代码阅读指南

## 📚 阅读顺序（按交互流程）

### 第一阶段：应用启动与初始化

#### 1. **App.xaml.cs** (入口点)
```16:30:App.xaml.cs
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
```

**关键点**：
- 创建 UI 组件（但不显示）
- 绑定事件处理器
- 初始化全局鼠标钩子

---

### 第二阶段：手势检测（底层交互）

#### 2. **UI/GlobalMouseHook.cs** (全局鼠标钩子)
```71:97:UI/GlobalMouseHook.cs
private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
{
    if (nCode >= 0)
    {
        var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
        var point = new Point(hookStruct.pt.x, hookStruct.pt.y);
        
        if (wParam == (IntPtr)WM_RBUTTONDOWN)
        {
            _isRightButtonDown = true;
            _rightButtonDownPos = point;
        }
        else if (wParam == (IntPtr)WM_MOUSEMOVE && _isRightButtonDown)
        {
            var deltaY = point.Y - _rightButtonDownPos.Y;
            
            // 检测向上拖拽
            if (deltaY < DRAG_THRESHOLD)
            {
                _isRightButtonDown = false;
                
                // 在 UI 线程上触发事件
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    RightDragUpDetected?.Invoke(point);
                });
            }
        }
```

**关键点**：
- 使用 Win32 API 钩子监听全局鼠标事件
- 检测右键按下 + 向上移动（阈值 -50 像素）
- 触发 `RightDragUpDetected` 事件（在 UI 线程）

---

### 第三阶段：操作轮盘交互

#### 3. **App.xaml.cs** → `OnRightDragUpDetected` (事件处理)
```32:39:App.xaml.cs
private void OnRightDragUpDetected(Point screenPosition)
{
    // 在 UI 线程上显示操作轮盘
    Dispatcher.Invoke(() =>
    {
        _operationWheel?.ShowWheel(screenPosition);
    });
}
```

#### 4. **UI/OperationWheel.xaml.cs** → `ShowWheel` (显示轮盘)
```32:64:UI/OperationWheel.xaml.cs
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
```

**关键点**：
- 焦点管理：记录原窗口，强制获得焦点
- DPI 转换：物理像素 → WPF 逻辑坐标
- 边界检查：确保窗口在屏幕内

#### 5. **UI/OperationWheel.xaml.cs** → `WheelCanvas_MouseLeftButtonDown` (点击检测)
```122:146:UI/OperationWheel.xaml.cs
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
```

#### 6. **UI/OperationWheel.xaml.cs** → `GetSectorFromPosition` (角度计算)
```151:192:UI/OperationWheel.xaml.cs
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
```

**关键点**：
- 通过距离和角度判断点击区域
- 角度计算：`Atan2(dx, -dy)` 将坐标转换为角度（上方为 0 度）

---

### 第四阶段：输入框交互

#### 7. **App.xaml.cs** → `OnNewTaskRequested` (新建任务事件)
```41:53:App.xaml.cs
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
```

#### 8. **UI/FlightPanel.xaml.cs** → `ShowInputBox` (显示输入框)
```32:65:UI/FlightPanel.xaml.cs
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
```

#### 9. **UI/FlightPanel.xaml.cs** → `TaskInputBox_KeyDown` (提交任务)
```67:91:UI/FlightPanel.xaml.cs
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
```

---

### 第五阶段：任务创建与数据持久化（IO 隔离）

#### 10. **Core/TaskDispatcher.cs** → `CreateTask` (核心逻辑)
```34:53:Core/TaskDispatcher.cs
public void CreateTask(string title)
{
    if (string.IsNullOrWhiteSpace(title))
    {
        return;
    }
    
    var task = new ModelsTask
    {
        Title = title.Trim(),
        CreatedAt = DateTime.Now
    };
    
    // 步骤 1: 立即更新内存状态（乐观更新）
    // UI 会通过 ObservableCollection 自动收到通知
    _tasks.Add(task);
    
    // 步骤 2: 异步落盘（不阻塞主线程）
    WriteQueue.Instance.Enqueue(task);
}
```

**关键点**：
- **乐观更新**：立即更新内存，UI 瞬间响应
- **异步落盘**：通过队列异步写入，不阻塞 UI

#### 11. **Core/WriteQueue.cs** → `Enqueue` + `ProcessQueueAsync` (异步写入)
```32:36:Core/WriteQueue.cs
public void Enqueue(ModelsTask task)
{
    _queue.Enqueue(task);
    _semaphore.Release();
}
```

```41:80:Core/WriteQueue.cs
private async Task ProcessQueueAsync()
{
    while (!_cancellationTokenSource.Token.IsCancellationRequested)
    {
        try
        {
            await _semaphore.WaitAsync(_cancellationTokenSource.Token);
            
            // 批量处理：收集一批任务后一次性写入
            var batch = new List<ModelsTask>();
            
            while (_queue.TryDequeue(out var task) && batch.Count < 10)
            {
                batch.Add(task);
            }
            
            // 批量写入数据库
            foreach (var task in batch)
            {
                await InfoPool.Instance.InsertTaskAsync(task);
            }
            
            // 如果队列还有剩余，短暂延迟后继续处理
            if (!_queue.IsEmpty)
            {
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            // 日志记录错误（实际项目中应使用日志框架）
            System.Diagnostics.Debug.WriteLine($"WriteQueue 错误: {ex.Message}");
            // 继续处理，不中断循环
        }
    }
}
```

**关键点**：
- **削峰缓冲**：批量处理（最多 10 个任务）
- **后台线程**：不阻塞主线程
- **信号量机制**：`SemaphoreSlim` 控制处理节奏

---

## 🔄 完整交互流程图

```
用户操作
  │
  ├─ 右键向上拖拽
  │   │
  │   └─ GlobalMouseHook.HookCallback
  │       │
  │       └─ 检测到 deltaY < -50
  │           │
  │           └─ 触发 RightDragUpDetected 事件
  │               │
  │               └─ App.OnRightDragUpDetected
  │                   │
  │                   └─ OperationWheel.ShowWheel
  │                       │
  │                       ├─ 记录原窗口焦点
  │                       ├─ DPI 坐标转换
  │                       ├─ 设置窗口位置
  │                       └─ 强制获得焦点
  │
  ├─ 点击轮盘上方扇形
  │   │
  │   └─ OperationWheel.WheelCanvas_MouseLeftButtonDown
  │       │
  │       ├─ GetSectorFromPosition (角度计算)
  │       │   └─ 判断点击区域
  │       │
  │       ├─ HideWithAnimation (隐藏轮盘)
  │       │
  │       └─ 触发 NewTaskRequested 事件
  │           │
  │           └─ App.OnNewTaskRequested
  │               │
  │               └─ FlightPanel.ShowInputBox
  │                   │
  │                   ├─ 记录原窗口焦点
  │                   ├─ DPI 坐标转换
  │                   └─ 强制获得焦点
  │
  ├─ 输入任务名称 + Enter
  │   │
  │   └─ FlightPanel.TaskInputBox_KeyDown
  │       │
  │       └─ TaskDispatcher.CreateTask
  │           │
  │           ├─ 立即更新内存 (ObservableCollection)
  │           │   └─ UI 瞬间响应（如果有绑定）
  │           │
  │           └─ WriteQueue.Enqueue
  │               │
  │               └─ 后台线程 ProcessQueueAsync
  │                   │
  │                   ├─ 批量收集任务（最多 10 个）
  │                   └─ InfoPool.InsertTaskAsync
  │                       └─ SQLite 写入
  │
  └─ 输入框隐藏 + 焦点归还
      │
      └─ FlightPanel.HideWithAnimation
          │
          ├─ 淡出动画
          ├─ 隐藏窗口
          └─ Win32Helper.RestoreForeground
              └─ 归还焦点到原窗口
```

---

## 🎯 关键交互逻辑要点

### 1. **焦点管理**（Win32Helper.cs）
- **抢占焦点**：`AttachThreadInput` + `SetForegroundWindow`
- **归还焦点**：记录原窗口句柄，隐藏时恢复

### 2. **DPI 感知**（Win32Helper.cs）
- 物理像素 → WPF 逻辑坐标转换
- 使用 `PresentationSource.CompositionTarget.TransformToDevice`

### 3. **IO 隔离架构**
- **主线程**：立即更新内存（乐观更新）
- **后台线程**：异步批量写入数据库
- **UI 永不阻塞**

### 4. **事件驱动架构**
- 全局钩子 → 轮盘事件 → 输入框事件 → 核心逻辑
- 使用 C# 事件机制解耦组件

### 5. **窗口状态管理**
- 显示前：停止动画、重置状态
- 隐藏时：淡出动画、清理状态、归还焦点

---

## 📖 辅助阅读文件

### 工具类
- **UI/Win32Helper.cs**：Win32 API 封装（焦点、DPI）
- **Models/Task.cs**：数据模型
- **Data/InfoPool.cs**：SQLite 数据库访问

### UI 定义
- **UI/OperationWheel.xaml**：轮盘 UI 布局
- **UI/FlightPanel.xaml**：输入框 UI 布局

---

## 🔍 调试建议

1. **手势检测**：在 `GlobalMouseHook.HookCallback` 中添加日志
2. **角度计算**：在 `OperationWheel.GetSectorFromPosition` 中打印角度值
3. **焦点问题**：检查 `Win32Helper.ForceForeground` 的返回值
4. **IO 隔离**：观察 `WriteQueue` 的批量写入时机

