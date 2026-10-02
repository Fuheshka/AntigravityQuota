using System;
using System.Collections.Generic;
using AntigravityQuota.Core;
using Xunit;

namespace AntigravityQuota.Tests;

public class StartupManagerTests
{
    private class FakeRegistryAccessor : IRegistryAccessor
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool ThrowOnGet { get; set; }
        public bool ThrowOnSet { get; set; }
        public bool ThrowOnDelete { get; set; }

        public string? GetValue(string keyPath, string valueName)
        {
            if (ThrowOnGet) throw new UnauthorizedAccessException("Registry read access denied.");
            return Values.TryGetValue($"{keyPath}\\{valueName}", out var val) ? val : null;
        }

        public void SetValue(string keyPath, string valueName, string value)
        {
            if (ThrowOnSet) throw new UnauthorizedAccessException("Registry write access denied.");
            Values[$"{keyPath}\\{valueName}"] = value;
        }

        public void DeleteValue(string keyPath, string valueName)
        {
            if (ThrowOnDelete) throw new UnauthorizedAccessException("Registry delete access denied.");
            Values.Remove($"{keyPath}\\{valueName}");
        }
    }

    [Fact]
    public void Constants_ShouldHaveExpectedWindowsRunKeyAndAppName()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", StartupManager.RunRegistrySubKey);
        Assert.Equal("AntigravityQuota", StartupManager.DefaultValueName);
        Assert.Equal(StartupManager.RunRegistrySubKey, StartupManager.RegistryKeyPath);
        Assert.Equal(StartupManager.DefaultValueName, StartupManager.ValueName);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenRegistryValueIsNull()
    {
        var manager = new StartupManager(
            getValue: _ => null,
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe");

        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenRegistryValueIsEmptyOrWhitespace()
    {
        var manager = new StartupManager(
            getValue: _ => "   ",
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe");

        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenRegistryValuePointsToDifferentExecutable()
    {
        var manager = new StartupManager(
            getValue: _ => "\"C:\\OtherApp\\other.exe\"",
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe");

        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenRegistryKeyContainsMatchingQuotedPath()
    {
        const string path = @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe";
        var manager = new StartupManager(
            getValue: _ => $"\"{path}\"",
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => path);

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenRegistryKeyContainsMatchingUnquotedPath()
    {
        const string path = @"C:\Tools\AntigravityQuota\AntigravityQuota.exe";
        var manager = new StartupManager(
            getValue: _ => path,
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => path);

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenCasingOrSlashesDiffer()
    {
        var manager = new StartupManager(
            getValue: _ => "\"C:/program files/antigravityquota/antigravityquota.exe\"",
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe");

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenRegistryHasLaunchArguments()
    {
        const string path = @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe";
        var manager = new StartupManager(
            getValue: _ => $"\"{path}\" --minimized --autostart",
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => path);

        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void IsEnabled_SupportsCustomExecutablePathArgument()
    {
        var manager = new StartupManager(
            getValue: _ => "\"D:\\CustomDir\\MyRunner.exe\"",
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\Default\Default.exe");

        Assert.False(manager.IsEnabled());
        Assert.True(manager.IsEnabled(@"D:\CustomDir\MyRunner.exe"));
    }

    [Fact]
    public void SetEnabled_True_CreatesRegistryValueWithQuotedPathWhenSpacesExist()
    {
        string? storedName = null;
        string? storedValue = null;

        const string exeWithSpaces = @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe";
        var manager = new StartupManager(
            getValue: _ => storedValue,
            setValue: (name, val) =>
            {
                storedName = name;
                storedValue = val;
            },
            deleteValue: _ => { storedValue = null; },
            processPathResolver: () => exeWithSpaces);

        var result = manager.SetEnabled(true);

        Assert.True(result);
        Assert.Equal("AntigravityQuota", storedName);
        Assert.Equal($"\"{exeWithSpaces}\"", storedValue);
        Assert.True(manager.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_UsesCustomExecutablePathWhenProvided()
    {
        string? storedValue = null;
        var manager = new StartupManager(
            getValue: _ => storedValue,
            setValue: (_, val) => { storedValue = val; },
            deleteValue: _ => { storedValue = null; },
            processPathResolver: () => @"C:\Default\App.exe");

        const string customPath = @"C:\Custom Path\CustomApp.exe";
        var result = manager.SetEnabled(true, customPath);

        Assert.True(result);
        Assert.Equal($"\"{customPath}\"", storedValue);
        Assert.True(manager.IsEnabled(customPath));
    }

    [Fact]
    public void SetEnabled_False_DeletesRegistryValue()
    {
        bool deleted = false;
        string? storedValue = "\"C:\\Program Files\\AntigravityQuota\\AntigravityQuota.exe\"";

        var manager = new StartupManager(
            getValue: _ => storedValue,
            setValue: (_, val) => { storedValue = val; },
            deleteValue: name =>
            {
                if (name == "AntigravityQuota")
                {
                    deleted = true;
                    storedValue = null;
                }
            },
            processPathResolver: () => @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe");

        var result = manager.SetEnabled(false);

        Assert.True(result);
        Assert.True(deleted);
        Assert.Null(storedValue);
        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void HandlesPathsWithSpacesCorrectly_QuotesWrapping()
    {
        var manager = new StartupManager(
            getValue: _ => null,
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\Program Files\Antigravity Quota\Antigravity Quota.exe");

        // Path with spaces should get wrapped in double quotes
        var resolved = manager.GetExecutablePath();
        Assert.Equal("\"C:\\Program Files\\Antigravity Quota\\Antigravity Quota.exe\"", resolved);

        // Path without spaces does not need quotes
        var noSpaces = manager.GetExecutablePath(@"C:\Tools\AntigravityQuota.exe");
        Assert.Equal(@"C:\Tools\AntigravityQuota.exe", noSpaces);

        // Already quoted path with spaces remains cleanly quoted once
        var alreadyQuoted = manager.GetExecutablePath("\"C:\\Program Files\\App.exe\"");
        Assert.Equal("\"C:\\Program Files\\App.exe\"", alreadyQuoted);

        // Already quoted path without spaces remains cleanly quoted once
        var quotedNoSpaces = manager.GetExecutablePath("\"C:\\Tools\\App.exe\"");
        Assert.Equal("\"C:\\Tools\\App.exe\"", quotedNoSpaces);
    }

    [Fact]
    public void HandlesErrorsGracefully_ReturnsFalseInsteadOfCrashing()
    {
        // 1. GetValue throws UnauthorizedAccessException
        var managerGetError = new StartupManager(
            getValue: _ => throw new UnauthorizedAccessException("Access denied"),
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => @"C:\App\app.exe");

        Assert.False(managerGetError.IsEnabled());

        // 2. SetValue throws UnauthorizedAccessException
        var managerSetError = new StartupManager(
            getValue: _ => null,
            setValue: (_, _) => throw new UnauthorizedAccessException("Access denied"),
            deleteValue: _ => { },
            processPathResolver: () => @"C:\App\app.exe");

        var setResult = managerSetError.SetEnabled(true);
        Assert.False(setResult);

        // 3. DeleteValue throws UnauthorizedAccessException
        var managerDeleteError = new StartupManager(
            getValue: _ => null,
            setValue: (_, _) => { },
            deleteValue: _ => throw new UnauthorizedAccessException("Access denied"),
            processPathResolver: () => @"C:\App\app.exe");

        var deleteResult = managerDeleteError.SetEnabled(false);
        Assert.False(deleteResult);
    }

    [Fact]
    public void SetEnabled_ReturnsFalse_WhenResolvedExecutablePathIsEmpty()
    {
        var manager = new StartupManager(
            getValue: _ => null,
            setValue: (_, _) => { },
            deleteValue: _ => { },
            processPathResolver: () => string.Empty);

        var result = manager.SetEnabled(true);
        Assert.False(result);
    }

    [Fact]
    public void WorksWithIRegistryAccessor_ProviderInjection()
    {
        var fakeRegistry = new FakeRegistryAccessor();
        const string exe = @"C:\Program Files\AntigravityQuota\AntigravityQuota.exe";

        var manager = new StartupManager(fakeRegistry, () => exe);

        Assert.False(manager.IsEnabled());

        // Enable
        Assert.True(manager.SetEnabled(true));
        Assert.True(manager.IsEnabled());
        Assert.Equal($"\"{exe}\"", fakeRegistry.Values[@"Software\Microsoft\Windows\CurrentVersion\Run\AntigravityQuota"]);

        // Disable
        Assert.True(manager.SetEnabled(false));
        Assert.False(manager.IsEnabled());
        Assert.False(fakeRegistry.Values.ContainsKey(@"Software\Microsoft\Windows\CurrentVersion\Run\AntigravityQuota"));

        // Error handling via accessor
        fakeRegistry.ThrowOnSet = true;
        Assert.False(manager.SetEnabled(true));

        fakeRegistry.ThrowOnSet = false;
        fakeRegistry.ThrowOnGet = true;
        Assert.False(manager.IsEnabled());

        fakeRegistry.ThrowOnGet = false;
        fakeRegistry.ThrowOnDelete = true;
        Assert.False(manager.SetEnabled(false));
    }

    [Fact]
    public void FallbackAndCrossPlatformSafety_DefaultConstructorDoesNotCrash()
    {
        // On macOS / Linux (non-Windows runner), parameterless constructor must not crash with PlatformNotSupportedException.
        // It must handle platform differences safely and return false.
        var defaultManager = new StartupManager();

        if (!OperatingSystem.IsWindows())
        {
            // Default accessor returns false on non-Windows without throwing
            Assert.False(defaultManager.IsEnabled());
            Assert.False(defaultManager.SetEnabled(true, @"C:\Test\test.exe"));
            Assert.False(defaultManager.SetEnabled(false));
        }

        // Helper GetExecutablePath should always return a string (not null)
        var path = defaultManager.GetExecutablePath();
        Assert.NotNull(path);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRegistryAccessorIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new StartupManager((IRegistryAccessor)null!));
    }

    [Fact]
    public void WindowsRegistryAccessor_CrossPlatformSafety()
    {
        var accessor = new WindowsRegistryAccessor();

        if (!OperatingSystem.IsWindows())
        {
            Assert.Null(accessor.GetValue(@"Software\Test", "TestVal"));
            Assert.Throws<PlatformNotSupportedException>(() => accessor.SetValue(@"Software\Test", "TestVal", "Data"));
            Assert.Throws<PlatformNotSupportedException>(() => accessor.DeleteValue(@"Software\Test", "TestVal"));
        }
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("C:\\App.exe", null, false)]
    [InlineData(null, "C:\\App.exe", false)]
    [InlineData("", "", false)]
    [InlineData("   ", "   ", false)]
    [InlineData("C:\\App.exe", "C:\\App.exe", true)]
    [InlineData("\"C:\\App.exe\"", "C:\\App.exe", true)]
    [InlineData("C:/App.exe", "C:\\App.exe", true)]
    [InlineData("\"C:\\Program Files\\App.exe\" -arg1", "C:\\Program Files\\App.exe", true)]
    [InlineData("C:\\Tools\\App.exe --flag", "C:\\Tools\\App.exe", true)]
    [InlineData("C:\\App1.exe", "C:\\App2.exe", false)]
    public void ArePathsEquivalent_EvaluatesCorrectly(string? p1, string? p2, bool expected)
    {
        Assert.Equal(expected, StartupManager.ArePathsEquivalent(p1, p2));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\"C:\\Program Files\\App.exe\"", "C:\\Program Files\\App.exe")]
    [InlineData("\"C:\\Program Files\\App.exe\" --arg", "C:\\Program Files\\App.exe")]
    [InlineData("C:\\Tools\\App.exe", "C:\\Tools\\App.exe")]
    [InlineData("C:\\Tools\\App.exe -v", "C:\\Tools\\App.exe")]
    public void ExtractExecutablePath_ExtractsCorrectly(string? cmdLine, string expected)
    {
        Assert.Equal(expected, StartupManager.ExtractExecutablePath(cmdLine!));
    }
}

