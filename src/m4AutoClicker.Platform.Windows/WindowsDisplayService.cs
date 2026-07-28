using m4AutoClicker.Application.Abstractions;
using m4AutoClicker.Domain.Display;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace m4AutoClicker.Platform.Windows;

public sealed class WindowsDisplayService : IDisplayService
{
    private readonly IApplicationLogger _logger;

    public WindowsDisplayService(IApplicationLogger logger)
    {
        _logger = logger;
    }

    public unsafe DisplaySnapshot GetSnapshot()
    {
        var virtualLeft = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var virtualTop = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var virtualWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var virtualHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

        var monitors = new List<MonitorSnapshot>();

        PInvoke.EnumDisplayMonitors(
            HDC.Null,
            (RECT?)null,
            (HMONITOR hMonitor, HDC _, RECT* _, LPARAM _) =>
            {
                var snapshot = CreateMonitorSnapshot(hMonitor);
                if (snapshot is not null)
                {
                    monitors.Add(snapshot);
                }

                return true;
            },
            IntPtr.Zero);

        return new DisplaySnapshot
        {
            VirtualLeft = virtualLeft,
            VirtualTop = virtualTop,
            VirtualWidth = virtualWidth,
            VirtualHeight = virtualHeight,
            Monitors = monitors
        };
    }

    private unsafe MonitorSnapshot? CreateMonitorSnapshot(HMONITOR hMonitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);

        if (!PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&info))
        {
            // Başarısızsa info.monitorInfo sıfırla doldurulmuş kalır; bunu sessizce (0,0,0,0)
            // sınırlarla döndürmek yerine bu monitörü tamamen atlıyoruz ki koordinat dönüşümü
            // yanlış bir hedefe tıklamaya yol açmasın.
            _logger.LogWarning("GetMonitorInfo başarısız oldu, bu monitör anlık görüntüye dahil edilmiyor.");
            return null;
        }

        uint dpiX = 96;
        uint dpiY = 96;
        var dpiResult = PInvoke.GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY);
        if (dpiResult.Failed)
        {
            // DPI alınamazsa varsayılan 96 ile devam edilir; bu, tam monitörü atlamayı
            // gerektirecek kadar kritik değil, yalnızca ölçekleme hesaplarını etkileyebilir.
            _logger.LogWarning("GetDpiForMonitor başarısız oldu, varsayılan 96 DPI kullanılıyor. HRESULT: {0}", dpiResult);
            dpiX = 96;
            dpiY = 96;
        }

        var isPrimary = (info.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0;

        return new MonitorSnapshot
        {
            DeviceId = info.szDevice.ToString(),
            Bounds = new MonitorBounds(
                info.monitorInfo.rcMonitor.left,
                info.monitorInfo.rcMonitor.top,
                info.monitorInfo.rcMonitor.right,
                info.monitorInfo.rcMonitor.bottom),
            WorkingArea = new MonitorBounds(
                info.monitorInfo.rcWork.left,
                info.monitorInfo.rcWork.top,
                info.monitorInfo.rcWork.right,
                info.monitorInfo.rcWork.bottom),
            DpiX = (int)dpiX,
            DpiY = (int)dpiY,
            IsPrimary = isPrimary
        };
    }
}
