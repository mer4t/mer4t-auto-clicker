using MertClicker.Application.Models;
using MertClicker.Application.Services;

namespace MertClicker.Application.Tests.Services;

public class ApplicationSettingsProviderTests
{
    [Fact]
    public void Current_Returns_Defaults_Before_Any_Update()
    {
        var provider = new ApplicationSettingsProvider();

        Assert.Equal(8, provider.Current.MouseMovementSampling.MinimumIntervalMilliseconds);
        Assert.Equal(2, provider.Current.MouseMovementSampling.MinimumDistancePixels);
    }

    [Fact]
    public void Update_Then_Current_Reflects_New_Value_Immediately()
    {
        var provider = new ApplicationSettingsProvider();
        var updated = new ApplicationSettings
        {
            MouseMovementSampling = new MouseMovementSamplingSettings { MinimumIntervalMilliseconds = 25, MinimumDistancePixels = 10 }
        };

        provider.Update(updated);

        Assert.Equal(25, provider.Current.MouseMovementSampling.MinimumIntervalMilliseconds);
        Assert.Equal(10, provider.Current.MouseMovementSampling.MinimumDistancePixels);
    }
}
