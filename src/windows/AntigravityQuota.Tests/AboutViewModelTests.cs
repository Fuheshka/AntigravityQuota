using System;
using System.Collections.Generic;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using AntigravityQuota.Core.ViewModels;
using Xunit;

namespace AntigravityQuota.Tests;

public class AboutViewModelTests
{
    [Fact]
    public void InitialState_ReflectsDefaultPropertiesAndDisconnected()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("ru");

        var vm = new AboutViewModel(loc: loc);

        Assert.Equal("1.2.0", vm.AppVersion);
        Assert.Equal(".NET 9 • Windows x64", vm.PlatformInfo);
        Assert.False(vm.IsConnected);
        Assert.Equal("Не обнаружено (ожидание среды)", vm.ConnectionStatusText);
        Assert.Equal("—", vm.ServerPidText);
        Assert.Equal("—", vm.ServerPortsText);
        Assert.Equal("—", vm.MaskedCsrfText);
        Assert.Equal("Никогда", vm.LastSyncText);
        Assert.Equal("Автор: Даниил К. (Fuheshka)", vm.AuthorshipText);
        Assert.Equal("https://github.com/Fuheshka", vm.AuthorUrl);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota", vm.RepositoryUrl);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota/releases", vm.ReleasesUrl);
        Assert.False(vm.HasCopyFeedback);
        Assert.Empty(vm.CopyStatusFeedback);
    }

    [Fact]
    public void UpdateState_WhenConnected_UpdatesPropertiesAndReport()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("en");

        var vm = new AboutViewModel(loc: loc);

        var endpoint = new ServerEndpoint(5555, "abcdef123456", new[] { 8081, 8080 });
        var now = DateTimeOffset.UtcNow;

        vm.UpdateState(endpoint, snapshot: null, lastUpdate: now);

        Assert.True(vm.IsConnected);
        Assert.Equal("Connected", vm.ConnectionStatusText);
        Assert.Equal("5555", vm.ServerPidText);
        Assert.Equal("8081, 8080", vm.ServerPortsText);
        Assert.Equal("abcd...3456", vm.MaskedCsrfText);
        Assert.NotEqual("Never", vm.LastSyncText);
        Assert.Contains("PID: 5555", vm.DiagnosticReportText);
        Assert.Contains("CSRF Token: abcd...3456", vm.DiagnosticReportText);
    }

    [Fact]
    public void UpdateState_WhenDisconnected_ResetsToDefaults()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("ru");

        var vm = new AboutViewModel(loc: loc);
        var endpoint = new ServerEndpoint(5555, "abcdef123456", new[] { 8081 });
        vm.UpdateState(endpoint, snapshot: null);
        Assert.True(vm.IsConnected);

        vm.UpdateState(null, snapshot: null);

        Assert.False(vm.IsConnected);
        Assert.Equal("Не обнаружено (ожидание среды)", vm.ConnectionStatusText);
        Assert.Equal("—", vm.ServerPidText);
        Assert.Equal("—", vm.ServerPortsText);
        Assert.Equal("—", vm.MaskedCsrfText);
    }

    [Fact]
    public void CopyReportCommand_InvokesClipboardActionAndSetsFeedback()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("ru");

        string? copiedText = null;
        var vm = new AboutViewModel(
            loc: loc,
            clipboardSetter: text => copiedText = text);

        vm.CopyReportCommand.Execute(null);

        Assert.NotNull(copiedText);
        Assert.Contains("=== AntigravityQuota Diagnostic Report ===", copiedText);
        Assert.True(vm.HasCopyFeedback);
        Assert.Equal("Отчет скопирован в буфер обмена", vm.CopyStatusFeedback);
    }

    [Fact]
    public void OpenUrlCommands_InvokeBrowserOpener()
    {
        var openedUrls = new List<string>();
        var vm = new AboutViewModel(urlOpener: url => openedUrls.Add(url));

        vm.OpenGitHubCommand.Execute(null);
        vm.OpenReleasesCommand.Execute(null);
        vm.OpenAuthorCommand.Execute(null);

        Assert.Equal(3, openedUrls.Count);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota", openedUrls[0]);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota/releases", openedUrls[1]);
        Assert.Equal("https://github.com/Fuheshka", openedUrls[2]);
    }

    [Fact]
    public void SetLanguage_UpdatesLocalizedStringsInAboutViewModel()
    {
        var loc = new LocalizationManager();
        loc.SetLanguage("ru");
        var vm = new AboutViewModel(loc: loc);

        Assert.Equal("Автор: Даниил К. (Fuheshka)", vm.AuthorshipText);
        Assert.Equal("Не обнаружено (ожидание среды)", vm.ConnectionStatusText);

        loc.SetLanguage("en");
        vm.RefreshLocalization();

        Assert.Equal("Created by Daniil K. (Fuheshka)", vm.AuthorshipText);
        Assert.Equal("Not detected (waiting for IDE)", vm.ConnectionStatusText);
    }
}
