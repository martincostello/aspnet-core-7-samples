using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace TodoApp;

public sealed class RateLimitedResult : IResult, IEndpointMetadataProvider
{
    private readonly TimeSpan _retryAfter;

    public RateLimitedResult(TimeSpan retryAfter)
    {
        _retryAfter = retryAfter;
    }

    public static void PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(
            new ProducesProblemResponseMetadata(StatusCodes.Status429TooManyRequests));
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var value = new ProblemDetails
        {
            Title = "Too Many Requests",
            Detail = "Too many requests.",
            Status = StatusCodes.Status429TooManyRequests,
        };

        int retryAfterSeconds = Math.Max(1, (int)_retryAfter.TotalSeconds);

        httpContext.Response.StatusCode = value.Status.Value;
        httpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

        return httpContext.Response.WriteAsJsonAsync(
            value,
            options: null,
            contentType: "application/problem+json");
    }
}
