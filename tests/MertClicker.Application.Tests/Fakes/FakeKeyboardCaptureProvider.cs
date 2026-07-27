using System.Threading.Channels;
using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeKeyboardCaptureProvider : IKeyboardCaptureProvider
{
    private readonly Channel<RawKeyboardEvent> _channel = Channel.CreateUnbounded<RawKeyboardEvent>();

    public bool IsCapturing { get; private set; }

    public ChannelReader<RawKeyboardEvent> Events => _channel.Reader;

    public ChannelWriter<RawKeyboardEvent> Writer => _channel.Writer;

    public void Start() => IsCapturing = true;

    public void Stop() => IsCapturing = false;

    public void Dispose() => _channel.Writer.TryComplete();
}
