using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Domain.Automation;

namespace m4AutoClicker.Application.Services;

// F9 acil durdurma için tek giriş noktası.
public sealed class EmergencyStopCoordinator : IEmergencyStopService
{
    private readonly AutoClickerService _autoClickerService;
    private readonly MacroRecorder _macroRecorder;
    private readonly IMacroPlayer _macroPlayer;
    private readonly IApplicationLogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public EmergencyStopCoordinator(
        AutoClickerService autoClickerService, MacroRecorder macroRecorder, IMacroPlayer macroPlayer, IApplicationLogger logger)
    {
        _autoClickerService = autoClickerService;
        _macroRecorder = macroRecorder;
        _macroPlayer = macroPlayer;
        _logger = logger;
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Acil durdurma başlatıldı.");
            await _autoClickerService.StopAsync();
            await _macroPlayer.StopAsync();

            if (_macroRecorder.State == RecordingState.Recording)
            {
                var macro = await _macroRecorder.StopAsync();
                _logger.LogInformation("Acil durdurma: devam eden makro kaydı durduruldu ({0} eylem).", macro.Actions.Count);
            }

            _logger.LogInformation("Acil durdurma tamamlandı: aktif otomasyonlar durduruldu.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acil durdurma sırasında hata oluştu.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
