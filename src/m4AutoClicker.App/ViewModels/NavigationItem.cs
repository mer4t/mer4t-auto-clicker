using CommunityToolkit.Mvvm.ComponentModel;

namespace m4AutoClicker.App.ViewModels;

public sealed partial class NavigationItem : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public NavigationItem(string title, object viewModel)
    {
        Title = title;
        ViewModel = viewModel;
    }

    public string Title { get; }

    public object ViewModel { get; }
}
