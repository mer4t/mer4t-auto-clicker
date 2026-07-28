using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Automation;
using m4AutoClicker.Domain.Macros;

namespace m4AutoClicker.Application.Abstractions;

public interface IMacroPlayer
{
    PlaybackState State { get; }

    // MacroPlayer, hem F8/Makrolarım hem de Makro Kaydedici ekranındaki "Son Kaydı Oynat" akışları
    // arasında tek bir paylaşılan singleton'dır; ancak hangi ekranın Duraklat/Devam Et'e bastığından
    // bağımsız olarak DİĞER ekranların da duraklatma durumunu doğru göstermesi gerekir. Bu olay,
    // PauseAsync/ResumeAsync tarafından tetiklenir; abone olan tüm ViewModel'ler kendi yerel
    // IsPlaybackPaused durumlarını buradan senkronize eder.
    event EventHandler<bool>? PlaybackPausedChanged;

    Task<PlaybackResult> PlayAsync(Macro macro, PlaybackOptions options, CancellationToken cancellationToken);

    Task PauseAsync();

    Task ResumeAsync();

    Task StopAsync();
}
