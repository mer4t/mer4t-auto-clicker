using MertClicker.Application.Abstractions;
using MertClicker.Application.Models;

namespace MertClicker.Application.Tests.Fakes;

public sealed class FakeApplicationSettingsProvider : IApplicationSettingsProvider
{
    public ApplicationSettings Current { get; set; } = new();

    public void Update(ApplicationSettings settings) => Current = settings;
}
