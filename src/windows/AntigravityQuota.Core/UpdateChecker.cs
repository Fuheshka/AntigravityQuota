using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AntigravityQuota.Core;

/// <summary>
/// Status of an update check operation.
/// </summary>
public enum UpdateStatus
{
    UpdateAvailable,
    UpToDate,
    Throttled,
    Failed
}

/// <summary>
/// Asset information associated with a GitHub Release.
/// </summary>
public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    public GitHubAsset()
    {
    }

    public GitHubAsset(string name, string browserDownloadUrl)
    {
        Name = name ?? string.Empty;
        BrowserDownloadUrl = browserDownloadUrl ?? string.Empty;
    }
}

/// <summary>
/// Deserialized payload from GitHub Releases API (releases/latest).
/// </summary>
public class GitHubReleaseInfo
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();

    [JsonIgnore]
    public string CleanVersion => TagName?.TrimStart('v', 'V').Trim() ?? string.Empty;
}

/// <summary>
/// The result of an update check.
/// </summary>
public class UpdateCheckResult
{
    public UpdateStatus Status { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string? LatestVersion { get; init; }
    public string? DownloadUrl { get; init; }
    public string? ReleasePageUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? ErrorMessage { get; init; }

    public bool IsUpdateAvailable => Status == UpdateStatus.UpdateAvailable;
    public bool IsUpToDate => Status == UpdateStatus.UpToDate;
    public bool IsThrottled => Status == UpdateStatus.Throttled;
    public bool IsFailed => Status == UpdateStatus.Failed;

    public static UpdateCheckResult Available(
        string latestVersion,
        string downloadUrl,
        string releasePageUrl,
        string currentVersion,
        string? releaseNotes = null) => new()
    {
        Status = UpdateStatus.UpdateAvailable,
        CurrentVersion = currentVersion,
        LatestVersion = latestVersion,
        DownloadUrl = downloadUrl,
        ReleasePageUrl = releasePageUrl,
        ReleaseNotes = releaseNotes
    };

    public static UpdateCheckResult UpToDate(
        string currentVersion,
        string? latestVersion = null,
        string? releasePageUrl = null) => new()
    {
        Status = UpdateStatus.UpToDate,
        CurrentVersion = currentVersion,
        LatestVersion = latestVersion ?? currentVersion,
        ReleasePageUrl = releasePageUrl
    };

    public static UpdateCheckResult Throttled(string currentVersion) => new()
    {
        Status = UpdateStatus.Throttled,
        CurrentVersion = currentVersion
    };

    public static UpdateCheckResult Failed(string errorMessage, string currentVersion) => new()
    {
        Status = UpdateStatus.Failed,
        ErrorMessage = errorMessage,
        CurrentVersion = currentVersion
    };
}

/// <summary>
/// Lightweight, zero-dependency GitHub Release update checker following the app-update-checker standard.
/// Implements 24-hour rate limit protection, SemVer comparison, Windows asset prioritization (.exe -> .msi -> .zip),
/// fail-silent network handling, and disk/injected cache persistence.
/// </summary>
public class UpdateChecker : IDisposable
{
    public const string DefaultRepository = "Fuheshka/AntigravityQuota";
    public const string DefaultVersion = "1.2.0";
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromHours(24);
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Resolves the current running assembly version, falling back to DefaultVersion ("1.2.0").
    /// </summary>
    public static string ResolveCurrentVersion()
    {
        try
        {
            var entry = System.Reflection.Assembly.GetEntryAssembly();
            if (entry != null && entry.GetName().Name?.StartsWith("AntigravityQuota", StringComparison.OrdinalIgnoreCase) == true)
            {
                var ver = entry.GetName().Version;
                if (ver != null && (ver.Major > 0 || ver.Minor > 0 || ver.Build > 0))
                {
                    return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
                }
            }

            var coreVer = typeof(UpdateChecker).Assembly.GetName().Version;
            if (coreVer != null && (coreVer.Major > 0 || coreVer.Minor > 0 || coreVer.Build > 0))
            {
                return $"{coreVer.Major}.{coreVer.Minor}.{Math.Max(0, coreVer.Build)}";
            }
        }
        catch
        {
            // Fail silent fallback
        }
        return DefaultVersion;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private class CacheData
    {
        public DateTimeOffset? LastCheck { get; set; }
    }

    private readonly HttpClient? _httpClient;
    private readonly bool _disposeClient;
    private readonly Func<string, Task<string>>? _httpGetter;
    private readonly string _currentVersion;
    private readonly string _repository;
    private readonly string _cacheFilePath;
    private readonly Func<DateTimeOffset?>? _lastCheckGetter;
    private readonly Action<DateTimeOffset>? _lastCheckSetter;
    private readonly Func<DateTimeOffset> _timeProvider;
    private readonly TimeSpan _cooldown;

    public string CurrentVersion => _currentVersion;
    public string Repository => _repository;
    public string CacheFilePath => _cacheFilePath;
    public TimeSpan Cooldown => _cooldown;

    public UpdateChecker(
        string currentVersion = DefaultVersion,
        string repository = DefaultRepository,
        HttpClient? httpClient = null,
        string? cacheFilePath = null,
        Func<DateTimeOffset?>? lastCheckGetter = null,
        Action<DateTimeOffset>? lastCheckSetter = null,
        Func<DateTimeOffset>? timeProvider = null,
        TimeSpan? cooldown = null,
        Func<string, Task<string>>? httpGetter = null)
    {
        _currentVersion = string.IsNullOrWhiteSpace(currentVersion) ? DefaultVersion : currentVersion.Trim();
        _repository = string.IsNullOrWhiteSpace(repository) ? DefaultRepository : repository.Trim();
        _httpGetter = httpGetter;

        if (httpGetter == null)
        {
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeClient = false;
            }
            else
            {
                _httpClient = new HttpClient();
                _disposeClient = true;
            }
        }
        else
        {
            _httpClient = null;
            _disposeClient = false;
        }

        _cacheFilePath = cacheFilePath ?? GetDefaultCacheFilePath();
        _lastCheckGetter = lastCheckGetter;
        _lastCheckSetter = lastCheckSetter;
        _timeProvider = timeProvider ?? (() => DateTimeOffset.UtcNow);
        _cooldown = cooldown ?? DefaultCooldown;
    }

