namespace m4AutoClicker.Application.Abstractions;

public interface IHighResolutionClock
{
    long GetTimestamp();

    long Frequency { get; }
}
