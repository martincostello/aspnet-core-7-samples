namespace TodoApp;

[CollectionDefinition(Name)]
public sealed class TodoAppCollection : ICollectionFixture<TodoAppFixture>
{
    public const string Name = "TodoApp server collection";
}
