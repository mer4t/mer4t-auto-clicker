namespace MertClicker.Platform.Windows;

// SendInput ile ürettiğimiz olayları hook'ta (Aşama 5) kullanıcının gerçek girdisinden ayırt etmek için kullanılır.
internal static class ApplicationInputMarker
{
    public const nuint Signature = 0x4D455254;
}
