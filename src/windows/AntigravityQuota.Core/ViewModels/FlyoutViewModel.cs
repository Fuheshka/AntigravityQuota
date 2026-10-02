using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core.ViewModels;

public class FlyoutViewModel : INotifyPropertyChanged
{
    private readonly LocalizationManager _loc;
    private readonly QuotaClient _client;
    private readonly QuotaHistoryTracker _history;
    private readonly Func<Task>? _triggerRefreshCallback;

    private double _geminiFiveHourPct = 100.0;
    private double _geminiWeeklyPct = 100.0;
    private double _claudeFiveHourPct = 100.0;
    private double _claudeWeeklyPct = 100.0;

    private string _geminiCountdownText = "100%";
    private string _claudeCountdownText = "100%";
    private string _geminiWeeklyCountdownText = "100%";
    private string _claudeWeeklyCountdownText = "100%";

    private string _geminiBurnRateText = "~0%/ч";
    private string _claudeBurnRateText = "~0%/ч";

    private string _geminiSparklinePath = "";
    private string _claudeSparklinePath = "";
    private bool _hasSparklineData = false;

    private bool _isOnline = false;
    private string _statusBadgeText = "Offline";
    private string _lastUpdatedText = "--:--:--";
    private bool _isRefreshing = false;
    private string _trayTooltipText = "AntigravityQuota\nOffline";

