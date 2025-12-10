using System;

namespace Waterflow.Models
{
    /// <summary>
    /// 任务数据模型
    /// </summary>
    public class Task
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string Title { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? Deadline { get; set; }
        
        public string? Category { get; set; }
        
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
    }
    
    public enum TaskStatus
    {
        Pending,    // 待处理
        InProgress, // 进行中
        Completed,  // 已完成
        Suspended   // 已挂起
    }
}


