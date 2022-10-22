using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace TodoApp;

[ApiController]
public class SampleController : Controller
{
    // MVC action methods now implicitly resolve parameters from services
    // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-2/#infer-api-controller-action-parameters-that-come-from-services

    [HttpGet("/samples/implicit-services")]
    public async Task<IActionResult> ImplicitServices(MyService service)
    {
        await service.DoSomethingAsync();
        return Ok();
    }

    // MVC action methods now support TryParse() for parameter values.
    // https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-3/#bind-using-tryparse-in-mvc-and-api-controllers

    [HttpGet("/samples/try-parse-parameter")]
    public IActionResult TryParseableParameters([FromQuery] Name name)
    {
        return Ok($"Hello {name.Value}");
    }
}

public record struct Name(string? Value) : IParsable<Name> // These methods work even if IParseable<T> is not used
{
    public static Name Parse(string s, IFormatProvider? provider)
    {
        return new(s);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Name result)
    {
        result = new(s);
        return true;
    }
}
