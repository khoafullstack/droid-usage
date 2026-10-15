using Microsoft.UI.Xaml;

namespace DroidUsage;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DroidUsage",
                    "crash.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, $"{DateTime.Now:O}\n{e.Message}\n{e.Exception}");
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DroidUsage",
                "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, $"{DateTime.Now:O}\n{ex}");
            throw;
        }
    }
}
