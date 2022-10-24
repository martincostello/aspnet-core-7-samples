using Microsoft.AspNetCore.Http.HttpResults;

namespace TodoApp;

// Samples for improved unit testability
// https://devblogs.microsoft.com/dotnet/asp-net-core-updates-in-dotnet-7-preview-3/#improved-unit-testability-for-minimal-route-handlers

public static class MathsTests
{
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(37, 42, 79)]
    [InlineData(1138, 0, 1138)]
    public static void Can_Add_Numbers(int x, int y, int expected)
    {
        // Act
        JsonHttpResult<int> result = Maths.Add(x, y);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 2, 2)]
    [InlineData(37, 42, 1554)]
    [InlineData(1138, 0, 0)]
    public static void Can_Multiply_Numbers(int x, int y, int expected)
    {
        // Act
        JsonHttpResult<int> result = Maths.Multiply(x, y);

        // Assert
        result.ShouldNotBeNull();
        result.Value.ShouldBe(expected);
    }
}
