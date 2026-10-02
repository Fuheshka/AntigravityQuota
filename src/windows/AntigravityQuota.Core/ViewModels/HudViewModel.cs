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
            return;
        }

        PillGeminiColor = GetStatusColor(GeminiFiveHourPct);
        PillClaudeColor = GetStatusColor(ClaudeFiveHourPct);
        GeminiBarColor = PillGeminiColor;
        ClaudeBarColor = PillClaudeColor;

        var gReset = snapshot.GeminiGroup?.FiveHourBucket?.TimeUntilReset;
        var gResetStr = gReset.HasValue && gReset.Value > TimeSpan.Zero
            ? $" {Loc.FormatResetCountdown(gReset.Value)}"
            : "";
        PillGeminiText = $"G {GeminiFiveHourPct:F0}%{gResetStr}";

        var cReset = snapshot.ClaudeGroup?.FiveHourBucket?.TimeUntilReset;
        var cResetStr = cReset.HasValue && cReset.Value > TimeSpan.Zero
            ? $" {Loc.FormatResetCountdown(cReset.Value)}"
            : "";
        PillClaudeText = $"C {ClaudeFiveHourPct:F0}%{cResetStr}";

        PillCombinedText = $"{PillGeminiText} · {PillClaudeText}";
    }
}
