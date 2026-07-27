namespace MertClicker.Application.Abstractions;

// F9 acil durdurma ile tetiklenir. İleride makro kaydı/oynatma servisleri de buraya eklenecek şekilde genişletilebilir.
public interface IEmergencyStopService
{
    Task StopAllAsync(CancellationToken cancellationToken = default);
}
