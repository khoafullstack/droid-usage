using System.Net.Http;
using DroidUsage.Helpers;
using DroidUsage.Models;
using DroidUsage.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.UI;

namespace DroidUsage;

public sealed partial class MainWindow : Window
{
    private const int CompactWidthDip = 276;
    private const int CompactHeightDip = 42;
    private const int ExpandedWidthDip = 440;
    private const int ExpandedHeightDip = 236;

    private readonly SolidColorBrush _brushFactoryOrange = new(Color.FromArgb(255, 255, 88, 0));
    private readonly SolidColorBrush _brushSuccess = new(Color.FromArgb(255, 34, 197, 94));
    private readonly SolidColorBrush _brushRefreshing = new(Color.FromArgb(255, 245, 158, 11));
    private readonly SolidColorBrush _brushDanger = new(Color.FromArgb(255, 239, 68, 68));
    private readonly SolidColorBrush _brushMuted = new(Color.FromArgb(255, 140, 140, 140));
    private readonly SolidColorBrush _brushWhite = new(Color.FromArgb(255, 255, 255, 255));
    private readonly SolidColorBrush _brushDarkText = new(Color.FromArgb(255, 13, 13, 13));
    private readonly SolidColorBrush _brushTransparent = new(Color.FromArgb(0, 0, 0, 0));

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly SettingsService _settingsService;
    private readonly FactoryAuthService _authService;
    private readonly FactoryApiClient _apiClient;
    private readonly TrayIconService _trayIconService;
    private readonly DispatcherTimer _refreshTimer;

    private BillingLimitsResponse? _latestLimits;
    private bool _isExpanded;
    private bool _isShowingCorePool;
    private bool _isShowingSettings;
    private bool _isWindowVisible = true;
    private bool _isRefreshing;

    private bool _isDragging;
    private bool _hasMovedDuringDrag;
    private WindowHelper.POINT _dragStartCursor;
    private PointInt32 _dragStartWindowPos;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        _authService = new FactoryAuthService(_settingsService, _httpClient);
        _apiClient = new FactoryApiClient(_authService, _httpClient);

        WindowHelper.ConfigureFloatingWidgetWindow(this);
        LoadFactoryLogoAndInitialPlan();

        _trayIconService = new TrayIconService(this);
        _trayIconService.ToggleVisibilityRequested += () => DispatcherQueue.TryEnqueue(ToggleWindowVisibility);
        _trayIconService.ToggleExpandRequested += () => DispatcherQueue.TryEnqueue(() => SetExpandedMode(!_isExpanded));
        _trayIconService.RefreshRequested += () => DispatcherQueue.TryEnqueue(() => _ = RefreshUsageAsync());
        _trayIconService.ResetPositionRequested += () => DispatcherQueue.TryEnqueue(ResetToDefaultPosition);
        _trayIconService.ExitRequested += () => DispatcherQueue.TryEnqueue(ExitApplication);

        Closed += MainWindow_Closed;

