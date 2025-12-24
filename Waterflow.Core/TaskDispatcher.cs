namespace Waterflow.Core;

/// <summary>
/// Singleton controller that coordinates task creation: update in-memory state synchronously,
/// then enqueue persistence asynchronously.
/// </summary>
public sealed class TaskDispatcher
{
    public static TaskDispatcher Instance { get; } = new();

    private TaskDispatcher() { }

    public TaskItem CreateTask(string title)
    {
        title = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title cannot be empty.", nameof(title));

        // Keep it lightweight; any heavier validation can be added later.
        if (title.Length > 200)
            title = title[..200];

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Title = title,
            CreatedAt = DateTimeOffset.Now
        };

        // 1) Sync update in-memory state (UI reacts immediately)
        ObservableTaskList.Instance.Add(task);

        // 2) Async persistence via write buffer (IO isolation boundary)
        WriteQueue.Instance.Enqueue(task);

        return task;
    }
}
