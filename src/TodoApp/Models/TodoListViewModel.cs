namespace TodoApp.Models;

public class TodoListViewModel
{
    public ICollection<TodoItemModel> Items { get; set; } = new List<TodoItemModel>();
}
