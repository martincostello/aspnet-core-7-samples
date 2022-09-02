using JustEat.HttpClientInterception;
using Microsoft.Extensions.Http;

namespace TodoApp;

// See https://github.com/justeat/httpclient-interception#registering-request-interception-when-using-ihttpclientfactory

public sealed class HttpRequestInterceptionFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly HttpClientInterceptorOptions _options;

    internal HttpRequestInterceptionFilter(HttpClientInterceptorOptions options)
    {
        _options = options;
    }

    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            next(builder);
            builder.AdditionalHandlers.Add(_options.CreateHttpMessageHandler());
        };
    }
}
