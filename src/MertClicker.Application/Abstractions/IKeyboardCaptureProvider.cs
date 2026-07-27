using System.Threading.Channels;
using MertClicker.Application.Models;

namespace MertClicker.Application.Abstractions;

public interface IKeyboardCaptureProvider : IDisposable
{
    bool IsCapturing { get; }

    ChannelReader<RawKeyboardEvent> Events { get; }

    void Start();

    void Stop();
}
