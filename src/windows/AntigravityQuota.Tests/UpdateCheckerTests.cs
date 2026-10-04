using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AntigravityQuota.Core;
using Xunit;

namespace AntigravityQuota.Tests;

public class UpdateCheckerTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;
        public List<HttpRequestMessage> CapturedRequests { get; } = new();

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            return Task.FromResult(_responseFactory(request));
        }
    }

    private class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> CapturedRequests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequests.Add(request);
            await Task.Delay(10000, cancellationToken);
            throw new TaskCanceledException("Simulated timeout");
        }
    }

    private static string CreateReleaseJson(
        string tagName = "v1.2.0",
        string htmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
        string body = "Release notes for v1.2.0",
        params (string name, string url)[] assets)
    {
        var assetList = new List<object>();
        foreach (var (name, url) in assets)
        {
            assetList.Add(new
            {
                name = name,
                browser_download_url = url
            });
        }

        var releaseObj = new
        {
            tag_name = tagName,
            html_url = htmlUrl,
            body = body,
            published_at = "2026-10-02T22:00:00Z",
            assets = assetList
        };

        return JsonSerializer.Serialize(releaseObj);
    }

    #region SemVer Tests

    [Theory]
    [InlineData("v1.2.0", "1.1.9", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("2.0.0", "1.99.99", true)]
    [InlineData("1.9.5", "1.10.0", false)]
    [InlineData("1.10.0", "1.9.5", true)]
    [InlineData("v2.1.0", "v2.0.9", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("1.0.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0.1", false)]
    [InlineData("1.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0", true)]
    [InlineData("v1.5.0-beta", "1.4.9", true)]
    [InlineData("", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    [InlineData("1.0.0", "", true)]
    [InlineData("1.0.0", null, true)]
    public void IsNewer_CorrectlyComparesVersions(string? candidate, string? current, bool expected)
    {
        bool result = UpdateChecker.IsNewer(candidate, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void DefaultVersion_MatchesCurrentRelease()
    {
        Assert.Equal("1.2.0", UpdateChecker.DefaultVersion);
        using var checker = new UpdateChecker();
        Assert.Equal("1.2.0", checker.CurrentVersion);
    }

    #endregion

    #region Asset Prioritization Tests

    [Fact]
    public void ResolveDownloadUrl_PicksExeOverZip()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.0",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            Assets = new List<GitHubAsset>
            {
                new("AntigravityQuota.zip", "https://github.com/releases/download/v1.2.0/AntigravityQuota.zip"),
                new("AntigravityQuota-Setup.exe", "https://github.com/releases/download/v1.2.0/AntigravityQuota-Setup.exe")
            }
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/releases/download/v1.2.0/AntigravityQuota-Setup.exe", url);
    }

    [Fact]
    public void ResolveDownloadUrl_PicksZip_WhenExeIsAbsent()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.0",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            Assets = new List<GitHubAsset>
            {
                new("AntigravityQuota.dmg", "https://github.com/releases/download/v1.2.0/AntigravityQuota.dmg"),
                new("AntigravityQuota-win.zip", "https://github.com/releases/download/v1.2.0/AntigravityQuota-win.zip")
            }
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/releases/download/v1.2.0/AntigravityQuota-win.zip", url);
    }

    [Fact]
    public void ResolveDownloadUrl_IgnoresMacOsZip_AndPicksWindowsZip_InMultiplatformRelease()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.0",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            Assets = new List<GitHubAsset>
            {
                new("AntigravityQuota-v1.2.0-macOS.dmg", "https://github.com/releases/download/v1.2.0/AntigravityQuota-v1.2.0-macOS.dmg"),
                new("AntigravityQuota-v1.2.0-macOS.zip", "https://github.com/releases/download/v1.2.0/AntigravityQuota-v1.2.0-macOS.zip"),
                new("AntigravityQuota-v1.2.0-windows-x64.zip", "https://github.com/releases/download/v1.2.0/AntigravityQuota-v1.2.0-windows-x64.zip")
            }
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/releases/download/v1.2.0/AntigravityQuota-v1.2.0-windows-x64.zip", url);
    }

    [Fact]
    public void ResolveDownloadUrl_WhenOnlyMacOsAssetsExist_FallsBackToReleasePageUrl()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.1",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.1",
            Assets = new List<GitHubAsset>
            {
                new("AntigravityQuota-v1.2.1-macOS.dmg", "https://github.com/releases/download/v1.2.1/AntigravityQuota-v1.2.1-macOS.dmg"),
                new("AntigravityQuota-v1.2.1-macOS.zip", "https://github.com/releases/download/v1.2.1/AntigravityQuota-v1.2.1-macOS.zip")
            }
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.1", url);
    }

    [Fact]
    public void ResolveDownloadUrl_PicksMsi_WhenExeAbsentAndMsiPresent()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.0",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            Assets = new List<GitHubAsset>
            {
                new("AntigravityQuota-Setup.msi", "https://github.com/releases/download/v1.2.0/AntigravityQuota-Setup.msi"),
                new("AntigravityQuota.zip", "https://github.com/releases/download/v1.2.0/AntigravityQuota.zip")
            }
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/releases/download/v1.2.0/AntigravityQuota-Setup.msi", url);
    }

    [Fact]
    public void ResolveDownloadUrl_FallsBackToHtmlUrl_WhenNoMatchingAssetsFound()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.0",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            Assets = new List<GitHubAsset>
            {
                new("AntigravityQuota.dmg", "https://github.com/releases/download/v1.2.0/AntigravityQuota.dmg"),
                new("AntigravityQuota.tar.gz", "https://github.com/releases/download/v1.2.0/AntigravityQuota.tar.gz")
            }
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0", url);
    }

    [Fact]
    public void ResolveDownloadUrl_HandlesEmptyAssets_ByReturningHtmlUrl()
    {
        var release = new GitHubReleaseInfo
        {
            TagName = "v1.2.0",
            HtmlUrl = "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            Assets = new List<GitHubAsset>()
        };

        var url = UpdateChecker.ResolveDownloadUrl(release);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0", url);
    }

    #endregion

    #region Throttling & Cooldown Tests

    [Fact]
    public async Task CheckForUpdatesAsync_WhenWithinCooldown_ReturnsThrottled_AndSkipsNetwork()
    {
        var now = DateTimeOffset.UtcNow;
        var lastCheck = now.AddHours(-5); // 5 hours ago (< 24h)

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => lastCheck,
            lastCheckSetter: _ => { },
            timeProvider: () => now);

        var result = await checker.CheckForUpdatesAsync(force: false);

        Assert.Equal(UpdateStatus.Throttled, result.Status);
        Assert.True(result.IsThrottled);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Empty(handler.CapturedRequests);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenCooldownExpired_PerformsCheck()
    {
        var now = DateTimeOffset.UtcNow;
        var lastCheck = now.AddHours(-25); // 25 hours ago (> 24h)

        string json = CreateReleaseJson("v1.1.0", assets: ("setup.exe", "https://github.com/download/setup.exe"));
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => lastCheck,
            lastCheckSetter: _ => { },
            timeProvider: () => now);

        var result = await checker.CheckForUpdatesAsync(force: false);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Single(handler.CapturedRequests);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenForceTrue_BypassesCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var lastCheck = now.AddMinutes(-5); // Checked 5 mins ago

        string json = CreateReleaseJson("v1.0.0");
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => lastCheck,
            lastCheckSetter: _ => { },
            timeProvider: () => now);

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Single(handler.CapturedRequests);
    }

    #endregion

    #region Response Parsing & Update Available / UpToDate Tests

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerVersionFound_ReturnsUpdateAvailableWithCorrectDetails()
    {
        string json = CreateReleaseJson(
            tagName: "v1.2.0",
            htmlUrl: "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            body: "Great new features and fixes",
            assets: ("AntigravityQuota-Setup.exe", "https://github.com/download/AntigravityQuota-Setup.exe"));

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("1.2.0", result.LatestVersion);
        Assert.Equal("https://github.com/download/AntigravityQuota-Setup.exe", result.DownloadUrl);
        Assert.Equal("https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0", result.ReleasePageUrl);
        Assert.Equal("Great new features and fixes", result.ReleaseNotes);

        // Verify request headers
        var captured = Assert.Single(handler.CapturedRequests);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal("https://api.github.com/repos/Fuheshka/AntigravityQuota/releases/latest", captured.RequestUri?.ToString());
        Assert.Equal("application/vnd.github.v3+json", captured.Headers.Accept.ToString());
        Assert.Equal("AntigravityQuota-Windows/1.0.0", captured.Headers.UserAgent.ToString());
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenCurrentVersionIsLatest_ReturnsUpToDate()
    {
        string json = CreateReleaseJson("v1.0.0");
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.True(result.IsUpToDate);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("1.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenCurrentVersionIsNewerThanRelease_ReturnsUpToDate()
    {
        string json = CreateReleaseJson("v0.9.5");
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.True(result.IsUpToDate);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("0.9.5", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithCustomHttpGetter_ReturnsUpdateAvailable()
    {
        string json = CreateReleaseJson("v1.5.0", assets: ("AntigravityQuota-Setup.exe", "https://download.exe"));
        string? requestedUrl = null;

        using var checker = new UpdateChecker(
            currentVersion: "1.0.0",
            httpGetter: url =>
            {
                requestedUrl = url;
                return Task.FromResult(json);
            },
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal("https://api.github.com/repos/Fuheshka/AntigravityQuota/releases/latest", requestedUrl);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.5.0", result.LatestVersion);
        Assert.Equal("https://download.exe", result.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithCustomHttpGetter_WhenThrows_FailsSilently()
    {
        using var checker = new UpdateChecker(
            currentVersion: "1.0.0",
            httpGetter: _ => throw new InvalidOperationException("Network mock crashed"),
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.True(result.IsFailed);
        Assert.Equal("Network mock crashed", result.ErrorMessage);
    }

    #endregion

    #region Error Handling & Fail-Silent Tests

    [Fact]
    public async Task CheckForUpdatesAsync_WhenServerReturns500_FailsSilently()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = "Internal Server Error"
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.True(result.IsFailed);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenRateLimited403_FailsSilently()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            ReasonPhrase = "rate limit exceeded"
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenJsonIsMalformed_FailsSilently()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Invalid JSON content {{{")
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenTimeoutOccurs_FailsSilently()
    {
        var handler = new TimeoutHttpMessageHandler();

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: _ => { });

        // Using a short timeout via cancellation token
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var result = await checker.CheckForUpdatesAsync(force: true, cancellationToken: cts.Token);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.True(result.IsFailed);
    }

    #endregion

    #region Cache Persistence Tests

    [Fact]
    public async Task CheckForUpdatesAsync_OnSuccessfulCheck_UpdatesTimestamp()
    {
        DateTimeOffset? capturedTimestamp = null;
        var now = new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero);

        string json = CreateReleaseJson("v1.0.0");
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        });

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: ts => capturedTimestamp = ts,
            timeProvider: () => now);

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Equal(now, capturedTimestamp);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_OnFailedCheck_DoesNotUpdateTimestamp()
    {
        DateTimeOffset? capturedTimestamp = null;
        var now = new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero);

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        using var checker = new UpdateChecker(
            handler: handler,
            currentVersion: "1.0.0",
            lastCheckGetter: () => null,
            lastCheckSetter: ts => capturedTimestamp = ts,
            timeProvider: () => now);

        var result = await checker.CheckForUpdatesAsync(force: true);

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Null(capturedTimestamp);
    }

    [Fact]
    public void FileBasedCache_SavesAndLoadsTimestampCorrectly()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"antigravity_update_test_{Guid.NewGuid():N}.json");
        try
        {
            var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateReleaseJson("v1.0.0"))
            });

            var checkTime = new DateTimeOffset(2026, 10, 2, 22, 15, 0, TimeSpan.Zero);

            using (var checker = new UpdateChecker(
                handler: handler,
                currentVersion: "1.0.0",
                cacheFilePath: tempFile,
                timeProvider: () => checkTime))
            {
                Assert.Null(checker.GetLastCheckTime());
                checker.SaveLastCheckTime(checkTime);
                Assert.Equal(checkTime, checker.GetLastCheckTime());
            }

            Assert.True(File.Exists(tempFile));

            // Create a second instance pointing to the same file
            using (var checker2 = new UpdateChecker(
                handler: handler,
                currentVersion: "1.0.0",
                cacheFilePath: tempFile))
            {
                Assert.Equal(checkTime, checker2.GetLastCheckTime());
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion
}
