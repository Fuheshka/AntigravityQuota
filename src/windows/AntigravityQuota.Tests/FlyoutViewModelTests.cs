using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using AntigravityQuota.Core.ViewModels;
using Xunit;

namespace AntigravityQuota.Tests;

public class FlyoutViewModelTests
{
    [Fact]
    public void InitialState_ReflectsOfflineAndDefaultValues()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("en");
        var vm = new FlyoutViewModel(loc: loc);

        Assert.False(vm.IsOnline);
        Assert.Equal("Offline", vm.StatusBadgeText);
        Assert.Contains("Offline", vm.TrayTooltipText);
        Assert.Equal(100.0, vm.GeminiFiveHourPct);
        Assert.Equal(100.0, vm.ClaudeFiveHourPct);
        Assert.False(vm.IsRefreshing);
        Assert.False(vm.HasSparklineData);
    }

    [Fact]
    public void UpdateFromSnapshot_NullSnapshot_ResetsToOffline()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("en");
        var vm = new FlyoutViewModel(loc: loc);

        vm.UpdateFromSnapshot(null);

        Assert.False(vm.IsOnline);
        Assert.Equal("Offline", vm.StatusBadgeText);
        Assert.Contains("Offline", vm.TrayTooltipText);
    }

    [Fact]
    public void UpdateFromSnapshot_ValidSnapshot_UpdatesPercentagesAndTooltip()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("en");
        var vm = new FlyoutViewModel(loc: loc);

        var now = DateTimeOffset.UtcNow;
        var geminiBucket5h = new QuotaBucket("gemini-5h", "5h", 0.854, now.AddHours(2));
        var geminiBucketWeek = new QuotaBucket("gemini-weekly", "weekly", 0.95, now.AddDays(4));
        var geminiGroup = new QuotaGroup("Gemini Models", "Gemini", new[] { geminiBucket5h, geminiBucketWeek });

        var claudeBucket5h = new QuotaBucket("claude-5h", "5h", 0.60, now.AddMinutes(45));
        var claudeBucketWeek = new QuotaBucket("claude-weekly", "weekly", 0.70, now.AddDays(2));
        var claudeGroup = new QuotaGroup("Claude Models", "Claude", new[] { claudeBucket5h, claudeBucketWeek });

        var snapshot = new QuotaSnapshot(new[] { geminiGroup, claudeGroup }, Array.Empty<ModelConfig>(), now);

        vm.UpdateFromSnapshot(snapshot);

        Assert.True(vm.IsOnline);
        Assert.Equal("Online", vm.StatusBadgeText);
        Assert.Equal(85.4, Math.Round(vm.GeminiFiveHourPct, 1));
        Assert.Equal(60.0, Math.Round(vm.ClaudeFiveHourPct, 1));
        Assert.Contains("Gemini: 85.4%", vm.TrayTooltipText);
        Assert.Contains("Claude: 60.0%", vm.TrayTooltipText);
        Assert.Contains(loc.ResetsIn, vm.GeminiCountdownText);
        Assert.Contains(loc.ResetsIn, vm.ClaudeCountdownText);
    }

    [Fact]
    public void UpdateFromSnapshot_ConsecutiveSamples_GeneratesSparklinePath()
    {
        var history = new QuotaHistoryTracker();
        var vm = new FlyoutViewModel(history: history);

        var t0 = DateTimeOffset.UtcNow.AddMinutes(-30);
        var t1 = DateTimeOffset.UtcNow;

        var snap0 = new QuotaSnapshot(new[]
        {
            new QuotaGroup("Gemini", "", new[] { new QuotaBucket(1.0) }),
            new QuotaGroup("Claude", "", new[] { new QuotaBucket(1.0) })
        }, Array.Empty<ModelConfig>(), t0);

        var snap1 = new QuotaSnapshot(new[]
        {
            new QuotaGroup("Gemini", "", new[] { new QuotaBucket(0.7) }),
            new QuotaGroup("Claude", "", new[] { new QuotaBucket(0.8) })
        }, Array.Empty<ModelConfig>(), t1);

        vm.UpdateFromSnapshot(snap0);
        vm.UpdateFromSnapshot(snap1);

        Assert.True(vm.HasSparklineData);
        Assert.StartsWith("M", vm.GeminiSparklinePath);
        Assert.StartsWith("M", vm.ClaudeSparklinePath);
    }

    [Fact]
    public async Task RefreshCommand_InvokesCallback_AndTogglesIsRefreshing()
    {
        bool callbackInvoked = false;
        var vm = new FlyoutViewModel(triggerRefreshCallback: () =>
        {
            callbackInvoked = true;
            return Task.CompletedTask;
        });

        Assert.True(vm.RefreshCommand.CanExecute(null));
        await vm.RefreshAsync();

        Assert.True(callbackInvoked);
        Assert.False(vm.IsRefreshing);
    }
}
