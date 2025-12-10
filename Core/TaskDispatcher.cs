using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Waterflow.Core;
using ModelsTask = Waterflow.Models.Task;

namespace Waterflow.Core
{
    /// <summary>
    /// 任务调度器 - 单例控制器，管理任务状态和 IO 隔离
    /// </summary>
    public class TaskDispatcher : INotifyPropertyChanged
    {
        private static readonly Lazy<TaskDispatcher> _instance = 
            new Lazy<TaskDispatcher>(() => new TaskDispatcher());
        
        public static TaskDispatcher Instance => _instance.Value;
        
        private readonly ObservableCollection<ModelsTask> _tasks = new();
        
        /// <summary>
        /// 可观察的任务列表（UI 绑定此属性）
        /// </summary>
        public ObservableCollection<ModelsTask> Tasks => _tasks;
        
        private TaskDispatcher()
        {
        }
        
        /// <summary>
        /// 创建新任务（乐观更新 + 异步落盘）
        /// </summary>
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
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

