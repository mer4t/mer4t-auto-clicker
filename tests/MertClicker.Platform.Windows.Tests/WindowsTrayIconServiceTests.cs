using MertClicker.Platform.Windows.Tests.Fakes;

namespace MertClicker.Platform.Windows.Tests;

// Not: Bu testler Show() çağırmaz; bu yüzden makinede gerçek, görünür bir sistem tepsisi simgesi
// oluşturulmaz. Show()'a bağlı senaryolar canlı/manuel olarak doğrulandı (bkz. rapor).
public class WindowsTrayIconServiceTests
{
    [Fact]
    public void Dispose_Is_Safe_When_Show_Was_Never_Called()
    {
        var service = new WindowsTrayIconService(new FakeApplicationLogger());

        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times_Safely()
    {
        var service = new WindowsTrayIconService(new FakeApplicationLogger());

        service.Dispose();
        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void ShowBalloonTip_Before_Show_Is_A_Safe_No_Op()
    {
        var service = new WindowsTrayIconService(new FakeApplicationLogger());

        var exception = Record.Exception(() => service.ShowBalloonTip("Başlık", "Metin"));

        Assert.Null(exception);
    }

    [Fact]
    public void Show_After_Dispose_Throws_ObjectDisposedException()
    {
        var service = new WindowsTrayIconService(new FakeApplicationLogger());
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(service.Show);
    }
}
