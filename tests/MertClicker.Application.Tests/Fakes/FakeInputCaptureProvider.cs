using System.Threading.Channels;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;

namespace MertClicker.Application.Tests.Fakes;

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
