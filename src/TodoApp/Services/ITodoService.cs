using TodoApp.Models;

namespace TodoApp.Services;

public interface ITodoService
{
    Task<string> AddItemAsync(string userId, string text, CancellationToken cancellationToken);

    Task<bool?> CompleteItemAsync(string userId, Guid itemId, CancellationToken cancellationToken);

    Task<bool> DeleteItemAsync(string userId, Guid itemId, CancellationToken cancellationToken);

    Task<TodoItemModel?> GetAsync(string userId, Guid itemId, CancellationToken cancellationToken);

    Task<TodoListViewModel> GetListAsync(string userId, CancellationToken cancellationToken);
}
