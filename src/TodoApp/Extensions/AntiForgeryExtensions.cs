using Microsoft.AspNetCore.Antiforgery;

namespace TodoApp.Extensions;

public static class AntiForgeryExtensions
{
    public static TBuilder ValidateAntiforgery<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();

            try
            {
                await antiforgery.ValidateRequestAsync(httpContext);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Antiforgery token validation failed.");
            }

            return await next(context);
        });
    }
}
