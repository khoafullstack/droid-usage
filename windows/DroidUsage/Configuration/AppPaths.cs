namespace DroidUsage.Configuration;

public static class AppPaths
{
    public static string AppDataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DroidUsage");

    public static string SettingsFile { get; } = Path.Combine(AppDataDir, "settings.json");

    public static string LogsDir { get; } = Path.Combine(AppDataDir, "logs");

    public static string LogFile { get; } = Path.Combine(LogsDir, "app.log");

    public static string FactoryDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".factory");
}
