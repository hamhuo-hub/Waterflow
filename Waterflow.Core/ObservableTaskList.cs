namespace Waterflow.Core;

/// <summary>
/// In-memory reactive state/cache for tasks.
/// </summary>
public sealed class ObservableTaskList
{
    private readonly object _gate = new();
    private readonly List<TaskItem> _tasks = new();

    public static ObservableTaskList Instance { get; } = new();

    private ObservableTaskList() { }

    public event Action<TaskItem>? TaskAdded;
    public event Action? TasksChanged;

    public void Add(TaskItem task)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));

        bool added;
        lock (_gate)
        {
            if (_tasks.Any(t => t.Id == task.Id))
            {
                added = false;
            }
            else
            {
                // Newest first (matches typical "inbox" UX)
                _tasks.Insert(0, task);
                added = true;
            }
        }

        if (!added) return;

        TaskAdded?.Invoke(task);
        TasksChanged?.Invoke();
    }

    /// <summary>
    /// Replace the current list without emitting change events.
    /// Intended for initial hydration from persistence.
    /// </summary>
    public void AddRangeSilent(IEnumerable<TaskItem> tasks)
    {
        if (tasks is null) throw new ArgumentNullException(nameof(tasks));

        lock (_gate)
        {
            _tasks.Clear();

            var seen = new HashSet<Guid>();
            foreach (var task in tasks.OrderByDescending(t => t.CreatedAt))
            {
                if (task is null) continue;
                if (!seen.Add(task.Id)) continue;
                _tasks.Add(task);
            }
        }
    }

    public IReadOnlyList<TaskItem> GetAll()
    {
        lock (_gate)
        {
            return _tasks.ToArray();
        }
    }
}
