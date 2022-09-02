using System.IO.Pipelines;
using System.Reflection;
using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using TodoApp.Extensions;

namespace TodoApp;

public static class SampleEndpoints
{
    public static void MapRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/stream-blob/{id}", (string id, IAmazonS3 client) =>
        {
            return Results.Stream(
                async (stream) => await client.GetObjectStreamAsync(
                    "my-blob-bucket", id, null));
        });
    }

    public static void MapUserRoutes(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/users")
                           .RequireAuthorization()
                           .WithGroupName("users");

        group.MapPost("/", () =>
        {
            var id = Guid.NewGuid().ToString();
            return Results.Created($"/api/users/{id}", new { id });
        });

        group.MapGet("/", () =>
        {
            return Results.Ok(new[]
            {
                new{ id = "Martin" },
                new{ id = "John" }
            });
        });

        group.MapGet("/{id}", (string id) =>
        {
            return Results.Ok(new { id });
        });

        group.MapDelete("/{id}", (string id) =>
        {
            return Results.NoContent();
        });
    }

    public static IServiceCollection AddSamples(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonS3, AmazonS3Client>();
        services.AddSingleton<MyService>();

        services.AddTransient<CreateUserHandler>();
        services.AddTransient<FindUsersHandler>();
        services.AddTransient<LocationSearchHandler>();

        services.AddMvc();

        return services;
    }

    public static IEndpointRouteBuilder MapSampleRoutes(this IEndpointRouteBuilder builder, ILogger logger)
    {
        // All of these sample endpoints are defined as part of a Route Group.
        // Applies the same route prefix and metadata, such as authN/authZ policies
        // to all of the endpoints that are defined using the route group.
        // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#route-groups
        var samples = builder.MapGroup("/samples")
                             .AllowAnonymous();

        {
            // Endpoints for dealing with multipart file uploads.
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-net-7-preview-1/#iformfile-and-iformfilecollection-support

            // This endpoint binds to a single file posted as part of an HTTP multipart upload
            samples.MapPost("/upload-file", async (IFormFile blob, IAmazonS3 client) =>
            {
                using var stream = blob.OpenReadStream();

                var request = new PutObjectRequest
                {
                    BucketName = "my-blob-bucket",
                    Key = blob.FileName,
                    InputStream = stream,
                    AutoCloseStream = false
                };

                await client.PutObjectAsync(request);
            });

            // This endpoint binds to two files, the second of which
            // is optional, posted as part of an HTTP multipart upload.
            samples.MapPost("/upload-files", async (IFormFile first, IFormFile? second, IAmazonS3 client) =>
            {
                using var blob1 = first.OpenReadStream();

                var request = new PutObjectRequest
                {
                    BucketName = "my-blob-bucket",
                    Key = first.FileName,
                    InputStream = blob1,
                    AutoCloseStream = false
                };

                await client.PutObjectAsync(request);

                if (second != null)
                {
                    using var blob2 = second.OpenReadStream();

                    request = new PutObjectRequest
                    {
                        BucketName = "my-blob-bucket",
                        Key = second.FileName,
                        InputStream = blob1,
                        AutoCloseStream = false
                    };

                    await client.PutObjectAsync(request);
                }
            });

            // This endpoint supports an arbitrary number of file uploads from a HTTP multipart form
            samples.MapPost("/upload-more-files", async (IFormFileCollection files, IAmazonS3 client) =>
            {
                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream();

                    var request = new PutObjectRequest
                    {
                        BucketName = "my-blob-bucket",
                        Key = file.FileName,
                        InputStream = stream,
                        AutoCloseStream = false
                    };

                    await client.PutObjectAsync(request);
                }
            });

            // This endpoint binds to a single file posted as part of an HTTP multipart upload
            // but also validates the request has a valid anti-forgery cookie/header value
            samples.MapPost("/upload-file", async (IFormFile blob, IAmazonS3 client) =>
            {
                using var stream = blob.OpenReadStream();

                var request = new PutObjectRequest
                {
                    BucketName = "my-blob-bucket",
                    Key = blob.FileName,
                    InputStream = stream,
                    AutoCloseStream = false
                };

                await client.PutObjectAsync(request);
            }).ValidateAntiforgery();

            // Samples for consuming the raw HTTP body.
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-net-7-preview-1/#bind-the-request-body-as-a-stream-or-pipereader

            // This endpoint binds to the raw HTTP body so that the body can be streamed
            // elsewhere without having to buffer it or require access to the HttpContext.
            // As the response is streamed, the Content-Length needs to be specified in
            // the PutObjectRequest object used to put the blob into the S3 bucket. Otherwise
            // the AWS S3 SDK tries to get the Stream's length, which is not supported.
            samples.MapPost("/upload-blob-from-stream", async (
                Stream blob,
                [FromHeader(Name = "Content-Length")] long contentLength,
                IAmazonS3 client) =>
            {
                var request = new PutObjectRequest
                {
                    BucketName = "my-blob-bucket",
                    Key = Guid.NewGuid().ToString() + ".bin",
                    InputStream = blob,
                    AutoCloseStream = false,
                };

                request.Headers.ContentLength = contentLength;

                await client.PutObjectAsync(request);
            });

            // This endpoint is the same as for a Stream, except using a PipeReader.
            // This type is useful to consume parts of the body piecemeal and/or
            // work with buffers associated with the raw request body directly.
            samples.MapPost("/upload-blob-from-pipe", async (PipeReader blob) =>
            {
                // Consume the pipe reader...
                _ = await blob.ReadAsync();
            });

            // Samples for header and query binding for collections.
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-2/#binding-arrays-and-stringvalues-from-headers-and-query-strings-in-minimal-apis

            // This endpoint binds multiple values from the name query string parameters.
            samples.MapGet("/search-people", (StringValues name) =>
            {
                var names = name.ToArray();
                logger.LogInformation($"Searched names: {string.Join(", ", names)}");
                return names;
            });

            // This can also now be done with arrays
            samples.MapGet("/search-people-array", (string[] name) =>
            {
                logger.LogInformation($"Searched names: {string.Join(", ", name)}");
                return name;
            });

            // This endpoint binds multiple values from the Use-Agent HTTP request header
            samples.MapGet("/random-number", ([FromHeader(Name = "User-Agent")] StringValues userAgent) =>
            {
                logger.LogInformation($"Client: {string.Join(", ", userAgent.ToArray())}");
                return Random.Shared.Next().ToString();
            });

            // Samples for endpoint filters:
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-3/#support-for-route-handler-filters-in-minimal-apis

            // This endpoint uses a lambda delegate to run code around the endpoint delegate
            samples.MapGet("/filter-lambda", () => logger.LogInformation("During handler"))
                   .AddEndpointFilter(async (context, next) =>
            {
                // The context parameter (which is of type EndpointFilterInvocationContext)
                // contains information about the endpoint that is being executed, such
                // as the route parameters, as well as access to the current HttpContext.
                logger.LogInformation("Before handler");

                // The next parameter is an EndpointFilterDelegate delegate, which represents
                // any additional endpoint filters to execute, as well as the endpoint handler itself.
                var result = await next(context);

                logger.LogInformation("After handler");

                // Returns the result of executing any inner filter(s) and/or the handler.
                return result;
            });

            // This endpoint achieves the same goal as the example above, except it implements
            // the new IEndpointFilter interface to be strongly typed. This allows you to
            // specify dependencies to inject into the constructor from the service collection,
            // as well as allowing you to unit test the filter directly.
            samples.MapGet("/filter-class", () => logger.LogInformation("During handler"))
                   .AddEndpointFilter<MyEndpointFilter>();

            // Samples for argument list parameter binding.
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-5/#minimal-api-parameter-binding-for-argument-lists

            // [AsParameters] is used to group all of the query string parameters into
            // a single object instead of having to declare a parameter for each one.
            samples.MapGet("/search-users", async (
                [AsParameters] FindUsersRequest request,
                FindUsersHandler handler,
                CancellationToken cancellationToken) =>
            {
                await handler.ProcessAsync(request, cancellationToken);
            });

            // Samples for typed results
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#typed-results-for-minimal-apis
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-4/#return-multiple-result-types-from-minimal-apis

            // The Results<T1, T2, ...> class is used to indicate which results that endpoint can return,
            // which in turn uses IEndpointMetadataProvider to indicate the status codes for OpenAPI.
            samples.MapPost("/create-user", async Task<Results<Created, BadRequest>> (CreateUser? user, CreateUserHandler handler) =>
            {
                if (user is null)
                {
                    return TypedResults.BadRequest();
                }

                string id = await handler.ProcessAsync(user);

                return TypedResults.Created($"/api/users/{id}");
            });

            // This endpoint takes a parameter that implements IBindableFromHttpContext<TSelf> which
            // can be used to dynamically bind a single value of the type using the HttpContext.
            samples.MapGet("/search-location", async (
                Geolocation? location,
                LocationSearchHandler handler) =>
            {
                if (location is null)
                {
                    return Results.BadRequest(new { message = "No geolocation specified." });
                }

                return Results.Ok(await handler.ProcessAsync(location));
            });

            // Sample for using Results.Stream()
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-3/#new-results-stream-overloads

            // This endpoint allows the response from S3 to be streamed to the client
            samples.MapGet("/stream-blob/{id}", (string blob, IAmazonS3 client) =>
            {
                return Results.Stream(async (stream) => await client.GetObjectStreamAsync("my-blob-bucket", blob, null));
            });

            // Samples for simplified authentication/authorization
            // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-5/#simplified-authentication-configuration

            // This endpoint requires the user to be authenticated
            samples.MapGet("/secret", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name}. This is a secret!")
                   .RequireAuthorization();

            // This endpoint requires the user to be authenticated and have the "admin" scope claim
            samples.MapGet("/secret/admin", (ClaimsPrincipal user) => $"Hello {user.Identity?.Name}. You are an admin!")
                   .RequireAuthorization(p => p.RequireClaim("scope", "admin"));
        }

        builder.MapDefaultControllerRoute();

        return builder;
    }

    private record class Geolocation(double Latitude, double Longitude) : IBindableFromHttpContext<Geolocation>
    {
        public static ValueTask<Geolocation?> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            var location = context.Request.Query["location"];

            if (StringValues.IsNullOrEmpty(location))
            {
                return default;
            }

            string[] components = location.ToString().Split(',');

            if (components.Length != 2 ||
                !double.TryParse(components[0], out var latitude) ||
                !double.TryParse(components[1], out var longitude))
            {
                return default;
            }

            return ValueTask.FromResult<Geolocation?>(new(latitude, longitude));
        }
    }

    private record struct FindUsersRequest(
        string? Id,
        string? Email,
        string? Name,
        DateTimeOffset? DateOfBirth);

    private sealed class FindUsersHandler
    {
        public Task ProcessAsync(FindUsersRequest request, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private record CreateUser(
        string? Email,
        string? Name,
        DateTimeOffset? DateOfBirth);

    private sealed class CreateUserHandler
    {
        public Task<string> ProcessAsync(CreateUser request)
            => Task.FromResult(Guid.NewGuid().ToString());
    }

    private sealed class LocationSearchHandler
    {
        public Task<string[]> ProcessAsync(Geolocation location)
            => Task.FromResult(new string[] { "London", "Amsterdam" });
    }

    private sealed class MyEndpointFilter : IEndpointFilter
    {
        public MyEndpointFilter(ILogger<MyEndpointFilter> logger)
        {
            Logger = logger;
        }

        private ILogger Logger { get; }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            Logger.LogInformation("Before handler");

            var result = await next(context);

            Logger.LogInformation("After handler");

            return result;
        }
    }
}
