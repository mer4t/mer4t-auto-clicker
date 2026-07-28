using System.Threading.Channels;
using m4AutoClicker.Application.Models;

namespace m4AutoClicker.Application.Abstractions;

public interface IInputCaptureProvider : IDisposable
{
    bool IsCapturing { get; }

    ChannelReader<RawMouseEvent> Events { get; }

    void Start();

    void Stop();
}
