namespace Waterflow.Core;

public sealed class TaskItem
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}