    public FlyoutViewModel(
        QuotaClient? client = null,
        QuotaHistoryTracker? history = null,
        LocalizationManager? loc = null,
        Func<Task>? triggerRefreshCallback = null)
    {
        _client = client ?? new QuotaClient();
        _history = history ?? new QuotaHistoryTracker();
        _loc = loc ?? LocalizationManager.Instance;
        _triggerRefreshCallback = triggerRefreshCallback;

        _statusBadgeText = _loc.StatusOffline;
        _trayTooltipText = _loc.FormatTrayTooltip(null, null);
        _geminiBurnRateText = _loc.IsRussian ? "~0%/ч" : "~0%/h";
        _claudeBurnRateText = _loc.IsRussian ? "~0%/ч" : "~0%/h";

        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !IsRefreshing);
        ShowAboutCommand = new RelayCommand(() => OnShowAboutRequested?.Invoke());
    }

    public LocalizationManager Loc => _loc;

    public double GeminiFiveHourPct
    {
        get => _geminiFiveHourPct;
        set => SetField(ref _geminiFiveHourPct, value);
    }

    public double GeminiWeeklyPct
    {
        get => _geminiWeeklyPct;
        set => SetField(ref _geminiWeeklyPct, value);
    }

    public double ClaudeFiveHourPct
    {
        get => _claudeFiveHourPct;
        set => SetField(ref _claudeFiveHourPct, value);
    }

    public double ClaudeWeeklyPct
    {
        get => _claudeWeeklyPct;
        set => SetField(ref _claudeWeeklyPct, value);
    }

    public string GeminiCountdownText
    {
        get => _geminiCountdownText;
        set => SetField(ref _geminiCountdownText, value);
    }

    public string ClaudeCountdownText
    {
        get => _claudeCountdownText;
        set => SetField(ref _claudeCountdownText, value);
    }

    public string GeminiWeeklyCountdownText
    {
        get => _geminiWeeklyCountdownText;
        set => SetField(ref _geminiWeeklyCountdownText, value);
    }

    public string ClaudeWeeklyCountdownText
    {
        get => _claudeWeeklyCountdownText;
        set => SetField(ref _claudeWeeklyCountdownText, value);
    }

    public string GeminiBurnRateText
    {
        get => _geminiBurnRateText;
        set => SetField(ref _geminiBurnRateText, value);
    }

    public string ClaudeBurnRateText
    {
        get => _claudeBurnRateText;
        set => SetField(ref _claudeBurnRateText, value);
    }

    public string GeminiSparklinePath
    {
        get => _geminiSparklinePath;
        set => SetField(ref _geminiSparklinePath, value);
    }

    public string ClaudeSparklinePath
    {
        get => _claudeSparklinePath;
        set => SetField(ref _claudeSparklinePath, value);
    }

    public bool HasSparklineData
    {
        get => _hasSparklineData;
        set => SetField(ref _hasSparklineData, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetField(ref _isOnline, value);
    }

    public string StatusBadgeText
    {
        get => _statusBadgeText;
        set => SetField(ref _statusBadgeText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set => SetField(ref _lastUpdatedText, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (SetField(ref _isRefreshing, value))
            {
                (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string TrayTooltipText
    {
        get => _trayTooltipText;
        set => SetField(ref _trayTooltipText, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public event Action? OnShowAboutRequested;

    public async Task RefreshAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;

        try
        {
            if (_triggerRefreshCallback != null)
            {
                await _triggerRefreshCallback();
            }
            else
            {
                var snapshot = await _client.FetchSnapshotAsync(forceRefresh: true);
                UpdateFromSnapshot(snapshot);
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public virtual void UpdateFromSnapshot(QuotaSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            IsOnline = false;
            StatusBadgeText = _loc.StatusOffline;
            TrayTooltipText = _loc.FormatTrayTooltip(null, null);
            return;
        }

        IsOnline = true;
        StatusBadgeText = _loc.StatusOnline;
        LastUpdatedText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        // Record to history tracker
        _history.Record(snapshot);

        // Extract Gemini and Claude groups
        var geminiGroup = snapshot.GeminiGroup;
        var claudeGroup = snapshot.ClaudeGroup;

        // 1. Gemini
        double? g5h = geminiGroup?.FiveHourBucket?.Percentage;
        double? gWeek = geminiGroup?.WeeklyBucket?.Percentage;
        if (!g5h.HasValue && geminiGroup?.Buckets.Count > 0)
            g5h = geminiGroup.Buckets[0].Percentage;

        GeminiFiveHourPct = g5h ?? 100.0;
        GeminiWeeklyPct = gWeek ?? 100.0;

        if (geminiGroup?.FiveHourBucket?.TimeUntilReset is { } g5hRem && g5hRem > TimeSpan.Zero)
        {
            GeminiCountdownText = $"{_loc.ResetsIn} {_loc.FormatResetCountdown(g5hRem)}";
        }
        else
        {
            GeminiCountdownText = $"{GeminiFiveHourPct:F1}%";
        }

        if (geminiGroup?.WeeklyBucket?.TimeUntilReset is { } gWeekRem && gWeekRem > TimeSpan.Zero)
        {
            GeminiWeeklyCountdownText = $"{_loc.ResetsIn} {_loc.FormatResetCountdown(gWeekRem)}";
        }
        else
        {
            GeminiWeeklyCountdownText = $"{GeminiWeeklyPct:F1}%";
        }

        // 2. Claude
        double? c5h = claudeGroup?.FiveHourBucket?.Percentage;
        double? cWeek = claudeGroup?.WeeklyBucket?.Percentage;
        if (!c5h.HasValue && claudeGroup?.Buckets.Count > 0)
            c5h = claudeGroup.Buckets[0].Percentage;

        ClaudeFiveHourPct = c5h ?? 100.0;
        ClaudeWeeklyPct = cWeek ?? 100.0;

        if (claudeGroup?.FiveHourBucket?.TimeUntilReset is { } c5hRem && c5hRem > TimeSpan.Zero)
        {
            ClaudeCountdownText = $"{_loc.ResetsIn} {_loc.FormatResetCountdown(c5hRem)}";
        }
        else
        {
            ClaudeCountdownText = $"{ClaudeFiveHourPct:F1}%";
        }

        if (claudeGroup?.WeeklyBucket?.TimeUntilReset is { } cWeekRem && cWeekRem > TimeSpan.Zero)
        {
            ClaudeWeeklyCountdownText = $"{_loc.ResetsIn} {_loc.FormatResetCountdown(cWeekRem)}";
        }
        else
        {
            ClaudeWeeklyCountdownText = $"{ClaudeWeeklyPct:F1}%";
        }

        // 3. Burn Rate
        var gBurn = _history.CalculateBurnRate(QuotaPool.Gemini);
        var cBurn = _history.CalculateBurnRate(QuotaPool.Claude);
        GeminiBurnRateText = gBurn.Formatted(_loc.IsRussian);
        ClaudeBurnRateText = cBurn.Formatted(_loc.IsRussian);

        // 4. Sparkline mini-charts (width: 320, height: 48)
        var sparklineData = _history.GetSparklineData(width: 320, height: 48);
        HasSparklineData = sparklineData.HasSufficientData;
        if (HasSparklineData)
        {
            GeminiSparklinePath = sparklineData.ToPathGeometry(QuotaPool.Gemini);
            ClaudeSparklinePath = sparklineData.ToPathGeometry(QuotaPool.Claude);
        }
        else
        {
            GeminiSparklinePath = "";
            ClaudeSparklinePath = "";
        }

        // 5. Tooltip
        TrayTooltipText = _loc.FormatTrayTooltip(GeminiFiveHourPct, ClaudeFiveHourPct);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(() => { execute(); return Task.CompletedTask; }, canExecute)
    {
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await _execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
