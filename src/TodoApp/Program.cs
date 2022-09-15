using TodoApp;

// Create the default web application builder
var builder = WebApplication.CreateBuilder(args);

// Configure the Todo repository and associated services
builder.Services.AddTodoApi();

builder.Services.AddAuthentication().AddJwtBearer();

// Add user authentication with GitHub as an external OAuth provider
builder.Services.AddGitHubAuthentication();

// Add services for endpoints that showcase new features independently of the Todo app
builder.Services.AddSamples();

// Add Razor Pages to render the UI
builder.Services.AddRazorPages();

// Configure OpenAPI documentation for the Todo API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Todo API", Version = "v1" });
});

// Create the app
var app = builder.Build();

// Configure error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseStatusCodePagesWithReExecute("/error", "?id={0}");

// Require use of HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Add static files for JavaScript, CSS and OpenAPI
app.UseStaticFiles();

// Add authN for GitHub
app.UseAuthentication();
app.UseAuthorization();

// Add Swagger endpoint for OpenAPI
app.UseSwagger();

// Add the HTTP endpoints and configure rate-limiting for the API
app.MapAuthenticationRoutes();

app.UseRateLimiter();
app.MapTodoApiRoutes();

// Add Razor Pages for the UI
app.MapRazorPages();

// Add HTTP endpoints that showcase new features independently of the Todo app
app.MapSampleRoutes(app.Logger);

// Run the application
app.Run();

public partial class Program
{
    // Expose the Program class for use with WebApplicationFactory<T>
}