    public UpdateChecker(
        HttpMessageHandler handler,
        string currentVersion = DefaultVersion,
        string repository = DefaultRepository,
        string? cacheFilePath = null,
        Func<DateTimeOffset?>? lastCheckGetter = null,
        Action<DateTimeOffset>? lastCheckSetter = null,
        Func<DateTimeOffset>? timeProvider = null,
        TimeSpan? cooldown = null)
        : this(currentVersion, repository, new HttpClient(handler), cacheFilePath, lastCheckGetter, lastCheckSetter, timeProvider, cooldown, null)
    {
        _disposeClient = true;
    }

    /// <summary>
    /// Checks for updates against GitHub Releases API.
    /// </summary>
    /// <param name="force">If true, bypasses the 24-hour rate limit cooldown (used for manual menu triggers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UpdateCheckResult describing the outcome.</returns>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider();

        // 1. Cooldown rate limit check
        if (!force)
        {
            var lastCheck = GetLastCheckTime();
            if (lastCheck.HasValue && (now - lastCheck.Value) < _cooldown)
            {
                return UpdateCheckResult.Throttled(_currentVersion);
            }
        }

        string endpoint = $"https://api.github.com/repos/{_repository}/releases/latest";

        // 2. If custom httpGetter is provided, use it directly
        if (_httpGetter != null)
        {
            try
            {
                var json = await _httpGetter(endpoint).ConfigureAwait(false);
                var release = JsonSerializer.Deserialize<GitHubReleaseInfo>(json, JsonOptions);
                if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                {
                    return UpdateCheckResult.Failed("Failed to parse release information from GitHub response.", _currentVersion);
                }

                SaveLastCheckTime(now);

                string latestVersion = release.CleanVersion;
                if (IsNewer(latestVersion, _currentVersion))
                {
                    string downloadUrl = ResolveDownloadUrl(release);
                    return UpdateCheckResult.Available(
                        latestVersion,
                        downloadUrl,
                        release.HtmlUrl,
                        _currentVersion,
                        release.Body);
                }

                return UpdateCheckResult.UpToDate(_currentVersion, latestVersion, release.HtmlUrl);
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Failed(ex.Message, _currentVersion);
            }
        }

