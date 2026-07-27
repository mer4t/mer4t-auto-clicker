namespace MertClicker.Application.Abstractions;

public interface IHighResolutionClock
{
    long GetTimestamp();

    long Frequency { get; }
}
