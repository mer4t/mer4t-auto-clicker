using System.Windows.Controls;
using m4AutoClicker.App.ViewModels.Pages;

namespace m4AutoClicker.App.Views.Pages;

public partial class LogsView : UserControl
{
    public LogsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is LogsViewModel viewModel)
        {
            await viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }
}
