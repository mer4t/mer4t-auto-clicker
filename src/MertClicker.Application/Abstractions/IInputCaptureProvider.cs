using System.Threading.Channels;
using MertClicker.Application.Models;

namespace MertClicker.Application.Abstractions;

public interface IInputCaptureProvider : IDisposable
{
    bool IsCapturing { get; }

    ChannelReader<RawMouseEvent> Events { get; }

    void Start();

    void Stop();
}
