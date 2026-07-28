using System.Threading.Channels;
using m4AutoClicker.Application.Models;

namespace m4AutoClicker.Application.Abstractions;

public interface IKeyboardCaptureProvider : IDisposable
{
    bool IsCapturing { get; }

    ChannelReader<RawKeyboardEvent> Events { get; }

    void Start();

    void Stop();
}
