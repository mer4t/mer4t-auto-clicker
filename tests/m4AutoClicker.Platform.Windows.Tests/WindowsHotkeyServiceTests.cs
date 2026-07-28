using m4AutoClicker.Application.Models;
using m4AutoClicker.Domain.Hotkeys;
using m4AutoClicker.Platform.Windows.Tests.Fakes;

namespace m4AutoClicker.Platform.Windows.Tests;

// Not: Bu testler Start() çağırmaz; bu yüzden gerçek RegisterHotKey/HwndSource oluşturma tetiklenmez
// ve makinede global bir kısayol yan etkisi bırakmaz. Start()'a bağlı (RegisterHotKey içeren) senaryolar
// Aşama 4 kapsamında manuel/canlı olarak doğrulandı (bkz. rapor).
public class WindowsHotkeyServiceTests
{
    [Fact]
    public void Register_Before_Start_Returns_WindowHandleUnavailable()
    {
        using var service = new WindowsHotkeyService(new FakeApplicationLogger());

        var result = service.Register(new HotkeyDefinition { Id = HotkeyIds.AutoClickerToggle, Key = VirtualKey.F6 });

        Assert.False(result.Success);
        Assert.Equal(HotkeyRegistrationErrorType.WindowHandleUnavailable, result.ErrorType);
        Assert.Equal(HotkeyIds.AutoClickerToggle, result.HotkeyId);
    }

    [Fact]
    public void Unregister_Unknown_Id_Returns_False_Without_Throwing()
    {
        using var service = new WindowsHotkeyService(new FakeApplicationLogger());

        var removed = service.Unregister("Unknown.Id");

        Assert.False(removed);
    }

    [Fact]
    public void UnregisterAll_Is_Safe_When_Nothing_Registered()
    {
        using var service = new WindowsHotkeyService(new FakeApplicationLogger());

        var exception = Record.Exception(service.UnregisterAll);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_Can_Be_Called_Multiple_Times_Safely()
    {
        var service = new WindowsHotkeyService(new FakeApplicationLogger());

        service.Dispose();
        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Register_After_Dispose_Returns_Unknown_Error_Without_Throwing()
    {
        var service = new WindowsHotkeyService(new FakeApplicationLogger());
        service.Dispose();

        var result = service.Register(new HotkeyDefinition { Id = HotkeyIds.EmergencyStop, Key = VirtualKey.F9 });

        Assert.False(result.Success);
        Assert.Equal(HotkeyRegistrationErrorType.Unknown, result.ErrorType);
    }
}
