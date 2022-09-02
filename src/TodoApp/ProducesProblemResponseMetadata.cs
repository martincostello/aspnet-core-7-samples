using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace TodoApp;

internal sealed class ProducesProblemResponseMetadata : IProducesResponseTypeMetadata
{
    internal ProducesProblemResponseMetadata(int statusCode)
    {
        StatusCode = statusCode;
    }

    public Type? Type => typeof(ProblemDetails);

    public int StatusCode { get; }

    public IEnumerable<string> ContentTypes { get; } = new[] { "application/problem+json" };
}
