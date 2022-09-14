using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TodoApp;

namespace Microsoft.AspNetCore.Builder;

public static class IApplicationBuilderExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        app.UseRateLimiter(new()
        {
            RejectionStatusCode = StatusCodes.Status429TooManyRequests,
            GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(CreateRateLimiter),
            OnRejected = OnRateLimited,
        });

        return app;
    }

    public static RateLimitPartition<string> CreateRateLimiter(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Any requests that are not authenticated are not rate-limited.
            return RateLimitPartition.GetNoLimiter("anonymous");
        }

        // Determine which set of rate limits to use. HTTP GET requests use a "read" set
        // of limits which are higher than those for "writes" (such as POST and DELETE).
        var operation = HttpMethods.IsGet(context.Request.Method) ? "Read" : "Write";
        var userId = context.User.GetUserId();

        var config = context.RequestServices.GetRequiredService<IConfiguration>();

        // Requests are partitioned by the type of operation and the authenticated
        // user's ID. This means that rate limits are specific to individual users
        // so "noisy neighbours" should not affect the usage of well-behaved clients.
        return RateLimitPartition.GetTokenBucketLimiter(
            $"{operation}-RateLimit-{userId}",
            _ =>
            {
                var section = config.GetSection($"RateLimits:{operation}");
                var autoReplenishment = section.GetValue<bool>("AutoReplenishment");
                var queueLimit = section.GetValue<int>("QueueLimit");
                var queueProcessingOrder = section.GetValue<QueueProcessingOrder>("QueueProcessingOrder");
                var replenishmentPeriod = section.GetValue<TimeSpan>("ReplenishmentPeriod");
                var tokenLimit = section.GetValue<int>("TokenLimit");
                var tokensPerPeriod = section.GetValue<int>("TokensPerPeriod");

                return new()
                {
                    AutoReplenishment = autoReplenishment,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = queueProcessingOrder,
                    TokenLimit = tokenLimit,
                    TokensPerPeriod = tokensPerPeriod,
                    ReplenishmentPeriod = replenishmentPeriod,
                };
            });
    }

    private static readonly MetadataName<TimeSpan> RetryAfterMetadata = new("RETRY_AFTER");

    public static async ValueTask OnRateLimited(OnRejectedContext context, CancellationToken cancellationToken)
    {
        // By default use a Retry-After value of one second.
        var retryAfter = TimeSpan.FromSeconds(1);

        if (context.Lease.TryGetMetadata(RetryAfterMetadata, out var ts))
        {
            // However if the lease associated with the request that
            // was rate limited contains the approprate metadata, use
            // that value to indicate the Retry-After value instead.
            retryAfter = ts;
        }

        var result = Results.Extensions.RateLimited(retryAfter);
        await result.ExecuteAsync(context.HttpContext);
    }
}
