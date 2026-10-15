using System.ComponentModel;
using DroidUsage.Platform;
using DroidUsage.Presentation;
using DroidUsage.Services.Abstractions;
using DroidUsage.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.UI;

namespace DroidUsage.Views;

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

    private readonly ISettingsService _settingsService;
    private readonly ITrayIconService _trayIconService;
    private readonly ILogger<MainWindow> _logger;

    private bool _isWindowVisible = true;
    private bool _isDragging;
    private bool _hasMovedDuringDrag;
    private WindowHelper.POINT _dragStartCursor;
    private PointInt32 _dragStartWindowPos;

    public MainViewModel ViewModel { get; }

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settingsService,
        ITrayIconService trayIconService,
        ILogger<MainWindow> logger)
    {
        ViewModel = viewModel;
        _settingsService = settingsService;
        _trayIconService = trayIconService;
        _logger = logger;

        InitializeComponent();

        WindowHelper.ConfigureFloatingWidgetWindow(this);
        LoadFactoryLogo();

        _trayIconService.Attach(this);
        _trayIconService.ToggleVisibilityRequested += () => DispatcherQueue.TryEnqueue(ToggleWindowVisibility);
        _trayIconService.ToggleExpandRequested += () => DispatcherQueue.TryEnqueue(() => ViewModel.ToggleExpandCommand.Execute(null));
        _trayIconService.RefreshRequested += () => DispatcherQueue.TryEnqueue(() => _ = ViewModel.RefreshAsync());
        _trayIconService.ResetPositionRequested += () => DispatcherQueue.TryEnqueue(ResetToDefaultPosition);
        _trayIconService.ExitRequested += () => DispatcherQueue.TryEnqueue(ExitApplication);

        Closed += MainWindow_Closed;

        InitializeSettingsControls();
        RestoreWindowPosition();
        ApplyPoolTabStyles();
        ApplyStatusVisuals();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.DisplayUpdated += UpdateBars;
        ViewModel.Start();
    }

    private void LoadFactoryLogo()
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load the Factory logo.");
        }
    }

    private void InitializeSettingsControls()
    {
        SelectComboItemByTag(PillMetricComboBox, ViewModel.PillDisplayBucket, fallbackIndex: 2);
        SelectComboItemByTag(RefreshIntervalComboBox, ViewModel.RefreshIntervalSeconds.ToString(), fallbackIndex: 2);
    }

    private static void SelectComboItemByTag(ComboBox comboBox, string tag, int fallbackIndex)
    {
        int index = fallbackIndex;
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item &&
                string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        comboBox.SelectedIndex = index;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsExpanded):
                OnExpandedChanged();
                break;
            case nameof(MainViewModel.IsShowingSettings):
                if (ViewModel.IsExpanded)
                {
                    WindowHelper.ResizeAndPositionEdgeAwareDip(this, ExpandedWidthDip, GetTargetHeightDip(true));
                }
                break;
            case nameof(MainViewModel.IsShowingCorePool):
                ApplyPoolTabStyles();
                break;
            case nameof(MainViewModel.Status):
            case nameof(MainViewModel.StatusTooltip):
                ApplyStatusVisuals();
                break;
            case nameof(MainViewModel.IsPillDanger):
                PillStatusDot.Fill = ViewModel.IsPillDanger ? _brushDanger : _brushFactoryOrange;
                UpdateBars();
                break;
            case nameof(MainViewModel.TrayTooltip):
                _trayIconService.UpdateTooltip(ViewModel.TrayTooltip);
                break;
        }
    }

    private void OnExpandedChanged()
    {
        bool expanded = ViewModel.IsExpanded;
        int targetWidthDip = expanded ? ExpandedWidthDip : CompactWidthDip;
        int targetHeightDip = GetTargetHeightDip(expanded);

        var newPos = WindowHelper.ResizeAndPositionEdgeAwareDip(this, targetWidthDip, targetHeightDip);
        DispatcherQueue.TryEnqueue(UpdateBars);

        _settingsService.Current.IsExpanded = expanded;
        _settingsService.Current.WindowX = newPos.X;
        _settingsService.Current.WindowY = newPos.Y;
        _settingsService.Save();
    }

    private void ApplyStatusVisuals()
    {
        RefreshStatusText.Foreground = ViewModel.Status switch
        {
            RefreshStatus.Success => _brushSuccess,
            RefreshStatus.Failed => _brushDanger,
            _ => _brushRefreshing
        };
        ToolTipService.SetToolTip(RefreshStatusText, ViewModel.StatusTooltip);
    }

    private void ApplyPoolTabStyles()
    {
        bool core = ViewModel.IsShowingCorePool;
        StyleTab(StandardPoolTabButton, active: !core);
        StyleTab(CorePoolTabButton, active: core);
    }

    private void StyleTab(Button button, bool active)
    {
        button.Background = active ? _brushWhite : _brushTransparent;
        button.Foreground = active ? _brushDarkText : _brushMuted;
        button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void UpdateBars()
    {
        var pillBrush = ViewModel.IsPillDanger ? _brushDanger : _brushFactoryOrange;
        PillStatusDot.Fill = pillBrush;
        double pillTrackWidth = PillBarTrack.ActualWidth > 0 ? PillBarTrack.ActualWidth : 54;
        PillBarFill.Background = pillBrush;
        PillBarFill.Width = UsagePresenter.ComputeBarFillWidth(pillTrackWidth, ViewModel.Pill.Percent, minNonZeroPx: 5);

        UpdateRowBar(ViewModel.FiveHour, FiveHourBarTrack, FiveHourBarFill);
        UpdateRowBar(ViewModel.Weekly, WeeklyBarTrack, WeeklyBarFill);
        UpdateRowBar(ViewModel.Monthly, MonthlyBarTrack, MonthlyBarFill);
    }

    private void UpdateRowBar(UsageRowViewModel row, Grid track, Border fill)
    {
        double trackWidth = track.ActualWidth > 0 ? track.ActualWidth : (ExpandedWidthDip - 36);
        fill.Background = row.IsDanger ? _brushDanger : _brushFactoryOrange;
        fill.Width = UsagePresenter.ComputeBarFillWidth(trackWidth, row.Percent, minNonZeroPx: 10);
    }

    private void UsageContentPanel_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateBars();

    private void PillBarTrack_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateBars();

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
        bool expanded = ViewModel.IsExpanded;
        int widthDip = expanded ? ExpandedWidthDip : CompactWidthDip;
        int heightDip = GetTargetHeightDip(expanded);

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
        bool expanded = ViewModel.IsExpanded;
        int widthDip = expanded ? ExpandedWidthDip : CompactWidthDip;
        int heightDip = GetTargetHeightDip(expanded);
        var defaultPos = WindowHelper.GetDefaultTopRightPosition(this, widthDip, heightDip);
        var clampedPos = WindowHelper.MoveAndClampDip(this, defaultPos.X, defaultPos.Y, widthDip, heightDip);

        if (!_isWindowVisible)
        {
            WindowHelper.GetAppWindow(this).Show();
            _isWindowVisible = true;
        }

        _settingsService.Current.WindowX = clampedPos.X;
        _settingsService.Current.WindowY = clampedPos.Y;
        _settingsService.Save();
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
            WindowHelper.GetAppWindow(this).Move(new PointInt32(
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
            int widthDip = ViewModel.IsExpanded ? ExpandedWidthDip : CompactWidthDip;
            int heightDip = ViewModel.IsExpanded ? ExpandedHeightDip : CompactHeightDip;
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
            ViewModel.ExpandCommand.Execute(null);
        }
    }

    private void PillMetricComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PillMetricComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            ViewModel.PillDisplayBucket = tag;
        }
    }

    private void RefreshIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RefreshIntervalComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tag &&
            int.TryParse(tag, out int seconds))
        {
            ViewModel.RefreshIntervalSeconds = seconds;
        }
    }

    private void HideToTrayButton_Click(object sender, RoutedEventArgs e) => ToggleWindowVisibility();

    private void ExitApplication()
    {
        _trayIconService.Dispose();
        Application.Current.Exit();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        ViewModel.Stop();
        _trayIconService.Dispose();
    }
}
