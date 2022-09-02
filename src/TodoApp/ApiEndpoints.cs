using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using TodoApp.Data;
using TodoApp.Models;
using TodoApp.Services;

namespace TodoApp;

/// <summary>
/// A class containing the HTTP endpoints for the Todo API.
/// </summary>
public static class ApiEndpoints
{
    /// <summary>
    /// Adds the services for the Todo API to the application.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/>.</param>
    /// <returns>
    /// A <see cref="IServiceCollection"/> that can be used to further configure the application.
    /// </returns>
    public static IServiceCollection AddTodoApi(this IServiceCollection services)
    {
        services.AddSingleton<IClock>(_ => SystemClock.Instance);
        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<ITodoService, TodoService>();

        services.AddDbContext<TodoContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var dataDirectory = configuration["DataDirectory"];

            if (string.IsNullOrEmpty(dataDirectory) || !Path.IsPathRooted(dataDirectory))
            {
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
                dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
            }

            // Ensure the configured data directory exists
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            var databaseFile = Path.Combine(dataDirectory, "TodoApp.db");

            options.UseSqlite("Data Source=" + databaseFile);
        });

        return services;
    }

    /// <summary>
    /// Maps the endpoints for the Todo API.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <returns>
    /// A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.
    /// </returns>
    public static IEndpointRouteBuilder MapTodoApiRoutes(this IEndpointRouteBuilder builder)
    {
        // Declare all the API endpoints to manage Todo items as being part of the
        // same Route Group. This allows us to mark all endpoints in the group as
        // requiring authorization with one call rather than each one needing to be
        // annotated individually. This makes it less likely to introduce a bug by
        // forgetting to add it if adding a new endpoint in the future.
        //
        // By convention, all of the endpoints defined as part of the group inherit the
        // same route prefix as declared by the route group, again reducing repeition.
        //
        // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#route-groups
        var group = builder.MapGroup("/api/items")
                           .RequireAuthorization();
        {
            // Each endpoint uses the new Results<,...> discriminated union types to declare the
            // possible types returned by the endpoint. These can then augment OpenAPI documentation
            // to return the correct HTTP status codes and content types for the endpoint. These
            // types go hand-in-hand with the new TypedResults class which exposes a concrete type
            // for the result of the method, rather than all of the methods returning IResult.
            // The concrete types also allow Minimal API endpoints to be unit tested.
            //
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#return-multiple-result-types-from-minimal-apis
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#typed-results-for-minimal-apis
            //
            // The [AsParameters] attribute is used to specify that an object contains properties
            // that we wish to bind to the request, rather than specifying many parameters. This
            // allows each endpoint to specify a single parameter to receive arguments that are
            // common between then, reducing repetition and making the endpoints more consistent.
            //
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-5/#minimal-api-parameter-binding-for-argument-lists
            //
            // Rate-limiting is documented via including the RateLimitedResult type, even though it
            // is not used in the endpoints' code themselves, but is instead handled by the rate-limiting
            // middleware that is added to the application in Program.cs.
            //
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#rate-limiting-middleware
            group.MapGet("/", async Task<Results<Ok<TodoListViewModel>, RateLimitedResult>> (
                [AsParameters] TodoRequestContext context) =>
                    TypedResults.Ok(await context.Service.GetListAsync(context.User, context.RequestAborted)))
                .WithSummary("Get all Todo items")
                .WithDescription("Gets all of the current user's todo items.");

            group.MapGet("/{id}", async Task<Results<Ok<TodoItemModel>, ProblemHttpResult, RateLimitedResult>> (
                Guid id,
                [AsParameters] TodoRequestContext context) =>
                {
                    var model = await context.Service.GetAsync(context.User, id, context.RequestAborted);

                    // Here we use a switch expression with TypedResults and Results<,,> to return different
                    // concrete types for different use cases for the endpoint (HTTP 200 vs. 404).
                    return model switch
                    {
                        null => TypedResults.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound),
                        _ => TypedResults.Ok(model),
                    };
                })
                // The return type from TypedResults.Problem() does not implement the
                // new IEndpointMetadataProvider interface, so we must explicit document
                // the endpoint as producing a 404 problem details response.
                .ProducesProblem(StatusCodes.Status404NotFound)
                .WithSummary("Get a specific Todo item")
                .WithDescription("Gets the todo item with the specified ID.");

            group.MapPost("/", async Task<Results<Created<CreatedTodoItemModel>, ProblemHttpResult, RateLimitedResult>> (
                CreateTodoItemModel model,
                [AsParameters] TodoRequestContext context) =>
                {
                    if (string.IsNullOrWhiteSpace(model.Text))
                    {
                        return TypedResults.Problem("No item text specified.", statusCode: StatusCodes.Status400BadRequest);
                    }

                    var id = await context.Service.AddItemAsync(context.User, model.Text, context.RequestAborted);

                    return TypedResults.Created($"/api/items/{id}", new CreatedTodoItemModel() { Id = id });
                })
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .WithSummary("Create a new Todo item")
                .WithDescription("Creates a new todo item for the current user and returns its ID.");

            group.MapPost("/{id}/complete", async Task<Results<NoContent, ProblemHttpResult, RateLimitedResult>> (
                Guid id,
                [AsParameters] TodoRequestContext context) =>
                {
                    var wasCompleted = await context.Service.CompleteItemAsync(context.User, id, context.RequestAborted);

                    return wasCompleted switch
                    {
                        true => TypedResults.NoContent(),
                        false => TypedResults.Problem("Item already completed.", statusCode: StatusCodes.Status400BadRequest),
                        _ => TypedResults.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound),
                    };
                })
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .WithSummary("Mark a Todo item as completed")
                .WithDescription("Marks the todo item with the specified ID as complete.");

            group.MapDelete("/{id}", async Task<Results<NoContent, ProblemHttpResult, RateLimitedResult>> (
                Guid id,
                [AsParameters] TodoRequestContext context) =>
                {
                    var wasDeleted = await context.Service.DeleteItemAsync(context.User, id, context.RequestAborted);
                    return wasDeleted switch
                    {
                        true => TypedResults.NoContent(),
                        false => TypedResults.Problem("Item not found.", statusCode: StatusCodes.Status404NotFound),
                    };
                })
                .ProducesProblem(StatusCodes.Status404NotFound)
                .WithSummary("Delete a Todo item")
                .WithDescription("Deletes the todo item with the specified ID.");
        };

        // Redirect to Open API/Swagger documentation
        builder.MapGet("/api", () => Results.Redirect("/swagger-ui/index.html"))
               .ExcludeFromDescription()
               .RequireAuthorization();

        return builder;
    }

    // The properties of the TodoRequestContext record are bound in the same way
    // that they would be bound if they were all individual parameters on the
    // Minimal API endpoint methods themselves. In this case the User is bound
    // via the static BindAsync() method, ITodoService is resolved from the
    // service collection as it is registered with DI, and the CancellationToken
    // is a special well-known type that binds to HttpContext.RequestAborted.

    private record struct TodoRequestContext(
        TodoUser User,
        ITodoService Service,
        CancellationToken RequestAborted);

    private sealed class TodoUser : IBindableFromHttpContext<TodoUser>
    {
        private TodoUser(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public static implicit operator string(TodoUser value) => value.Id;

        public static ValueTask<TodoUser?> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            return ValueTask.FromResult<TodoUser?>(new(context.User.GetUserId()));
        }
    }
}
