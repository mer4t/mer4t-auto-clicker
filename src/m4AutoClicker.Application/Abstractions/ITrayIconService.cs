namespace m4AutoClicker.Application.Abstractions;

public interface ITrayIconService : IDisposable
{
    void Show();

    void ShowBalloonTip(string title, string text);

    // Simgeye çift tıklama veya bağlam menüsündeki "Göster" seçeneği.
    event EventHandler? OpenRequested;

    // Bağlam menüsündeki "Çıkış" seçeneği.
    event EventHandler? ExitRequested;

    // Bağlam menüsündeki "Acil Durdur" seçeneği.
    event EventHandler? EmergencyStopRequested;
}
