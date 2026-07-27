namespace MertClicker.Application.Abstractions;

public interface IApplicationLogger
{
    void LogDebug(string message, params object?[] args);

    void LogInformation(string message, params object?[] args);

    void LogWarning(string message, params object?[] args);

    void LogError(Exception? exception, string message, params object?[] args);
}
