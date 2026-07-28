using m4AutoClicker.Application.Abstractions;

namespace m4AutoClicker.Infrastructure.Tests.Fakes;

public sealed class FakeApplicationLogger : IApplicationLogger
{
    public List<string> ErrorMessages { get; } = [];

    public void LogDebug(string message, params object?[] args)
    {
    }

    public void LogInformation(string message, params object?[] args)
    {
    }

    public void LogWarning(string message, params object?[] args)
    {
    }

    public void LogError(Exception? exception, string message, params object?[] args) => ErrorMessages.Add(message);
}
