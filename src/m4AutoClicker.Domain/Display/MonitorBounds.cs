namespace m4AutoClicker.Domain.Display;

public readonly record struct MonitorBounds(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
