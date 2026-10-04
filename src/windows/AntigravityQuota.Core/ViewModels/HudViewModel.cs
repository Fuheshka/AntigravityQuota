using System.Globalization;
using System.Windows.Input;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core.ViewModels;

public class HudViewModel : FlyoutViewModel
{
    private readonly HudSettings _settings;
    private readonly string? _settingsPath;

    private bool _isEnabled = true;
    private bool _isPillMode;
    private bool _isSparklineExpanded;
    private bool _autoHideEnabled = true;
    private bool _clickThroughEnabled = true;
    private bool _hotkeysEnabled = true;
    private double? _windowX;
    private double? _windowY;

    private string _geminiBarColor = "#10B981";
    private string _claudeBarColor = "#10B981";

    private string _pillGeminiText = "G --%";
    private string _pillClaudeText = "C --%";
    private string _pillCombinedText = "Offline";
    private string _pillGeminiColor = "#9CA3AF";
    private string _pillClaudeColor = "#9CA3AF";

    private string _geminiResetCountdownParentheses = "";
    private string _claudeResetCountdownParentheses = "";
    private string _geminiWeeklyText = "Недельный: --%";
    private string _claudeWeeklyText = "Недельный: --%";
    private string _geminiWeeklyResetText = "";
    private string _claudeWeeklyResetText = "";
    private string _footerUpdatedText = "Обновлено в --:--";

    private string _geminiTrendArrow = "";
    private string _geminiTrendColor = "#F5A624";
    private string _geminiBurnRateFormatted = "";
    private bool _hasGeminiBurnRate = false;

    private string _claudeTrendArrow = "";
    private string _claudeTrendColor = "#F5A624";
    private string _claudeBurnRateFormatted = "";
    private bool _hasClaudeBurnRate = false;

