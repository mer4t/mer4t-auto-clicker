using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Display;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Services;

// Auto clicker ile aynı paylaşılan AutomationEngine'i kullanır; bu yüzden ikisi aynı anda çalışamaz
// (AutomationEngine zaten bunu State koruması ile garanti eder).
public sealed class MacroPlayer : IMacroPlayer
{
    private readonly AutomationEngine _engine;
    private readonly IDisplayService _displayService;
    private readonly IApplicationLogger _logger;
    private readonly SemaphoreSlim _startGate = new(1, 1);

    public MacroPlayer(AutomationEngine engine, IDisplayService displayService, IApplicationLogger logger)
    {
        _engine = engine;
        _displayService = displayService;
        _logger = logger;
    }

    public PlaybackState State => _engine.State;

    public event EventHandler<bool>? PlaybackPausedChanged;

    public async Task<PlaybackResult> PlayAsync(Macro macro, PlaybackOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(macro);

        // UI butonu ile F8 global kısayolu aynı akışı kullandığı için bu kapı, aradaki yarış
        // durumlarına ve auto clicker ile aynı anda tetiklenmeye karşı korur (bkz. AutoClickerService).
        if (!await _startGate.WaitAsync(0, cancellationToken))
        {
            return new PlaybackResult { Success = false, ExecutedActionCount = 0, ErrorMessage = "Otomasyon zaten çalışıyor." };
        }

        try
        {
            var warning = DescribeDisplayMismatch(macro.DisplaySnapshot, _displayService.GetSnapshot());
            if (warning is not null)
            {
                _logger.LogWarning(
                    "Makro '{0}' farklı bir ekran yapılandırmasında kaydedilmiş; koordinatlar ölçeklenmediği için hedefi kaçırabilir. {1}",
                    macro.Name, warning);
            }

            var result = await _engine.RunAsync(macro.Actions, options, PlaybackState.Playing, cancellationToken);
            return result with { DisplayMismatchWarning = warning };
        }
        finally
        {
            _startGate.Release();
        }
    }

    // Koordinatlar makro içinde ham piksel olarak saklanır ve mevcut ekran yapılandırmasına göre
    // ölçeklenmez; bu yüzden çözünürlük/monitör sayısı kayıt anındakinden farklıysa kullanıcı en
    // azından bilgilendirilir (özellikle dışa/içe aktarılan makrolarda gerçekleşebilecek bir durum).
    private static string? DescribeDisplayMismatch(DisplaySnapshot recorded, DisplaySnapshot current)
    {
        if (recorded.VirtualWidth == current.VirtualWidth &&
            recorded.VirtualHeight == current.VirtualHeight &&
            recorded.Monitors.Count == current.Monitors.Count)
        {
            return null;
        }

        return $"Kayıt: {recorded.VirtualWidth}x{recorded.VirtualHeight} ({recorded.Monitors.Count} monitör), " +
               $"şu an: {current.VirtualWidth}x{current.VirtualHeight} ({current.Monitors.Count} monitör).";
    }

    public async Task PauseAsync()
    {
        await _engine.PauseAsync();
        PlaybackPausedChanged?.Invoke(this, true);
    }

    public async Task ResumeAsync()
    {
        await _engine.ResumeAsync();
        PlaybackPausedChanged?.Invoke(this, false);
    }

    public Task StopAsync() => _engine.StopAsync();
}
