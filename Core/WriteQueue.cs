using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Waterflow.Data;
using ModelsTask = Waterflow.Models.Task;

namespace Waterflow.Core
{
    /// <summary>
    /// 写入队列 - 削峰缓冲区，实现 IO 隔离
    /// </summary>
    public class WriteQueue
    {
        private static readonly Lazy<WriteQueue> _instance = new Lazy<WriteQueue>(() => new WriteQueue());
        public static WriteQueue Instance => _instance.Value;
        
        private readonly ConcurrentQueue<ModelsTask> _queue = new();
        private readonly SemaphoreSlim _semaphore = new(0);
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _backgroundWorker;
        
        private WriteQueue()
        {
            _backgroundWorker = Task.Run(ProcessQueueAsync);
        }
        
        /// <summary>
        /// 将任务加入写入队列（非阻塞）
        /// </summary>
        public void Enqueue(ModelsTask task)
        {
            _queue.Enqueue(task);
            _semaphore.Release();
        }
        
        /// <summary>
        /// 后台线程批量处理写入
        /// </summary>
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
        
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _backgroundWorker.Wait(TimeSpan.FromSeconds(5));
            _semaphore.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}