        // 3. Otherwise build and send HTTP request via HttpClient
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd($"AntigravityQuota-Windows/{_currentVersion}");

        using var cts = new CancellationTokenSource(DefaultTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

        try
        {
            using var response = await _httpClient!.SendAsync(request, linkedCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed(
                    $"GitHub API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase})",
                    _currentVersion);
            }

            var contentStream = await response.Content.ReadAsStreamAsync(linkedCts.Token).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseInfo>(
                contentStream,
                JsonOptions,
                linkedCts.Token).ConfigureAwait(false);

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return UpdateCheckResult.Failed("Failed to parse release information from GitHub response.", _currentVersion);
            }

            // Successfully received and parsed release - record timestamp
            SaveLastCheckTime(now);

            string latestVersion = release.CleanVersion;
            if (IsNewer(latestVersion, _currentVersion))
            {
                string downloadUrl = ResolveDownloadUrl(release);
                return UpdateCheckResult.Available(
                    latestVersion,
                    downloadUrl,
                    release.HtmlUrl,
                    _currentVersion,
                    release.Body);
            }

            return UpdateCheckResult.UpToDate(_currentVersion, latestVersion, release.HtmlUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Failed("Update check was canceled.", _currentVersion);
        }
        catch (Exception ex)
        {
            // Fail-silent in all error modes (timeout, network offline, DNS error, malformed JSON)
            return UpdateCheckResult.Failed(ex.Message, _currentVersion);
        }
    }

    /// <summary>
    /// Compares two semantic version strings. Returns true if candidate is strictly newer than current.
    /// Handles leading 'v'/'V', variable dot-separated numeric segments (1.10.0 > 1.9.5, 2.0.0 > 1.99.99).
    /// </summary>
    public static bool IsNewer(string? candidate, string? current)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var v1 = ParseVersionSegments(candidate);
        var v2 = ParseVersionSegments(current);

        int maxLen = Math.Max(v1.Length, v2.Length);
        for (int i = 0; i < maxLen; i++)
        {
            int p1 = i < v1.Length ? v1[i] : 0;
            int p2 = i < v2.Length ? v2[i] : 0;
            if (p1 > p2) return true;
            if (p1 < p2) return false;
        }

        return false;
    }

