using System.Collections.Concurrent;
using System.Diagnostics;

namespace Waterflow.Core;

/// <summary>
/// Peak-shaving async write buffer. Keeps disk IO off the UI thread.
/// </summary>
public sealed class WriteQueue : IDisposable
{
    private readonly ConcurrentQueue<TaskItem> _queue = new();
    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private readonly TimeSpan _flushDelay = TimeSpan.FromMilliseconds(400);
    private readonly Timer _timer;

    private bool _disposed;

    public static WriteQueue Instance { get; } = new();

    private WriteQueue()
    {
        _timer = new Timer(_ => _ = FlushAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Enqueue(TaskItem task)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));
        if (_disposed) return;

        _queue.Enqueue(task);

        // Debounce flush to batch multiple quick task creations.
        _timer.Change(_flushDelay, Timeout.InfiniteTimeSpan);
    }

    public Task FlushNowAsync() => FlushAsync();

    private async Task FlushAsync()
    {
        if (_disposed) return;

        // Never allow concurrent flushes (timer could re-enter).
        if (!await _flushGate.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            var batch = new List<TaskItem>(capacity: 32);
            while (_queue.TryDequeue(out var item))
            {
                batch.Add(item);
            }

            if (batch.Count == 0) return;
            await InfoPool.Instance.AppendBatchAsync(batch).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();

        try
        {
            FlushAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort flush on shutdown.
        }
    }
}
