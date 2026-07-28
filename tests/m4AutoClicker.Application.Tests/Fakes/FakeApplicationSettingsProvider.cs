using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Application.Models;

namespace m4AutoClicker.Application.Tests.Fakes;

public sealed class FakeApplicationSettingsProvider : IApplicationSettingsProvider
{
    public ApplicationSettings Current { get; set; } = new();

    public void Update(ApplicationSettings settings) => Current = settings;
}
