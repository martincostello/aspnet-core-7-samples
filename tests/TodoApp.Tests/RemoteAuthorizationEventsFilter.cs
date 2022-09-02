using AspNet.Security.OAuth.GitHub;
using Microsoft.Extensions.Options;

namespace TodoApp;

public sealed class RemoteAuthorizationEventsFilter : IPostConfigureOptions<GitHubAuthenticationOptions>
{
    public RemoteAuthorizationEventsFilter(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    private IHttpClientFactory HttpClientFactory { get; }

    public void PostConfigure(string? name, GitHubAuthenticationOptions options)
    {
        // Use HttpClientFactory for HTTP requests so that the tests
        // can intercept the request and return canned responses.
        options.Backchannel = HttpClientFactory.CreateClient(name ?? string.Empty);

        // Configure the GitHub provider to redirect back to the
        // test application, rather than GitHub's own login pages.
        options.EventsType = typeof(LoopbackOAuthEvents);
    }
}
