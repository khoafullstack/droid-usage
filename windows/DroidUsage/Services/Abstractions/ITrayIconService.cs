using Microsoft.UI.Xaml;

namespace DroidUsage.Services.Abstractions;

public interface ITrayIconService : IDisposable
{
    event Action? ToggleVisibilityRequested;
    event Action? ToggleExpandRequested;
    event Action? RefreshRequested;
    event Action? ResetPositionRequested;
    event Action? ExitRequested;

    void Attach(Window window);

    void UpdateTooltip(string tooltip);
}
