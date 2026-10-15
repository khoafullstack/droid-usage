using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;
using WinRT;

namespace DroidUsage;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before any UI: handles install/update/uninstall hooks and exits when needed.
        VelopackApp.Build().Run();

        ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
