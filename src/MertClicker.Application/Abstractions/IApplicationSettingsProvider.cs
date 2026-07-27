using MertClicker.Application.Models;

namespace MertClicker.Application.Abstractions;

// Uygulama içinde her yerden senkron olarak okunabilecek, bellekte tutulan güncel ayarlar.
// Diskteki gerçek kalıcılık ISettingsRepository'nin işidir; bu sağlayıcı yalnızca "şu an geçerli olan
// değer" için ucuz, G/Ç gerektirmeyen bir önbellektir (ör. MacroOptimizer her makro kaydını
// optimize ederken diski okumadan güncel ayarları kullanabilsin diye).
public interface IApplicationSettingsProvider
{
    ApplicationSettings Current { get; }

    void Update(ApplicationSettings settings);
}