    public HudViewModel(
        HudSettings? settings = null,
        string? settingsPath = null,
        QuotaClient? client = null,
        QuotaHistoryTracker? history = null,
        LocalizationManager? loc = null,
        Func<Task>? triggerRefreshCallback = null)
        : base(client, history, loc, triggerRefreshCallback)
    {
        _settingsPath = settingsPath;
        _settings = settings ?? HudSettingsManager.Load(_settingsPath);

        _isEnabled = _settings.IsEnabled;
        _isPillMode = _settings.IsPillMode;
        _isSparklineExpanded = _settings.IsSparklineExpanded;
        _autoHideEnabled = _settings.AutoHideEnabled;
        _clickThroughEnabled = _settings.ClickThroughEnabled;
        _hotkeysEnabled = _settings.HotkeysEnabled;
        _windowX = _settings.X;
        _windowY = _settings.Y;

        TogglePillModeCommand = new RelayCommand(() =>
        {
            IsPillMode = !IsPillMode;
        });

        ToggleSparklineCommand = new RelayCommand(() =>
        {
            IsSparklineExpanded = !IsSparklineExpanded;
        });
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetField(ref _isEnabled, value))
            {
                _settings.IsEnabled = value;
                HudSettingsManager.Save(_settings, _settingsPath);
            }
        }
    }

    public string GeminiBarColor
    {
        get => _geminiBarColor;
        set => SetField(ref _geminiBarColor, value);
    }

    public string ClaudeBarColor
    {
        get => _claudeBarColor;
        set => SetField(ref _claudeBarColor, value);
    }

    public bool IsPillMode
    {
        get => _isPillMode;
        set
        {
            if (SetField(ref _isPillMode, value))
            {
                _settings.IsPillMode = value;
                HudSettingsManager.Save(_settings, _settingsPath);
            }
        }
    }

    public bool IsSparklineExpanded
    {
        get => _isSparklineExpanded;
        set
        {
            if (SetField(ref _isSparklineExpanded, value))
            {
                _settings.IsSparklineExpanded = value;
                HudSettingsManager.Save(_settings, _settingsPath);
            }
        }
    }

    public bool AutoHideEnabled
    {
        get => _autoHideEnabled;
        set
        {
            if (SetField(ref _autoHideEnabled, value))
            {
                _settings.AutoHideEnabled = value;
                HudSettingsManager.Save(_settings, _settingsPath);
            }
        }
    }

    public bool ClickThroughEnabled
    {
        get => _clickThroughEnabled;
        set
        {
            if (SetField(ref _clickThroughEnabled, value))
            {
                _settings.ClickThroughEnabled = value;
                HudSettingsManager.Save(_settings, _settingsPath);
            }
        }
    }

    public bool HotkeysEnabled
    {
        get => _hotkeysEnabled;
        set
        {
            if (SetField(ref _hotkeysEnabled, value))
            {
                _settings.HotkeysEnabled = value;
                HudSettingsManager.Save(_settings, _settingsPath);
            }
        }
    }

    public double? WindowX
    {
        get => _windowX;
        set => SetField(ref _windowX, value);
    }

    public double? WindowY
    {
        get => _windowY;
        set => SetField(ref _windowY, value);
    }

    public string PillGeminiText
    {
        get => _pillGeminiText;
        set => SetField(ref _pillGeminiText, value);
    }

    public string PillClaudeText
    {
        get => _pillClaudeText;
        set => SetField(ref _pillClaudeText, value);
    }

    public string PillCombinedText
    {
        get => _pillCombinedText;
        set => SetField(ref _pillCombinedText, value);
    }

    public string PillGeminiColor
    {
        get => _pillGeminiColor;
        set => SetField(ref _pillGeminiColor, value);
    }

    public string PillClaudeColor
    {
        get => _pillClaudeColor;
        set => SetField(ref _pillClaudeColor, value);
    }

    public string GeminiResetCountdownParentheses
    {
        get => _geminiResetCountdownParentheses;
        set => SetField(ref _geminiResetCountdownParentheses, value);
    }

    public string ClaudeResetCountdownParentheses
    {
        get => _claudeResetCountdownParentheses;
        set => SetField(ref _claudeResetCountdownParentheses, value);
    }

    public string GeminiWeeklyText
    {
        get => _geminiWeeklyText;
        set => SetField(ref _geminiWeeklyText, value);
    }

    public string ClaudeWeeklyText
    {
        get => _claudeWeeklyText;
        set => SetField(ref _claudeWeeklyText, value);
    }

    public string GeminiWeeklyResetText
    {
        get => _geminiWeeklyResetText;
        set => SetField(ref _geminiWeeklyResetText, value);
    }

    public string ClaudeWeeklyResetText
    {
        get => _claudeWeeklyResetText;
        set => SetField(ref _claudeWeeklyResetText, value);
    }

    public string FooterUpdatedText
    {
        get => _footerUpdatedText;
        set => SetField(ref _footerUpdatedText, value);
    }

    public string GeminiTrendArrow
    {
        get => _geminiTrendArrow;
        set => SetField(ref _geminiTrendArrow, value);
    }

    public string GeminiTrendColor
    {
        get => _geminiTrendColor;
        set => SetField(ref _geminiTrendColor, value);
    }

    public string GeminiBurnRateFormatted
    {
        get => _geminiBurnRateFormatted;
        set => SetField(ref _geminiBurnRateFormatted, value);
    }

    public bool HasGeminiBurnRate
    {
        get => _hasGeminiBurnRate;
        set => SetField(ref _hasGeminiBurnRate, value);
    }

    public string ClaudeTrendArrow
    {
        get => _claudeTrendArrow;
        set => SetField(ref _claudeTrendArrow, value);
    }

    public string ClaudeTrendColor
    {
        get => _claudeTrendColor;
        set => SetField(ref _claudeTrendColor, value);
    }

    public string ClaudeBurnRateFormatted
    {
        get => _claudeBurnRateFormatted;
        set => SetField(ref _claudeBurnRateFormatted, value);
    }

    public bool HasClaudeBurnRate
    {
        get => _hasClaudeBurnRate;
        set => SetField(ref _hasClaudeBurnRate, value);
    }

    public string HudGeminiTitle => "Gemini";
    public string HudClaudeTitle => "Claude / GPT";

    public ICommand TogglePillModeCommand { get; }
    public ICommand ToggleSparklineCommand { get; }

    public void SavePosition(double x, double y)
    {
        WindowX = x;
        WindowY = y;
        _settings.X = x;
        _settings.Y = y;
        HudSettingsManager.Save(_settings, _settingsPath);
    }

    public static string GetTrendColor(QuotaTrend trend)
    {
        return trend switch
        {
            QuotaTrend.Falling => "#F5A624", // Amber
            QuotaTrend.Rising => "#34D399",  // Emerald
            _ => "#80FFFFFF"                 // Muted
        };
    }

    public static string GetStatusColor(double percentage)
    {
        if (percentage > 50.0) return "#10B981"; // Emerald / Green
        if (percentage >= 20.0) return "#F59E0B"; // Amber / Yellow
        return "#EF4444"; // Red
    }

    public override void UpdateFromSnapshot(QuotaSnapshot? snapshot)
    {
        base.UpdateFromSnapshot(snapshot);

        if (snapshot == null)
        {
            PillGeminiText = "G --%";
            PillClaudeText = "C --%";
            PillCombinedText = Loc.StatusOffline;
            PillGeminiColor = "#9CA3AF";
            PillClaudeColor = "#9CA3AF";
            GeminiResetCountdownParentheses = "";
            ClaudeResetCountdownParentheses = "";
            GeminiWeeklyText = $"{Loc.WeeklyLimit}: --%";
            ClaudeWeeklyText = $"{Loc.WeeklyLimit}: --%";
            GeminiWeeklyResetText = "";
            ClaudeWeeklyResetText = "";
            FooterUpdatedText = $"{Loc.UpdatedAt} --:--";
            HasGeminiBurnRate = false;
            GeminiTrendArrow = "";
            GeminiBurnRateFormatted = "";
            HasClaudeBurnRate = false;
            ClaudeTrendArrow = "";
            ClaudeBurnRateFormatted = "";
            return;
        }

        PillGeminiColor = GetStatusColor(GeminiFiveHourPct);
        PillClaudeColor = GetStatusColor(ClaudeFiveHourPct);
        GeminiBarColor = PillGeminiColor;
        ClaudeBarColor = PillClaudeColor;

        var gBurn = _history.CalculateBurnRate(QuotaPool.Gemini);
        var cBurn = _history.CalculateBurnRate(QuotaPool.Claude);

        HasGeminiBurnRate = gBurn != null && (gBurn.Trend != QuotaTrend.Steady || gBurn.BurnRatePerHour != 0);
        GeminiTrendArrow = gBurn?.Trend == QuotaTrend.Falling ? "↓" : (gBurn?.Trend == QuotaTrend.Rising ? "↑" : "");
        GeminiTrendColor = GetTrendColor(gBurn?.Trend ?? QuotaTrend.Steady);
        GeminiBurnRateFormatted = gBurn?.Formatted(Loc.IsRussian) ?? "";

        HasClaudeBurnRate = cBurn != null && (cBurn.Trend != QuotaTrend.Steady || cBurn.BurnRatePerHour != 0);
        ClaudeTrendArrow = cBurn?.Trend == QuotaTrend.Falling ? "↓" : (cBurn?.Trend == QuotaTrend.Rising ? "↑" : "");
        ClaudeTrendColor = GetTrendColor(cBurn?.Trend ?? QuotaTrend.Steady);
        ClaudeBurnRateFormatted = cBurn?.Formatted(Loc.IsRussian) ?? "";

        var gReset = snapshot.GeminiGroup?.FiveHourBucket?.TimeUntilReset;
        var gResetStr = gReset.HasValue && gReset.Value > TimeSpan.Zero
            ? $" {Loc.FormatResetCountdown(gReset.Value)}"
            : "";
        PillGeminiText = $"G {GeminiFiveHourPct:F0}%{GeminiTrendArrow}{gResetStr}";
        GeminiResetCountdownParentheses = gReset.HasValue && gReset.Value > TimeSpan.Zero
            ? $"({Loc.FormatResetCountdown(gReset.Value)})"
            : "";

        var cReset = snapshot.ClaudeGroup?.FiveHourBucket?.TimeUntilReset;
        var cResetStr = cReset.HasValue && cReset.Value > TimeSpan.Zero
            ? $" {Loc.FormatResetCountdown(cReset.Value)}"
            : "";
        PillClaudeText = $"C {ClaudeFiveHourPct:F0}%{ClaudeTrendArrow}{cResetStr}";
        ClaudeResetCountdownParentheses = cReset.HasValue && cReset.Value > TimeSpan.Zero
            ? $"({Loc.FormatResetCountdown(cReset.Value)})"
            : "";

        PillCombinedText = $"{PillGeminiText} · {PillClaudeText}";

        // Weekly Limit Lines: "Недельный: 4.3%"
        GeminiWeeklyText = $"{Loc.WeeklyLimit}: {GeminiWeeklyPct.ToString("F1", CultureInfo.InvariantCulture)}%";
        ClaudeWeeklyText = $"{Loc.WeeklyLimit}: {ClaudeWeeklyPct.ToString("F1", CultureInfo.InvariantCulture)}%";

        var gWeekReset = snapshot.GeminiGroup?.WeeklyBucket?.TimeUntilReset;
        GeminiWeeklyResetText = gWeekReset.HasValue && gWeekReset.Value > TimeSpan.Zero
            ? $"↻ {Loc.FormatResetCountdown(gWeekReset.Value)}"
            : "";

        var cWeekReset = snapshot.ClaudeGroup?.WeeklyBucket?.TimeUntilReset;
        ClaudeWeeklyResetText = cWeekReset.HasValue && cWeekReset.Value > TimeSpan.Zero
            ? $"↻ {Loc.FormatResetCountdown(cWeekReset.Value)}"
            : "";

        // Footer: "Обновлено в 17:03"
        FooterUpdatedText = $"{Loc.UpdatedAt} {DateTime.Now:HH:mm}";
    }
}
