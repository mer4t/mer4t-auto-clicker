using MertClicker.Application.Abstractions;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeApplicationLogger : IApplicationLogger
{
    public List<string> InformationMessages { get; } = [];

    public List<string> WarningMessages { get; } = [];

    public List<string> ErrorMessages { get; } = [];

    public void LogDebug(string message, params object?[] args)
    {
    }

    public void LogInformation(string message, params object?[] args) => InformationMessages.Add(message);

    public void LogWarning(string message, params object?[] args) => WarningMessages.Add(message);

    public void LogError(Exception? exception, string message, params object?[] args) => ErrorMessages.Add(message);
}
