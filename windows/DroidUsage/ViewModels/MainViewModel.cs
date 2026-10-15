using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DroidUsage.Models;
using DroidUsage.Presentation;
using DroidUsage.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace DroidUsage.ViewModels;

public enum RefreshStatus
{
    Refreshing,
    Success,
    Failed
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IUsageApiClient _apiClient;
    private readonly IPlanTierProvider _planTierProvider;
    private readonly ISettingsService _settings;
    private readonly UsagePresenter _presenter;
    private readonly IClock _clock;
    private readonly ILogger<MainViewModel> _logger;
    private readonly DispatcherQueueTimer _refreshTimer;

    private BillingLimitsResponse? _latestLimits;
    private bool _isRefreshing;

    public MainViewModel(
        IUsageApiClient apiClient,
        IPlanTierProvider planTierProvider,
        ISettingsService settings,
        UsagePresenter presenter,
        IClock clock,
        ILogger<MainViewModel> logger)
    {
        _apiClient = apiClient;
        _planTierProvider = planTierProvider;
        _settings = settings;
        _presenter = presenter;
        _clock = clock;
        _logger = logger;

        _isExpanded = settings.Current.IsExpanded;
        _manualApiKey = settings.ManualApiKey;
        _planTier = planTierProvider.GetCachedTier();

        _refreshTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _refreshTimer.IsRepeating = true;
        _refreshTimer.Interval = TimeSpan.FromSeconds(
            UsagePresenter.NormalizeRefreshSeconds(settings.Current.RefreshIntervalSeconds));
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
    }

    public UsageRowViewModel FiveHour { get; } = new();

    public UsageRowViewModel Weekly { get; } = new();

    public UsageRowViewModel Monthly { get; } = new();

    public UsageRowViewModel Pill { get; } = new();

    /// <summary>Raised after usage numbers are recomputed so the view can resize bars.</summary>
    public event Action? DisplayUpdated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCollapsed))]
    private bool _isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingUsage))]
    private bool _isShowingSettings;

    [ObservableProperty]
    private bool _isShowingCorePool;

    [ObservableProperty]
    private string _pillLabel = "Monthly";

    [ObservableProperty]
    private string _pillPercentText = "--%";

    [ObservableProperty]
    private string _pillResetText = string.Empty;

    [ObservableProperty]
    private bool _isPillDanger;

    [ObservableProperty]
    private RefreshStatus _status = RefreshStatus.Refreshing;

    [ObservableProperty]
    private string? _statusTooltip;

    [ObservableProperty]
    private string _lastRefreshText = "Last refresh: --:--:--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlanTier))]
    private string? _planTier;

    [ObservableProperty]
    private string _manualApiKey = string.Empty;

    [ObservableProperty]
    private string _trayTooltip = "Factory Droid Usage";

    public bool IsCollapsed => !IsExpanded;

    public bool IsShowingUsage => !IsShowingSettings;

    public bool HasPlanTier => !string.IsNullOrWhiteSpace(PlanTier);

    public string StatusText => Status switch
    {
        RefreshStatus.Success => "Success",
        RefreshStatus.Failed => "Fail",
        _ => "Refreshing"
    };

    public string PillDisplayBucket
    {
        get => _settings.Current.PillDisplayBucket ?? "monthly";
        set
        {
            if (_settings.Current.PillDisplayBucket == value)
            {
                return;
            }

            _settings.Current.PillDisplayBucket = value;
            OnPropertyChanged();
            UpdateUsageDisplay();
        }
    }

    public int RefreshIntervalSeconds
    {
        get => UsagePresenter.NormalizeRefreshSeconds(_settings.Current.RefreshIntervalSeconds);
        set
        {
            if (value < UsagePresenter.MinRefreshSeconds || value == _settings.Current.RefreshIntervalSeconds)
            {
                return;
            }

            _settings.Current.RefreshIntervalSeconds = value;
            _refreshTimer.Interval = TimeSpan.FromSeconds(value);
            OnPropertyChanged();
        }
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!value)
        {
            IsShowingSettings = false;
        }
    }

    partial void OnStatusChanged(RefreshStatus value) => OnPropertyChanged(nameof(StatusText));

    public void Start()
    {
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    public void Stop() => _refreshTimer.Stop();

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void Expand() => IsExpanded = true;

    [RelayCommand]
    private void Collapse() => IsExpanded = false;

    [RelayCommand]
    private void ToggleSettings() => IsShowingSettings = !IsShowingSettings;

    [RelayCommand]
    private void SelectStandardPool()
    {
        IsShowingCorePool = false;
        UpdateUsageDisplay();
    }

    [RelayCommand]
    private void SelectCorePool()
    {
        IsShowingCorePool = true;
        UpdateUsageDisplay();
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        _settings.ManualApiKey = ManualApiKey?.Trim() ?? string.Empty;
        _refreshTimer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
        _settings.Save();

        IsShowingSettings = false;
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        Status = RefreshStatus.Refreshing;
        StatusTooltip = null;

        try
        {
            var result = await _apiClient.FetchBillingLimitsAsync();

            if (!result.IsSuccess || result.Data == null)
            {
                MarkFailed(result.ErrorMessage ?? "Failed to fetch limits");
                return;
            }

            _latestLimits = result.Data;
            PlanTier = result.PlanTier;
            UpdateUsageDisplay();

            LastRefreshText = $"Last refresh: {_clock.Now:HH:mm:ss}";
            Status = RefreshStatus.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while refreshing usage.");
            MarkFailed(ex.Message);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void UpdateUsageDisplay()
    {
        if (_latestLimits?.Limits == null)
        {
            return;
        }

        var standard = _latestLimits.Limits.Standard;
        var core = _latestLimits.Limits.Core;

        var (label, pillBucket) = _presenter.SelectPillBucket(standard, PillDisplayBucket);
        int pillPercent = _presenter.DisplayPercent(pillBucket);

        IsPillDanger = pillPercent >= 100;
        PillLabel = label;
        PillPercentText = $"{pillPercent}%";
        PillResetText = pillBucket != null && !_presenter.IsInactive(pillBucket)
            ? _presenter.FormatResetTime(pillBucket, compact: true)
            : string.Empty;
        Pill.Percent = pillPercent;

        var activePool = IsShowingCorePool ? core : standard;
        BindRow(FiveHour, activePool?.FiveHour);
        BindRow(Weekly, activePool?.Weekly);
        BindRow(Monthly, activePool?.Monthly);

        TrayTooltip = _presenter.BuildTooltip(standard);
        DisplayUpdated?.Invoke();
    }

    private void MarkFailed(string message)
    {
        Status = RefreshStatus.Failed;
        StatusTooltip = message;
        IsPillDanger = true;
    }

    private void BindRow(UsageRowViewModel row, UsageBucket? bucket)
    {
        int pct = _presenter.DisplayPercent(bucket);
        row.Percent = pct;
        row.PercentText = $"{pct}%";
        row.ResetText = _presenter.FormatResetTime(bucket, compact: false);
        row.IsActive = !_presenter.IsInactive(bucket);
        row.IsDanger = pct >= 100;
    }
}
