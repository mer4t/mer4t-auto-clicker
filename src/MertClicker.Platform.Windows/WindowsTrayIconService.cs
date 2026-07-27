using System.Drawing;
using System.Windows.Forms;
using MertClicker.Application.Abstractions;

namespace MertClicker.Platform.Windows;

// System.Windows.Forms.NotifyIcon kullanır. NotifyIcon kendi başına bir mesaj döngüsü kurmaz;
// WPF ana UI thread'inin Dispatcher'ı zaten aynı Win32 mesaj kuyruğunu pompaladığı için (hotkey ve
// fare kancasıyla aynı mekanizma), Show() bu thread'den çağrıldığı sürece olaylar sorunsuz çalışır.
public sealed class WindowsTrayIconService : ITrayIconService
{
    private readonly IApplicationLogger _logger;
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    public WindowsTrayIconService(IApplicationLogger logger)
    {
        _logger = logger;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? EmergencyStopRequested;

    public void Show()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsTrayIconService));
        }

        if (_notifyIcon is not null)
        {
            _logger.LogWarning("WindowsTrayIconService.Show birden fazla kez çağrıldı, yok sayılıyor.");
            return;
        }

        var showItem = new ToolStripMenuItem("Göster");
        showItem.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var emergencyStopItem = new ToolStripMenuItem("Acil Durdur (F9)");
        emergencyStopItem.Click += (_, _) => EmergencyStopRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Çıkış");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(showItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(emergencyStopItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "MertClicker",
            ContextMenuStrip = contextMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation("Sistem tepsisi simgesi gösterildi.");
    }

    public void ShowBalloonTip(string title, string text)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(3000);
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(exePath);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch
        {
            // Simge çıkarılamazsa sistem varsayılanına düşülür; tepsi simgesinin görünmemesine
            // sebep olacak kadar önemli bir hata değil.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _logger.LogInformation("Sistem tepsisi simgesi kaldırıldı.");
    }
}
