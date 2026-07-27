using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MertClicker.Application.Abstractions;

namespace MertClicker.App.ViewModels.Pages;

public sealed partial class LogsViewModel : ObservableObject
{
    private readonly ILogReader _logReader;

    public LogsViewModel(ILogReader logReader)
    {
        _logReader = logReader;
    }

    public ObservableCollection<string> LogLines { get; } = [];

    [ObservableProperty]
    private string _statusMessage = "Boşta.";

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            var lines = await _logReader.ReadRecentLinesAsync();

            LogLines.Clear();
            foreach (var line in lines)
            {
                LogLines.Add(line);
            }

            StatusMessage = LogLines.Count == 0 ? "Henüz kayıt yok." : $"Son {LogLines.Count} satır gösteriliyor.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Kayıtlar okunamadı: {ex.Message}";
        }
    }
}
