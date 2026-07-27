using MertClicker.Application.Abstractions;

namespace MertClicker.Platform.Windows.Tests.Fakes;

public sealed class FakeApplicationLogger : IApplicationLogger
{
    public void LogDebug(string message, params object?[] args)
    {
    }

    public void LogInformation(string message, params object?[] args)
    {
    }

    public void LogWarning(string message, params object?[] args)
    {
    }

    public void LogError(Exception? exception, string message, params object?[] args)
    {
    }
}
