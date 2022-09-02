namespace TodoApp;

public static class ResultExtensions
{
    public static IResult RateLimited(this IResultExtensions extensions, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        return new RateLimitedResult(retryAfter);
    }
}
