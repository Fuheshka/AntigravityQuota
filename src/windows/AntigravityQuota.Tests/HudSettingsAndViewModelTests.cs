using System.Text.Json;
using System.Windows.Input;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using AntigravityQuota.Core.ViewModels;
using Xunit;

namespace AntigravityQuota.Tests;

public class HudSettingsAndViewModelTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _tempSettingsFile;

    public HudSettingsAndViewModelTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AntigravityQuotaTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _tempSettingsFile = Path.Combine(_tempDirectory, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public void HudSettings_DefaultValues_AreSensible()
    {
        var settings = new HudSettings();

        Assert.Null(settings.X);
        Assert.Null(settings.Y);
        Assert.True(settings.IsEnabled);
        Assert.False(settings.IsPillMode);
        Assert.False(settings.IsSparklineExpanded);
        Assert.True(settings.AutoHideEnabled);
        Assert.True(settings.ClickThroughEnabled);
    }

    [Fact]
    public void HudSettingsManager_Load_NonExistentFile_ReturnsDefaults()
    {
        var nonExistentPath = Path.Combine(_tempDirectory, "non_existent.json");
        var settings = HudSettingsManager.Load(nonExistentPath);

        Assert.NotNull(settings);
        Assert.Null(settings.X);
        Assert.Null(settings.Y);
        Assert.True(settings.IsEnabled);
        Assert.False(settings.IsPillMode);
        Assert.True(settings.AutoHideEnabled);
        Assert.True(settings.ClickThroughEnabled);
    }

    [Fact]
    public void HudSettingsManager_SaveAndLoad_PreservesAllFields()
    {
        var original = new HudSettings
        {
            X = 250.5,
            Y = 480.0,
            IsEnabled = false,
            IsPillMode = true,
            IsSparklineExpanded = true,
            AutoHideEnabled = false,
            ClickThroughEnabled = false
        };

        HudSettingsManager.Save(original, _tempSettingsFile);
        Assert.True(File.Exists(_tempSettingsFile));

        var loaded = HudSettingsManager.Load(_tempSettingsFile);

        Assert.NotNull(loaded);
        Assert.Equal(250.5, loaded.X);
        Assert.Equal(480.0, loaded.Y);
        Assert.False(loaded.IsEnabled);
        Assert.True(loaded.IsPillMode);
        Assert.True(loaded.IsSparklineExpanded);
        Assert.False(loaded.AutoHideEnabled);
        Assert.False(loaded.ClickThroughEnabled);
    }

    [Fact]
    public void HudSettingsManager_Load_CorruptedJson_FallsBackToDefaults()
    {
        File.WriteAllText(_tempSettingsFile, "{ broken json content !!! @@");

        var settings = HudSettingsManager.Load(_tempSettingsFile);

        Assert.NotNull(settings);
        Assert.True(settings.IsEnabled);
        Assert.False(settings.IsPillMode);
    }

    [Fact]
    public void HudSettingsManager_ClampPosition_KeepsCoordinatesWithinScreenBounds()
    {
        // Monitor bounds: Left=0, Top=0, Right=1920, Bottom=1080
        var clamped = HudSettingsManager.ClampPosition(
            x: -50,
            y: 1200,
            windowWidth: 320,
            windowHeight: 400,
            screenLeft: 0,
            screenTop: 0,
            screenWidth: 1920,
            screenHeight: 1080);

        Assert.Equal(0, clamped.X);
        Assert.Equal(680, clamped.Y); // 1080 - 400
    }

    [Fact]
    public void HudViewModel_InitialState_MatchesSettings()
    {
        var settings = new HudSettings
        {
            X = 100,
            Y = 200,
            IsPillMode = true,
            IsSparklineExpanded = false
        };

        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.True(vm.IsPillMode);
        Assert.False(vm.IsSparklineExpanded);
        Assert.Equal(100, vm.WindowX);
        Assert.Equal(200, vm.WindowY);
    }

    [Fact]
    public void HudViewModel_TogglePillMode_TogglesStateAndPersists()
    {
        var settings = new HudSettings { IsPillMode = false };
        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.False(vm.IsPillMode);

        vm.TogglePillModeCommand.Execute(null);

        Assert.True(vm.IsPillMode);
        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.True(loaded.IsPillMode);

        vm.TogglePillModeCommand.Execute(null);

        Assert.False(vm.IsPillMode);
        loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.False(loaded.IsPillMode);
    }

    [Fact]
    public void HudViewModel_SavePosition_UpdatesViewModelAndSettings()
    {
        var vm = new HudViewModel(settingsPath: _tempSettingsFile);

        vm.SavePosition(450.0, 320.0);

        Assert.Equal(450.0, vm.WindowX);
        Assert.Equal(320.0, vm.WindowY);

        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.Equal(450.0, loaded.X);
        Assert.Equal(320.0, loaded.Y);
    }

    [Fact]
    public void HudViewModel_UpdateFromSnapshot_FormatsPillTextAndColors()
    {
        var vm = new HudViewModel(settingsPath: _tempSettingsFile);

        var g5h = new QuotaBucket(0.85, DateTimeOffset.UtcNow.AddHours(1).AddMinutes(12));
        var gWeek = new QuotaBucket(0.95, DateTimeOffset.UtcNow.AddDays(5));
        var geminiGroup = new QuotaGroup("gemini", new[] { g5h, gWeek });

        var c5h = new QuotaBucket(1.0, null);
        var cWeek = new QuotaBucket(1.0, null);
        var claudeGroup = new QuotaGroup("claude", new[] { c5h, cWeek });

        var snapshot = new QuotaSnapshot(new[] { geminiGroup, claudeGroup }, Array.Empty<ModelConfig>());

        vm.UpdateFromSnapshot(snapshot);

        // Check Pill format: G 85% 1h 12m, C 100%
        Assert.Contains("G 85%", vm.PillGeminiText);
        Assert.Contains("C 100%", vm.PillClaudeText);

        // Status color: >50% is green (#10B981)
        Assert.Equal("#10B981", vm.PillGeminiColor);
        Assert.Equal("#10B981", vm.PillClaudeColor);
    }

    [Fact]
    public void HudViewModel_PillColor_ChangesByPercentageThresholds()
    {
        Assert.Equal("#10B981", HudViewModel.GetStatusColor(80.0)); // > 50%
        Assert.Equal("#F59E0B", HudViewModel.GetStatusColor(35.0)); // 20%..50%
        Assert.Equal("#EF4444", HudViewModel.GetStatusColor(15.0)); // < 20%
    }

    [Fact]
    public void HudViewModel_IsEnabled_TogglesAndPersists()
    {
        var settings = new HudSettings { IsEnabled = true };
        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.True(vm.IsEnabled);
        vm.IsEnabled = false;

        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.False(loaded.IsEnabled);
    }

    [Fact]
    public void HudViewModel_ToggleSparkline_TogglesAndPersists()
    {
        var settings = new HudSettings { IsSparklineExpanded = false };
        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.False(vm.IsSparklineExpanded);
        vm.ToggleSparklineCommand.Execute(null);

        Assert.True(vm.IsSparklineExpanded);
        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.True(loaded.IsSparklineExpanded);
    }

    [Fact]
    public void HudViewModel_UpdateFromSnapshot_NullSnapshot_SetsOfflineState()
    {
        var vm = new HudViewModel(settingsPath: _tempSettingsFile);
        vm.UpdateFromSnapshot(null);

        Assert.False(vm.IsOnline);
        Assert.Equal("G --%", vm.PillGeminiText);
        Assert.Equal("C --%", vm.PillClaudeText);
        Assert.Equal("#9CA3AF", vm.PillGeminiColor);
        Assert.Equal("#9CA3AF", vm.PillClaudeColor);
    }

    [Fact]
    public void HudViewModel_AutoHideEnabled_TogglesAndPersists()
    {
        var settings = new HudSettings { AutoHideEnabled = true };
        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.True(vm.AutoHideEnabled);
        vm.AutoHideEnabled = false;

        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.False(loaded.AutoHideEnabled);
    }

    [Fact]
    public void HudViewModel_ClickThroughEnabled_TogglesAndPersists()
    {
        var settings = new HudSettings { ClickThroughEnabled = true };
        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.True(vm.ClickThroughEnabled);
        vm.ClickThroughEnabled = false;

        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.False(loaded.ClickThroughEnabled);
    }

    [Fact]
    public void HudViewModel_HotkeysEnabled_TogglesAndPersists()
    {
        var settings = new HudSettings { HotkeysEnabled = true };
        var vm = new HudViewModel(settings: settings, settingsPath: _tempSettingsFile);

        Assert.True(vm.HotkeysEnabled);
        vm.HotkeysEnabled = false;

        var loaded = HudSettingsManager.Load(_tempSettingsFile);
        Assert.False(loaded.HotkeysEnabled);
    }
}
