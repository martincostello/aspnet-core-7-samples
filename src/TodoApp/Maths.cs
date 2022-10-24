using Microsoft.AspNetCore.Http.HttpResults;

namespace TodoApp;

public static class Maths
{
    public static JsonHttpResult<int> Add(int x, int y)
        => TypedResults.Json(x + y);

    public static JsonHttpResult<int> Multiply(int x, int y)
        => TypedResults.Json(x * y);
}
