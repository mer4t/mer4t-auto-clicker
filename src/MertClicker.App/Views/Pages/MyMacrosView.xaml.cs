using System.Windows.Controls;
using MertClicker.App.ViewModels.Pages;

namespace MertClicker.App.Views.Pages;

public partial class MyMacrosView : UserControl
{
    public MyMacrosView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ViewModel singleton olduğu için (kütüphane Aşama 7'de disk tabanlı), sayfaya her navigasyonda
    // (View implicit DataTemplate ile yeniden oluşturulduğunda) listeyi güncel tutmak için yenileriz.
    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is MyMacrosViewModel viewModel)
        {
            await viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }
}
