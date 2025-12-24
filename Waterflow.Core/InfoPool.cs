using System.Text;
using System.Text.Json;

namespace Waterflow.Core;

/// <summary>
/// Persistence layer for tasks. Uses JSONL (one JSON per line) to make appends cheap and batched.
/// </summary>
public sealed class InfoPool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly string _tasksFilePath;

    public static InfoPool Instance { get; } = new();

    private InfoPool()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Waterflow");

        Directory.CreateDirectory(baseDir);
        _tasksFilePath = Path.Combine(baseDir, "tasks.jsonl");
    }

    public string TasksFilePath => _tasksFilePath;

    public async Task<IReadOnlyList<TaskItem>> LoadAllAsync()
    {
        if (!File.Exists(_tasksFilePath))
            return Array.Empty<TaskItem>();

        await _ioGate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var stream = new FileStream(
                _tasksFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var reader = new StreamReader(stream, Encoding.UTF8);

            var byId = new Dictionary<Guid, TaskItem>();
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null) break;

                line = line.Trim();
                if (line.Length == 0) continue;

                try
                {
                    var task = JsonSerializer.Deserialize<TaskItem>(line, JsonOptions);
                    if (task is null) continue;
                    if (task.Id == Guid.Empty) continue;
                    if (string.IsNullOrWhiteSpace(task.Title)) continue;
                    byId[task.Id] = task;
                }
                catch
                {
                    // Ignore corrupted lines (best-effort load).
                }
            }

            return byId.Values
                .OrderByDescending(t => t.CreatedAt)
                .ToArray();
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task AppendBatchAsync(IReadOnlyList<TaskItem> tasks)
    {
        if (tasks is null) throw new ArgumentNullException(nameof(tasks));
        if (tasks.Count == 0) return;

        await _ioGate.WaitAsync().ConfigureAwait(false);
        try
        {
            using var stream = new FileStream(
                _tasksFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous);

            await using var writer = new StreamWriter(stream, Encoding.UTF8);

            foreach (var task in tasks)
            {
                if (task is null) continue;
                if (task.Id == Guid.Empty) continue;
                if (string.IsNullOrWhiteSpace(task.Title)) continue;

                var json = JsonSerializer.Serialize(task, JsonOptions);
                await writer.WriteLineAsync(json).ConfigureAwait(false);
            }

            await writer.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }
}
