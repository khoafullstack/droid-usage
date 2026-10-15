using CommunityToolkit.Mvvm.ComponentModel;

namespace DroidUsage.ViewModels;

public sealed partial class UsageRowViewModel : ObservableObject
{
    [ObservableProperty]
    private int _percent;

    [ObservableProperty]
    private string _percentText = "0%";

    [ObservableProperty]
    private string _resetText = "--";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDanger;
}