    private static int[] ParseVersionSegments(string? versionStr)
    {
        if (string.IsNullOrWhiteSpace(versionStr))
        {
            return Array.Empty<int>();
        }

        var trimmed = versionStr.Trim().TrimStart('v', 'V').Trim();
        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            int len = 0;
            while (len < part.Length && char.IsDigit(part[len]))
            {
                len++;
            }

            if (len > 0 && int.TryParse(part.AsSpan(0, len), out int num))
            {
                result[i] = num;
            }
            else
            {
                result[i] = 0;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves the optimal download URL for Windows:
    /// Prioritizes .exe -> .msi -> Windows-specific archives (.zip/.7z/.tar.gz).
    /// Filters out foreign OS assets (macOS, Linux, Android) to prevent cross-platform download collisions.
    /// Falls back to release.HtmlUrl if no suitable Windows asset is present.
    /// </summary>
    public static string ResolveDownloadUrl(GitHubReleaseInfo release)
    {
        if (release == null) return string.Empty;

        if (release.Assets != null && release.Assets.Count > 0)
        {
            var validAssets = release.Assets
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
                .ToList();

            static bool IsForeignToWindows(string fileName)
            {
                var lower = fileName.ToLowerInvariant();
                // Foreign OS file extensions
                if (lower.EndsWith(".dmg") || lower.EndsWith(".pkg") ||
                    lower.EndsWith(".appimage") || lower.EndsWith(".deb") ||
                    lower.EndsWith(".rpm") || lower.EndsWith(".apk") || lower.EndsWith(".aab"))
                {
                    return true;
                }
                // Foreign OS keywords
                if (lower.Contains("macos") || lower.Contains("darwin") || lower.Contains("osx") ||
                    lower.Contains("apple") || lower.Contains("linux") || lower.Contains("ubuntu") ||
                    lower.Contains("debian") || lower.Contains("android"))
                {
                    return true;
                }
                // Word-boundary / hyphen-boundary mac indicators ("-mac.", "-mac-", "_mac.", "_mac_")
                if (lower.Contains("-mac.") || lower.Contains("-mac-") ||
                    lower.Contains("_mac.") || lower.Contains("_mac_") ||
                    lower.Contains(".mac."))
                {
                    return true;
                }
                return false;
            }

            var winCandidates = validAssets.Where(a => !IsForeignToWindows(a.Name)).ToList();

            // 1. Prioritize native executable/installer (.exe)
            var exeAsset = winCandidates.FirstOrDefault(a =>
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
            if (exeAsset != null)
            {
                return exeAsset.BrowserDownloadUrl;
            }

            // 2. Prioritize MSI installer (.msi)
            var msiAsset = winCandidates.FirstOrDefault(a =>
                a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
            if (msiAsset != null)
            {
                return msiAsset.BrowserDownloadUrl;
            }

            // 3. Prioritize Windows-specific archives (contains "win" or "windows")
            var winZipAsset = winCandidates.FirstOrDefault(a =>
            {
                var lower = a.Name.ToLowerInvariant();
                if (!lower.EndsWith(".zip") && !lower.EndsWith(".7z") && !lower.EndsWith(".tar.gz"))
                {
                    return false;
                }
                return lower.Contains("win") || lower.Contains("windows");
            });
            if (winZipAsset != null)
            {
                return winZipAsset.BrowserDownloadUrl;
            }

            // 4. Fallback to generic archive (if no OS markers exist at all)
            var genericZipAsset = winCandidates.FirstOrDefault(a =>
                a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (genericZipAsset != null)
            {
                return genericZipAsset.BrowserDownloadUrl;
            }
        }

        return release.HtmlUrl ?? string.Empty;
    }

    /// <summary>
    /// Gets the default location of the update cache file in %APPDATA%\AntigravityQuota\update_cache.json.
    /// </summary>
    public static string GetDefaultCacheFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "AntigravityQuota", "update_cache.json");
    }

    /// <summary>
    /// Gets the timestamp of the last update check from injected delegate or disk cache.
    /// </summary>
    public DateTimeOffset? GetLastCheckTime()
    {
        if (_lastCheckGetter != null)
        {
            return _lastCheckGetter();
        }

        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<CacheData>(json, JsonOptions);
            return cache?.LastCheck;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Persists the timestamp of a successful update check to injected delegate or disk cache.
    /// </summary>
    public void SaveLastCheckTime(DateTimeOffset timestamp)
    {
        if (_lastCheckSetter != null)
        {
            _lastCheckSetter(timestamp);
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(_cacheFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var cache = new CacheData { LastCheck = timestamp };
            var json = JsonSerializer.Serialize(cache, JsonOptions);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch
        {
            // Fail-silent on IO errors
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient?.Dispose();
        }
    }
}
