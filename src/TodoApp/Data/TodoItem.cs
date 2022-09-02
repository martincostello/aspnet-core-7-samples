namespace TodoApp.Data;

public class TodoItem
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = default!;

    public string Text { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
