using System.Threading.Channels;
using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;

namespace m4AutoClicker.Application.Tests.Fakes;

public sealed class FakeInputCaptureProvider : IInputCaptureProvider
{
    private readonly Channel<RawMouseEvent> _channel = Channel.CreateUnbounded<RawMouseEvent>();

    public bool IsCapturing { get; private set; }

    public ChannelReader<RawMouseEvent> Events => _channel.Reader;

    public ChannelWriter<RawMouseEvent> Writer => _channel.Writer;

    public void Start() => IsCapturing = true;

    public void Stop() => IsCapturing = false;

    public void Dispose() => _channel.Writer.TryComplete();
}
