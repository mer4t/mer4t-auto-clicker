namespace MertClicker.Infrastructure;

public sealed class ApplicationPaths
{
    // rootDirectoryOverride yalnızca testler içindir: gerçek %LOCALAPPDATA% yerine geçici bir klasör
    // kullanarak testlerin kullanıcının asıl makro/ayar dosyalarına dokunmasını engeller.
    public ApplicationPaths(string? rootDirectoryOverride = null)
    {
        RootDirectory = rootDirectoryOverride
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MertClicker");
        MacrosDirectory = Path.Combine(RootDirectory, "macros");
        BackupsDirectory = Path.Combine(RootDirectory, "backups");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        SettingsFilePath = Path.Combine(RootDirectory, "settings.json");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(MacrosDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    public string RootDirectory { get; }

    public string MacrosDirectory { get; }

    public string BackupsDirectory { get; }

    public string LogsDirectory { get; }

    public string SettingsFilePath { get; }
}