        InitializeSettingsControls();
        _isExpanded = _settingsService.Current.IsExpanded;
        ApplyPanelVisibility(_isExpanded);
        RestoreWindowPosition();

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(15, _settingsService.Current.RefreshIntervalSeconds))
        };
        _refreshTimer.Tick += async (_, _) => await RefreshUsageAsync();
        _refreshTimer.Start();

        _ = RefreshUsageAsync();
    }

    private void LoadFactoryLogoAndInitialPlan()
    {
        try
        {
            var svgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "factory-icon.svg");
            if (File.Exists(svgPath))
            {
                FactoryLogoImage.Source = new SvgImageSource(new Uri(svgPath))
                {
                    RasterizePixelWidth = 32,
                    RasterizePixelHeight = 32
                };
            }
        }
        catch
        {
        }

        UpdatePlanBadge(_authService.GetCachedFactoryTier());
    }

    private void UpdatePlanBadge(string? planTier)
    {
        if (!string.IsNullOrWhiteSpace(planTier))
        {
            PlanBadgeText.Text = planTier;
            PlanBadgeBorder.Visibility = Visibility.Visible;
        }
        else
        {
            PlanBadgeBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void InitializeSettingsControls()
    {
        ManualApiKeyBox.Password = _settingsService.Current.ManualApiKey ?? string.Empty;

        var targetTag = _settingsService.Current.PillDisplayBucket ?? "monthly";
        int metricIdx = 0;
        for (int i = 0; i < PillMetricComboBox.Items.Count; i++)
        {
            if (PillMetricComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, targetTag, StringComparison.OrdinalIgnoreCase))
            {
                metricIdx = i;
                break;
            }
        }
        PillMetricComboBox.SelectedIndex = metricIdx;

        var intervalStr = Math.Max(15, _settingsService.Current.RefreshIntervalSeconds).ToString();
        int intervalIdx = 2;
        for (int i = 0; i < RefreshIntervalComboBox.Items.Count; i++)
        {
            if (RefreshIntervalComboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, intervalStr, StringComparison.OrdinalIgnoreCase))
            {
                intervalIdx = i;
                break;
            }
        }
        RefreshIntervalComboBox.SelectedIndex = intervalIdx;
    }

    private int GetTargetHeightDip(bool expanded)
    {
        if (!expanded)
        {
            return CompactHeightDip;
        }

        ExpandedCardPanel.Measure(new Windows.Foundation.Size(ExpandedWidthDip, double.PositiveInfinity));
        int measured = (int)Math.Ceiling(ExpandedCardPanel.DesiredSize.Height) + 2;
        return measured > 160 ? measured : ExpandedHeightDip;
    }

    private void RestoreWindowPosition()
    {
        int widthDip = _isExpanded ? ExpandedWidthDip : CompactWidthDip;
        int heightDip = GetTargetHeightDip(_isExpanded);

        PointInt32 targetPos;
        if (_settingsService.Current.WindowX.HasValue && _settingsService.Current.WindowY.HasValue)
        {
            targetPos = new PointInt32(
                _settingsService.Current.WindowX.Value,
                _settingsService.Current.WindowY.Value);
        }
        else
        {
            targetPos = WindowHelper.GetDefaultTopRightPosition(this, widthDip, heightDip);
        }

        var clampedPos = WindowHelper.MoveAndClampDip(this, targetPos.X, targetPos.Y, widthDip, heightDip);
        _settingsService.Current.WindowX = clampedPos.X;
        _settingsService.Current.WindowY = clampedPos.Y;
    }

    private void ResetToDefaultPosition()
    {
        int widthDip = _isExpanded ? ExpandedWidthDip : CompactWidthDip;
        int heightDip = GetTargetHeightDip(_isExpanded);
        var defaultPos = WindowHelper.GetDefaultTopRightPosition(this, widthDip, heightDip);
        var clampedPos = WindowHelper.MoveAndClampDip(this, defaultPos.X, defaultPos.Y, widthDip, heightDip);

        var appWindow = WindowHelper.GetAppWindow(this);
        if (!_isWindowVisible)
        {
            appWindow.Show();
            _isWindowVisible = true;
        }

        _settingsService.Current.WindowX = clampedPos.X;
        _settingsService.Current.WindowY = clampedPos.Y;
        _settingsService.Save();
    }

    private void ApplyPanelVisibility(bool expanded)
    {
        if (expanded)
        {
            CollapsedPillPanel.Visibility = Visibility.Collapsed;
            ExpandedCardPanel.Visibility = Visibility.Visible;
        }
        else
        {
            _isShowingSettings = false;
            SettingsContentPanel.Visibility = Visibility.Collapsed;
            UsageContentPanel.Visibility = Visibility.Visible;
            ExpandedCardPanel.Visibility = Visibility.Collapsed;
            CollapsedPillPanel.Visibility = Visibility.Visible;
        }
    }

    private void SetExpandedMode(bool expanded, bool saveSetting = true)
    {
        _isExpanded = expanded;
        ApplyPanelVisibility(expanded);

        int targetWidthDip = expanded ? ExpandedWidthDip : CompactWidthDip;
        int targetHeightDip = GetTargetHeightDip(expanded);

        var newPos = WindowHelper.ResizeAndPositionEdgeAwareDip(this, targetWidthDip, targetHeightDip);
        DispatcherQueue.TryEnqueue(UpdateUsageDisplay);

        if (saveSetting)
        {
            _settingsService.Current.IsExpanded = expanded;
            _settingsService.Current.WindowX = newPos.X;
            _settingsService.Current.WindowY = newPos.Y;
            _settingsService.Save();
        }
    }

    private void ToggleWindowVisibility()
    {
        var appWindow = WindowHelper.GetAppWindow(this);
        if (_isWindowVisible)
        {
            appWindow.Hide();
            _isWindowVisible = false;
        }
        else
        {
            appWindow.Show();
            _isWindowVisible = true;
        }
    }

    public async Task RefreshUsageAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        RefreshStatusText.Text = "Refreshing";
        RefreshStatusText.Foreground = _brushRefreshing;
        ToolTipService.SetToolTip(RefreshStatusText, null);

        try
        {
            var result = await _apiClient.FetchBillingLimitsAsync();

            if (!result.IsSuccess || result.Data == null)
            {
                RefreshStatusText.Text = "Fail";
                RefreshStatusText.Foreground = _brushDanger;
                ToolTipService.SetToolTip(RefreshStatusText, result.ErrorMessage ?? "Failed to fetch limits");
                PillStatusDot.Fill = _brushDanger;
                return;
            }

            StatusHintText.Visibility = Visibility.Collapsed;
            _latestLimits = result.Data;
            UpdatePlanBadge(result.PlanTier);
            UpdateUsageDisplay();

            LastRefreshText.Text = $"Last refresh: {DateTime.Now:HH:mm:ss}";
            RefreshStatusText.Text = "Success";
            RefreshStatusText.Foreground = _brushSuccess;
        }
        catch (Exception ex)
        {
            RefreshStatusText.Text = "Fail";
            RefreshStatusText.Foreground = _brushDanger;
            ToolTipService.SetToolTip(RefreshStatusText, ex.Message);
            PillStatusDot.Fill = _brushDanger;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void UsageContentPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUsageDisplay();
    }

    private void PillBarTrack_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUsageDisplay();
    }

    private void UpdateUsageDisplay()
    {
        if (_latestLimits?.Limits == null)
        {
            return;
        }

        var standard = _latestLimits.Limits.Standard;
        var core = _latestLimits.Limits.Core;

        var (pillLabel, pillBucket) = SelectPillBucket(standard);
        int pillPercent = pillBucket?.DisplayPercent ?? 0;
        var pillBrush = pillPercent >= 100 ? _brushDanger : _brushFactoryOrange;

        PillStatusDot.Fill = pillBrush;
        PillBucketLabel.Text = pillLabel;
        PillPercentText.Text = $"{pillPercent}%";
        PillResetText.Text = pillBucket != null && !pillBucket.IsExpiredOrInactive
            ? pillBucket.GetFormattedResetTime(compact: true)
            : string.Empty;

        double pillTrackWidth = PillBarTrack.ActualWidth > 0 ? PillBarTrack.ActualWidth : 54;
        PillBarFill.Background = pillBrush;
        PillBarFill.Width = ComputeBarFillWidth(pillTrackWidth, pillPercent, minNonZeroPx: 5);

        var activePool = _isShowingCorePool ? core : standard;

        BindBucketRow(
            activePool?.FiveHour,
            FiveHourPercentText,
            FiveHourResetText,
            FiveHourSyncIcon,
            FiveHourBarTrack,
            FiveHourBarFill);

        BindBucketRow(
            activePool?.Weekly,
            WeeklyPercentText,
            WeeklyResetText,
            WeeklySyncIcon,
            WeeklyBarTrack,
            WeeklyBarFill);

        BindBucketRow(
            activePool?.Monthly,
            MonthlyPercentText,
            MonthlyResetText,
            MonthlySyncIcon,
            MonthlyBarTrack,
            MonthlyBarFill);

        int mPct = standard?.Monthly?.DisplayPercent ?? 0;
        int wPct = standard?.Weekly?.DisplayPercent ?? 0;
        int hPct = standard?.FiveHour?.DisplayPercent ?? 0;

        _trayIconService.UpdateTooltip(
            $"Factory Droid — Monthly: {mPct}% | Weekly: {wPct}% | 5h: {hPct}%");
    }

    private void BindBucketRow(
        UsageBucket? bucket,
        TextBlock percentText,
        TextBlock resetText,
        FontIcon syncIcon,
        Grid barTrack,
        Border barFill)
    {
        int pct = bucket?.DisplayPercent ?? 0;
        bool isInactive = bucket == null || bucket.IsExpiredOrInactive;

        percentText.Text = $"{pct}%";
        resetText.Text = bucket?.GetFormattedResetTime(compact: false) ?? "Use Droid to start";
        syncIcon.Visibility = isInactive ? Visibility.Collapsed : Visibility.Visible;

        double trackWidth = barTrack.ActualWidth > 0 ? barTrack.ActualWidth : (ExpandedWidthDip - 36);
        barFill.Background = pct >= 100 ? _brushDanger : _brushFactoryOrange;
        barFill.Width = ComputeBarFillWidth(trackWidth, pct, minNonZeroPx: 10);
    }

    private static double ComputeBarFillWidth(double trackWidth, int percent, double minNonZeroPx)
    {
        if (percent <= 0 || trackWidth <= 0)
        {
            return 0;
        }

        double clamped = Math.Clamp(percent, 0, 100);
        double proportional = trackWidth * (clamped / 100.0);
        return Math.Min(trackWidth, Math.Max(minNonZeroPx, proportional));
    }

    private (string Label, UsageBucket? Bucket) SelectPillBucket(UsagePoolLimits? pool)
    {
        if (pool == null)
        {
            return ("Monthly", null);
        }

        var mode = _settingsService.Current.PillDisplayBucket ?? "monthly";
        return mode switch
        {
            "fiveHour" => ("5-hour", pool.FiveHour),
            "weekly" => ("Weekly", pool.Weekly),
            "highest" => SelectHighestBucket(pool),
            _ => ("Monthly", pool.Monthly)
        };
    }

    private static (string Label, UsageBucket? Bucket) SelectHighestBucket(UsagePoolLimits pool)
    {
        var candidates = new (string Label, UsageBucket? Bucket)[]
        {
            ("5-hour", pool.FiveHour),
            ("Weekly", pool.Weekly),
            ("Monthly", pool.Monthly)
        };

        return candidates
            .OrderByDescending(c => c.Bucket?.DisplayPercent ?? 0)
            .First();
    }

    private void DragRegion_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            var props = e.GetCurrentPoint(element).Properties;
            if (props.IsLeftButtonPressed)
            {
                _isDragging = true;
                _hasMovedDuringDrag = false;
                WindowHelper.GetCursorPos(out _dragStartCursor);
                _dragStartWindowPos = WindowHelper.GetAppWindow(this).Position;
                element.CapturePointer(e.Pointer);
            }
        }
    }

    private void DragRegion_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        WindowHelper.GetCursorPos(out var currentCursor);
        int dx = currentCursor.X - _dragStartCursor.X;
        int dy = currentCursor.Y - _dragStartCursor.Y;

        if (!_hasMovedDuringDrag && (Math.Abs(dx) > 4 || Math.Abs(dy) > 4))
        {
            _hasMovedDuringDrag = true;
        }

        if (_hasMovedDuringDrag)
        {
            var appWindow = WindowHelper.GetAppWindow(this);
            appWindow.Move(new PointInt32(
                _dragStartWindowPos.X + dx,
                _dragStartWindowPos.Y + dy));
        }
    }

    private void DragRegion_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }

        if (_hasMovedDuringDrag)
        {
            int widthDip = _isExpanded ? ExpandedWidthDip : CompactWidthDip;
            int heightDip = _isExpanded ? ExpandedHeightDip : CompactHeightDip;
            var pos = WindowHelper.GetAppWindow(this).Position;
            var clamped = WindowHelper.MoveAndClampDip(this, pos.X, pos.Y, widthDip, heightDip);
            _settingsService.Current.WindowX = clamped.X;
            _settingsService.Current.WindowY = clamped.Y;
            _settingsService.Save();
        }
    }

    private void CollapsedPill_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        bool wasMoved = _hasMovedDuringDrag;
        DragRegion_PointerReleased(sender, e);

        if (!wasMoved)
        {
            SetExpandedMode(true);
        }
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpandedMode(true);
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e)
    {
        SetExpandedMode(false);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshUsageAsync();
    }

    private void SettingsToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _isShowingSettings = !_isShowingSettings;
        SettingsContentPanel.Visibility = _isShowingSettings ? Visibility.Visible : Visibility.Collapsed;
        UsageContentPanel.Visibility = _isShowingSettings ? Visibility.Collapsed : Visibility.Visible;
        WindowHelper.ResizeAndPositionEdgeAwareDip(this, ExpandedWidthDip, GetTargetHeightDip(true));
    }

    private void StandardPoolTabButton_Click(object sender, RoutedEventArgs e)
    {
        _isShowingCorePool = false;
        StandardPoolTabButton.Background = _brushWhite;
        StandardPoolTabButton.Foreground = _brushDarkText;
        StandardPoolTabButton.FontWeight = FontWeights.SemiBold;

        CorePoolTabButton.Background = _brushTransparent;
        CorePoolTabButton.Foreground = _brushMuted;
        CorePoolTabButton.FontWeight = FontWeights.Normal;

        UpdateUsageDisplay();
    }

    private void CorePoolTabButton_Click(object sender, RoutedEventArgs e)
    {
        _isShowingCorePool = true;
        CorePoolTabButton.Background = _brushWhite;
        CorePoolTabButton.Foreground = _brushDarkText;
        CorePoolTabButton.FontWeight = FontWeights.SemiBold;

        StandardPoolTabButton.Background = _brushTransparent;
        StandardPoolTabButton.Foreground = _brushMuted;
        StandardPoolTabButton.FontWeight = FontWeights.Normal;

        UpdateUsageDisplay();
    }

    private void PillMetricComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PillMetricComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _settingsService.Current.PillDisplayBucket = tag;
            UpdateUsageDisplay();
        }
    }

    private void RefreshIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RefreshIntervalComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            int.TryParse(tag, out int seconds) &&
            seconds >= 15)
        {
            _settingsService.Current.RefreshIntervalSeconds = seconds;
            if (_refreshTimer != null)
            {
                _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
            }
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settingsService.Current.ManualApiKey = ManualApiKeyBox.Password?.Trim() ?? string.Empty;
        if (_refreshTimer != null)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(
                Math.Max(15, _settingsService.Current.RefreshIntervalSeconds));
        }

        _settingsService.Save();

        _isShowingSettings = false;
        SettingsContentPanel.Visibility = Visibility.Collapsed;
        UsageContentPanel.Visibility = Visibility.Visible;
        WindowHelper.ResizeAndPositionEdgeAwareDip(this, ExpandedWidthDip, GetTargetHeightDip(true));

        await RefreshUsageAsync();
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowVisibility();
    }

    private void ExitApplication()
    {
        _trayIconService.Dispose();
        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _refreshTimer.Stop();
        _trayIconService.Dispose();
        _httpClient.Dispose();
    }
}
