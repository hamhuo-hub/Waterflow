# Waterflow - 极速任务捕获系统

基于 C# (.NET 8) + WPF 的 Windows 桌面应用，实现毫秒级任务捕获与录入。

## 核心特性

- **极速响应**：右键向上拖拽手势触发，毫秒级显示输入框
- **IO 隔离架构**：UI 永不阻塞，异步写入数据库
- **焦点智能管理**：自动抢占和归还焦点，无缝切换工作流
- **DPI 感知**：完美适配高分辨率屏幕

## 技术架构

### 三层架构

1. **UI Layer (Presentation)**
   - `FlightPanel`: 悬浮输入框窗口
   - `GlobalMouseHook`: 全局鼠标钩子，捕获右键拖拽手势

2. **Logic Layer (In-Memory)**
   - `TaskDispatcher`: 单例任务调度器，管理任务状态
   - `ObservableCollection<Task>`: 响应式状态管理
   - `WriteQueue`: 异步写入队列，实现 IO 隔离

3. **Data Layer (Persistence)**
   - `InfoPool`: SQLite 数据库访问层

## 使用方式

1. 运行应用程序（后台静默运行）
2. 在任意位置按住鼠标右键并向上拖拽
3. 输入框立即出现在鼠标位置
4. 输入任务名称，按 `Enter` 提交
5. 输入框自动消失，焦点归还给原窗口

## 项目结构

```
Waterflow/
├── Models/
│   └── Task.cs                 # 任务数据模型
├── Data/
│   └── InfoPool.cs             # SQLite 数据库访问
├── Core/
│   ├── TaskDispatcher.cs       # 任务调度器（单例）
│   └── WriteQueue.cs           # 异步写入队列
├── UI/
│   ├── FlightPanel.xaml        # 悬浮输入框 UI
│   ├── FlightPanel.xaml.cs     # 输入框逻辑
│   ├── GlobalMouseHook.cs      # 全局鼠标钩子
│   └── Win32Helper.cs          # Win32 API 辅助（焦点、DPI）
├── App.xaml                     # WPF 应用入口
├── App.xaml.cs                  # 应用启动逻辑
└── Waterflow.csproj            # 项目文件
```

## 关键技术点

### 1. 焦点抢占与归还

使用 Win32 API `AttachThreadInput` 和 `SetForegroundWindow` 实现跨进程焦点管理。

### 2. DPI 感知

通过 `PresentationSource.CompositionTarget.TransformToDevice` 将物理像素转换为 WPF 逻辑坐标。

### 3. IO 隔离

- 主线程：立即更新内存状态（乐观更新）
- 后台线程：批量异步写入数据库
- UI 永不阻塞

## 开发环境

- .NET 8.0
- Windows 10/11
- Visual Studio 2022 或更高版本

## 依赖包

- `Microsoft.Data.Sqlite` (8.0.0)

## 构建与运行

```bash
dotnet restore
dotnet build
dotnet run
```

## 注意事项

- 首次运行会在 `%LocalAppData%\Waterflow\` 创建 SQLite 数据库
- 需要管理员权限才能安装全局鼠标钩子（某些情况下）
- 建议在系统启动时自动运行


