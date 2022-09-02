namespace TodoApp.Models;

public class TodoItemModel
{
    public string Id { get; set; } = default!;

    public string Text { get; set; } = default!;

    public bool IsCompleted { get; set; }

    public string LastUpdated { get; set; } = default!;
}
