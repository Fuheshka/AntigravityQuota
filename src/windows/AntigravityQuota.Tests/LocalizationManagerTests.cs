using System.Globalization;
using AntigravityQuota.Core;

namespace AntigravityQuota.Tests;

public class LocalizationManagerTests
{
    [Fact]
    public void SetLanguage_ExplicitRussian_ReturnsRussianStrings()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("ru");

        Assert.True(loc.IsRussian);
        Assert.Equal("ru", loc.CurrentLanguage);
        Assert.Equal("Включить HUD", loc.MenuEnableHUD);
        Assert.Equal("Режим таблетки", loc.MenuCompactPillMode);
        Assert.Equal("Автоскрытие HUD", loc.MenuAutoHideHUD);
        Assert.Equal("Сквозной клик (Alt для перемещения)", loc.MenuClickThrough);
        Assert.Equal("Настройки...", loc.MenuSettings);
        Assert.Equal("О программе...", loc.MenuAbout);
        Assert.Equal("Выход", loc.MenuExit);
        Assert.Equal("Обновить квоты", loc.MenuRefreshNow);
        Assert.Equal("Горячие клавиши", loc.MenuGlobalHotkeys);
        Assert.Equal("Alt+Shift+Q", loc.HotkeyHintToggleHud);
        Assert.Equal("Alt+Shift+M", loc.HotkeyHintTogglePill);
        Assert.Equal("Alt+Shift+R", loc.HotkeyHintRefresh);
        Assert.Equal("Горячие клавиши:", loc.AboutHotkeysTitle);
        Assert.Contains("Alt + Shift + Q", loc.AboutHotkeyToggleHud);
        Assert.Contains("Alt + Shift + M", loc.AboutHotkeyTogglePill);
        Assert.Contains("Alt + Shift + R", loc.AboutHotkeyRefresh);
        Assert.Equal("Пул Gemini (Flash / Pro)", loc.GeminiPoolTitle);
        Assert.Equal("Пул Claude и GPT-OSS", loc.ClaudePoolTitle);
        Assert.Equal("Лимит 5ч", loc.FiveHourLimit);
        Assert.Equal("Недельный", loc.WeeklyLimit);
        Assert.Equal("Автор: Даниил К. (Fuheshka)", loc.AboutAuthor);
        Assert.Equal("Запускать при старте Windows", loc.MenuLaunchAtStartup);
        Assert.Equal("Проверить обновления...", loc.MenuCheckUpdates);
        Assert.Equal("Доступно обновление", loc.UpdateDialogTitleAvailable);
        Assert.Equal("Обновлений не найдено", loc.UpdateDialogTitleUpToDate);
        Assert.Equal("Ошибка проверки обновлений", loc.UpdateDialogTitleFailed);
        Assert.Contains("1.2.0", loc.FormatUpdateAvailableMessage("1.2.0"));
        Assert.Contains("1.0.0", loc.FormatUpToDateMessage("1.0.0"));
        Assert.False(string.IsNullOrWhiteSpace(loc.UpdateDialogFailedMessage));
    }

    [Fact]
    public void SetLanguage_ExplicitEnglish_ReturnsEnglishStrings()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("en");

        Assert.False(loc.IsRussian);
        Assert.Equal("en", loc.CurrentLanguage);
        Assert.Equal("Enable HUD", loc.MenuEnableHUD);
        Assert.Equal("Pill Mode", loc.MenuCompactPillMode);
        Assert.Equal("Auto-Hide HUD", loc.MenuAutoHideHUD);
        Assert.Equal("Click-Through (Alt to move)", loc.MenuClickThrough);
        Assert.Equal("Settings...", loc.MenuSettings);
        Assert.Equal("About...", loc.MenuAbout);
        Assert.Equal("Exit", loc.MenuExit);
        Assert.Equal("Refresh Quotas", loc.MenuRefreshNow);
        Assert.Equal("Global Hotkeys", loc.MenuGlobalHotkeys);
        Assert.Equal("Alt+Shift+Q", loc.HotkeyHintToggleHud);
        Assert.Equal("Alt+Shift+M", loc.HotkeyHintTogglePill);
        Assert.Equal("Alt+Shift+R", loc.HotkeyHintRefresh);
        Assert.Equal("Global Hotkeys:", loc.AboutHotkeysTitle);
        Assert.Contains("Alt + Shift + Q", loc.AboutHotkeyToggleHud);
        Assert.Contains("Alt + Shift + M", loc.AboutHotkeyTogglePill);
        Assert.Contains("Alt + Shift + R", loc.AboutHotkeyRefresh);
        Assert.Equal("Gemini Pool (Flash / Pro)", loc.GeminiPoolTitle);
        Assert.Equal("Claude & GPT-OSS Pool", loc.ClaudePoolTitle);
        Assert.Equal("5h Limit", loc.FiveHourLimit);
        Assert.Equal("Weekly", loc.WeeklyLimit);
        Assert.Equal("Created by Daniil K. (Fuheshka)", loc.AboutAuthor);
        Assert.Equal("Launch at Windows startup", loc.MenuLaunchAtStartup);
        Assert.Equal("Check for Updates...", loc.MenuCheckUpdates);
        Assert.Equal("Update Available", loc.UpdateDialogTitleAvailable);
        Assert.Equal("You're Up to Date", loc.UpdateDialogTitleUpToDate);
        Assert.Equal("Update Check Failed", loc.UpdateDialogTitleFailed);
        Assert.Contains("1.2.0", loc.FormatUpdateAvailableMessage("1.2.0"));
        Assert.Contains("1.0.0", loc.FormatUpToDateMessage("1.0.0"));
        Assert.False(string.IsNullOrWhiteSpace(loc.UpdateDialogFailedMessage));
    }

    [Theory]
    [InlineData("ru-RU", true)]
    [InlineData("ru-BY", true)]
    [InlineData("en-US", false)]
    [InlineData("de-DE", false)]
    public void DetectLanguage_FromCulture_ResolvesCorrectly(string cultureName, bool expectedRussian)
    {
        var culture = new CultureInfo(cultureName);
        var loc = new LocalizationManager(culture);

        Assert.Equal(expectedRussian, loc.IsRussian);
        Assert.Equal(expectedRussian ? "ru" : "en", loc.CurrentLanguage);
    }

    [Fact]
    public void FormatTrayTooltip_FormatsBothPoolPercentages_Correctly()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("en");

        var tooltip = loc.FormatTrayTooltip(85.4, 100.0);
        Assert.Contains("Gemini: 85.4%", tooltip);
        Assert.Contains("Claude: 100.0%", tooltip);

        var nullTooltip = loc.FormatTrayTooltip(null, null);
        Assert.Contains("Offline", nullTooltip);
    }

    [Fact]
    public void FormatTrayTooltip_RussianOffline_FormatsCorrectly()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("ru");

        var nullTooltip = loc.FormatTrayTooltip(null, null);
        Assert.Contains("Оффлайн", nullTooltip);

        var activeTooltip = loc.FormatTrayTooltip(50.0, 75.5);
        Assert.Contains("Gemini: 50.0%", activeTooltip);
        Assert.Contains("Claude: 75.5%", activeTooltip);
    }

    [Fact]
    public void SingletonInstance_DefaultIsAvailable()
    {
        Assert.NotNull(LocalizationManager.Instance);
        Assert.NotNull(LocalizationManager.Instance.AppTitle);
    }
}
