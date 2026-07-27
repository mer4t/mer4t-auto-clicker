namespace MertClicker.Application.Models;

public sealed record PlaybackResult
{
    public required bool Success { get; init; }

    public required int ExecutedActionCount { get; init; }

    public string? ErrorMessage { get; init; }

    // Makronun kaydedildiği ekran yapılandırması (çözünürlük/monitör sayısı) mevcut yapılandırmadan
    // farklıysa doldurulur; koordinatlar farklı ekranlarda ölçeklenmediği için kullanıcıyı bilgilendirir.
    public string? DisplayMismatchWarning { get; init; }
}
