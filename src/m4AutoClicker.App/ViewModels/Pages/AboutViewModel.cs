using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace m4AutoClicker.App.ViewModels.Pages;

public sealed record AboutFeature(string Title, string Description);

public sealed record AboutShortcut(string Key, string Action);

public sealed partial class AboutViewModel : ObservableObject
{
    public string ApplicationName => "m4 Auto Clicker";

    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    public string Description =>
        "Windows için hafif bir otomasyon uygulaması: sabit aralıklarla otomatik tıklama ve " +
        "gerçek zamanlamasıyla fare/klavye makrosu kaydedip oynatma. Her şey global kısayollarla, " +
        "uygulama odakta olmasa bile çalışır.";

    public string Developer => "mer4t";

    public string RepositoryUrl => "https://github.com/mer4t/mer4t-auto-clicker";

    public string LicenseName => "MIT Lisansı";

    public IReadOnlyList<AboutFeature> Features { get; } =
    [
        new("Auto Clicker", "Sabit sayıda veya durdurana kadar tıklama; sol/sağ/orta tuş, tekli/çiftli tıklama, sabit nokta ya da güncel imleç konumu hedefi."),
        new("Makro Kaydedici", "Fare hareketi, tıklama, tekerlek olayları ve klavye tuş basışlarını gerçek zamanlamasıyla kaydeder."),
        new("Duraklat / Devam Et", "Zaman telafili duraklatma: kaldığın yerden değil, doğru zamanlamayla devam eder."),
        new("Özelleştirilebilir Kısayollar", "Her eylem istediğin tuş ve Ctrl/Alt/Shift/Win kombinasyonuna yeniden atanabilir."),
        new("Makro Kütüphanesi", "Kaydedilen makroları listele, oynat, açıklama/etiket ekle, sil, dışa/içe aktar."),
        new("Sistem Tepsisi", "Arka planda çalışır; tepsi simgesinden Göster, Acil Durdur veya Çıkış.")
    ];

    public IReadOnlyList<AboutShortcut> Shortcuts { get; } =
    [
        new("F6", "Auto Clicker'ı başlat/durdur"),
        new("F7", "Makro kaydını başlat/durdur"),
        new("F8", "Seçili makroyu oynat/durdur"),
        new("F9", "Acil durdurma — tüm aktif otomasyonları anında durdurur")
    ];
}
