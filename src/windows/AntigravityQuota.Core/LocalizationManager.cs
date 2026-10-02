using System.Globalization;

namespace AntigravityQuota.Core;

/// <summary>
/// Dual-language (Russian &amp; English) localization manager with system culture detection.
/// Follows app-i18n-localization standard and Ponytail simplicity.
/// </summary>
public class LocalizationManager
{
    private static LocalizationManager? _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    private string _currentLanguage;

    public LocalizationManager(CultureInfo? culture = null)
    {
        var targetCulture = culture ?? CultureInfo.CurrentUICulture;
        _currentLanguage = targetCulture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
    }

    public string CurrentLanguage => _currentLanguage;
    public bool IsRussian => _currentLanguage == "ru";

    public void SetLanguage(string lang)
    {
        _currentLanguage = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
    }

    // App & Header
    public string AppTitle => IsRussian ? "AntigravityQuota • Лимиты моделей" : "AntigravityQuota • Model Limits";
    public string AppShortTitle => "AntigravityQuota";
    public string StatusOnline => IsRussian ? "В сети" : "Online";
    public string StatusOffline => IsRussian ? "Оффлайн" : "Offline";
    public string WaitingForAntigravity => IsRussian ? "Ожидание запуска Antigravity..." : "Waiting for Antigravity...";
    public string UpdatedAt => IsRussian ? "Обновлено в" : "Updated at";

    // Pools & Meters
    public string GeminiPoolTitle => IsRussian ? "Пул Gemini (Flash / Pro)" : "Gemini Pool (Flash / Pro)";
    public string ClaudePoolTitle => IsRussian ? "Пул Claude и GPT-OSS" : "Claude & GPT-OSS Pool";
    public string FiveHourLimit => IsRussian ? "Лимит 5ч" : "5h Limit";
    public string WeeklyLimit => IsRussian ? "Недельный" : "Weekly";
    public string ResetsIn => IsRussian ? "сброс через" : "resets in";
    public string QuotaExhausted => IsRussian ? "Исчерпана" : "Exhausted";
    public string QuotaFull => IsRussian ? "100% Доступно" : "100% Available";

    // Burn Rate & Sparkline
    public string BurnRateTitle => IsRussian ? "Расход токенов" : "Token Burn Rate";
    public string SparklineTitle => IsRussian ? "График расхода (1ч)" : "Burn Rate Chart (1h)";
    public string CollectingData => IsRussian ? "Сбор статистики..." : "Collecting statistics...";

    // Context Menu Items
    public string MenuEnableHUD => IsRussian ? "Включить HUD" : "Enable HUD";
    public string MenuCompactPillMode => IsRussian ? "Режим таблетки" : "Pill Mode";
    public string MenuAutoHideHUD => IsRussian ? "Автоскрытие HUD" : "Auto-Hide HUD";
    public string MenuClickThrough => IsRussian ? "Сквозной клик (Alt для перемещения)" : "Click-Through (Alt to move)";
    public string MenuSettings => IsRussian ? "Настройки..." : "Settings...";
    public string MenuAbout => IsRussian ? "О программе..." : "About...";
    public string MenuExit => IsRussian ? "Выход" : "Exit";
    public string MenuRefreshNow => IsRussian ? "Обновить квоты" : "Refresh Quotas";
    public string MenuGlobalHotkeys => IsRussian ? "Горячие клавиши" : "Global Hotkeys";

    // Hotkey hints and gestures
    public string HotkeyHintToggleHud => "Alt+Shift+Q";
    public string HotkeyHintTogglePill => "Alt+Shift+M";
    public string HotkeyHintRefresh => "Alt+Shift+R";

    // About & Credits
    public string AboutAuthor => IsRussian ? "Автор: Даниил К. (Fuheshka)" : "Created by Daniil K. (Fuheshka)";
    public string AboutDescription => IsRussian 
        ? "Монитор квот и лимитов моделей Google Antigravity в реальном времени" 
        : "Real-time model quota monitor & HUD for Google Antigravity";
    public string AboutHotkeysTitle => IsRussian ? "Горячие клавиши:" : "Global Hotkeys:";
    public string AboutHotkeyToggleHud => IsRussian 
        ? "• Alt + Shift + Q — показать / скрыть виджет HUD" 
        : "• Alt + Shift + Q — Show / hide HUD widget";
    public string AboutHotkeyTogglePill => IsRussian 
        ? "• Alt + Shift + M — переключить режим таблетки и карточки" 
        : "• Alt + Shift + M — Toggle pill and card mode";
    public string AboutHotkeyRefresh => IsRussian 
        ? "• Alt + Shift + R — принудительно обновить квоты" 
        : "• Alt + Shift + R — Force refresh quotas now";
    public string AboutCheckUpdates => IsRussian ? "Проверить обновления..." : "Check for Updates...";

    // Tooltip formatting
    public string FormatTrayTooltip(double? geminiPct, double? claudePct)
    {
        if (!geminiPct.HasValue && !claudePct.HasValue)
        {
            return IsRussian ? "AntigravityQuota\nОффлайн (ожидание среды)" : "AntigravityQuota\nOffline (waiting for IDE)";
        }

        var gText = geminiPct.HasValue ? $"{geminiPct.Value.ToString("F1", CultureInfo.InvariantCulture)}%" : "--%";
        var cText = claudePct.HasValue ? $"{claudePct.Value.ToString("F1", CultureInfo.InvariantCulture)}%" : "--%";

        return $"AntigravityQuota\nGemini: {gText} · Claude: {cText}";
    }

    public string FormatResetCountdown(TimeSpan? remaining)
    {
        if (!remaining.HasValue || remaining.Value <= TimeSpan.Zero)
            return IsRussian ? "готов" : "ready";

        var ts = remaining.Value;
        if (ts.TotalDays >= 1)
        {
            int days = (int)ts.TotalDays;
            int hours = ts.Hours;
            return IsRussian ? $"{days}д {hours}ч" : $"{days}d {hours}h";
        }
        if (ts.TotalHours >= 1)
        {
            int hours = (int)ts.TotalHours;
            int minutes = ts.Minutes;
            return IsRussian ? $"{hours}ч {minutes}м" : $"{hours}h {minutes}m";
        }
        return IsRussian ? $"{ts.Minutes}м" : $"{ts.Minutes}m";
    }
}
