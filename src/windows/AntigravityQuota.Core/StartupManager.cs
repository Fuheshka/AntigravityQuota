using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AntigravityQuota.Core;

/// <summary>
/// Abstraction for Windows Registry operations to allow 100% testability across platforms.
/// </summary>
public interface IRegistryAccessor
{
    string? GetValue(string keyPath, string valueName);
    void SetValue(string keyPath, string valueName, string value);
    void DeleteValue(string keyPath, string valueName);
}

/// <summary>
/// Native Windows Registry implementation using HKCU (CurrentUser).
/// Safe for compilation on any OS with runtime platform guards.
/// </summary>
public sealed class WindowsRegistryAccessor : IRegistryAccessor
{
    public string? GetValue(string keyPath, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public void SetValue(string keyPath, string valueName, string value)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Registry operations are only supported on Windows.");
        }

        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException($"Unable to open or create registry subkey '{keyPath}'.");
        }

        key.SetValue(valueName, value);
    }

    public void DeleteValue(string keyPath, string valueName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Registry operations are only supported on Windows.");
        }

        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

/// <summary>
/// Manages application auto-start on Windows user logon via the Registry:
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Adheres to Ponytail principles: minimal footprint, standard APIs, and zero unhandled exceptions.
/// </summary>
public sealed class StartupManager
{
    public const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "AntigravityQuota";

    // Public aliases and properties for caller convenience
    public static string RegistryKeyPath => RunRegistrySubKey;
    public static string ValueName => DefaultValueName;

    private static readonly WindowsRegistryAccessor DefaultAccessor = new();

    private readonly Func<string, string?> _getValue;
    private readonly Action<string, string> _setValue;
    private readonly Action<string> _deleteValue;
    private readonly Func<string?> _processPathResolver;

    /// <summary>
    /// Constructs a StartupManager with delegate injection for registry and process path resolution.
    /// Default delegates use native Windows Registry and current process path resolution.
    /// </summary>
    public StartupManager(
        Func<string, string?>? getValue = null,
        Action<string, string>? setValue = null,
        Action<string>? deleteValue = null,
        Func<string?>? processPathResolver = null)
    {
        _getValue = getValue ?? (name => DefaultAccessor.GetValue(RunRegistrySubKey, name));
        _setValue = setValue ?? ((name, val) => DefaultAccessor.SetValue(RunRegistrySubKey, name, val));
        _deleteValue = deleteValue ?? (name => DefaultAccessor.DeleteValue(RunRegistrySubKey, name));
        _processPathResolver = processPathResolver ?? ResolveDefaultProcessPath;
    }

    /// <summary>
    /// Constructs a StartupManager using an IRegistryAccessor provider.
    /// </summary>
    public StartupManager(
        IRegistryAccessor registryAccessor,
        Func<string?>? processPathResolver = null)
        : this(
            getValue: name => registryAccessor.GetValue(RunRegistrySubKey, name),
            setValue: (name, val) => registryAccessor.SetValue(RunRegistrySubKey, name, val),
            deleteValue: name => registryAccessor.DeleteValue(RunRegistrySubKey, name),
            processPathResolver: processPathResolver)
    {
        ArgumentNullException.ThrowIfNull(registryAccessor);
    }

    /// <summary>
    /// Checks whether the application auto-start entry exists in the Windows Registry
    /// and matches the expected executable path.
    /// </summary>
    public bool IsEnabled(string? customExecutablePath = null)
    {
        try
        {
            var existing = _getValue(DefaultValueName);
            if (string.IsNullOrWhiteSpace(existing))
            {
                return false;
            }

            var expected = GetExecutablePath(customExecutablePath);
            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            return ArePathsEquivalent(existing, expected);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables application auto-start in the Windows Registry.
    /// Returns true if successful, or false if an error occurs.
    /// </summary>
    public bool SetEnabled(bool enable, string? customExecutablePath = null)
    {
        try
        {
            if (enable)
            {
                var exePath = GetExecutablePath(customExecutablePath);
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    return false;
                }

                _setValue(DefaultValueName, exePath);
            }
            else
            {
                _deleteValue(DefaultValueName);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves and formats the executable path, wrapping in double quotes if spaces exist.
    /// </summary>
    public string GetExecutablePath(string? customPath = null)
    {
        string raw = !string.IsNullOrWhiteSpace(customPath)
            ? customPath
            : (_processPathResolver() ?? string.Empty);

        return FormatExecutablePath(raw);
    }

    /// <summary>
    /// Formats an executable path for the Windows Run registry key.
    /// Strips surrounding quotes, then re-wraps in quotes if spaces are present or if originally quoted.
    /// </summary>
    public static string FormatExecutablePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var trimmed = rawPath.Trim();
        bool hadQuotes = false;
        if (trimmed.Length >= 2 && trimmed.StartsWith('\"') && trimmed.EndsWith('\"'))
        {
            trimmed = trimmed[1..^1].Trim();
            hadQuotes = true;
        }

        if (trimmed.Contains(' ') || hadQuotes)
        {
            return $"\"{trimmed}\"";
        }

        return trimmed;
    }

    /// <summary>
    /// Determines whether two executable paths are equivalent, taking into account
    /// quotes, forward/backward slashes, casing, and optional command-line arguments.
    /// </summary>
    public static bool ArePathsEquivalent(string? path1, string? path2)
    {
        if (path1 == null || path2 == null)
        {
            return false;
        }

        var clean1 = CleanPathForComparison(path1);
        var clean2 = CleanPathForComparison(path2);

        if (string.IsNullOrEmpty(clean1) || string.IsNullOrEmpty(clean2))
        {
            return false;
        }

        if (string.Equals(clean1, clean2, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var exe1 = ExtractExecutablePath(path1).Replace('/', '\\');
        var exe2 = ExtractExecutablePath(path2).Replace('/', '\\');

        if (!string.IsNullOrEmpty(exe1) && !string.IsNullOrEmpty(exe2))
        {
            if (string.Equals(exe1, exe2, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(exe1, clean2, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(clean1, exe2, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the executable portion from a command-line string (handling surrounding quotes).
    /// </summary>
    public static string ExtractExecutablePath(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return string.Empty;
        }

        var trimmed = commandLine.Trim();
        if (trimmed.StartsWith('\"'))
        {
            int endQuoteIndex = trimmed.IndexOf('\"', 1);
            if (endQuoteIndex > 0)
            {
                return trimmed[1..endQuoteIndex].Trim();
            }

            return trimmed[1..].Trim();
        }

        int firstSpaceIndex = trimmed.IndexOf(' ');
        if (firstSpaceIndex > 0)
        {
            return trimmed[..firstSpaceIndex].Trim();
        }

        return trimmed;
    }

    private static string CleanPathForComparison(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('\"') && trimmed.EndsWith('\"'))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        return trimmed.Replace('/', '\\');
    }

    private static string? ResolveDefaultProcessPath()
    {
        return Environment.ProcessPath ?? GetMainModuleFileNameSafe() ?? string.Empty;
    }

    private static string? GetMainModuleFileNameSafe()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}
