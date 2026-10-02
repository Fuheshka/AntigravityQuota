using System.Text.Json;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core;

public static class HudSettingsManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetDefaultSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "AntigravityQuota", "settings.json");
    }

    public static HudSettings Load(string? filePath = null)
    {
        var path = filePath ?? GetDefaultSettingsPath();
        try
        {
            if (!File.Exists(path))
            {
                return new HudSettings();
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<HudSettings>(json, JsonOptions);
            return settings ?? new HudSettings();
        }
        catch
        {
            return new HudSettings();
        }
    }

    public static void Save(HudSettings settings, string? filePath = null)
    {
        var path = filePath ?? GetDefaultSettingsPath();
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Suppress IO errors to avoid breaking UI operations
        }
    }

    public static (double X, double Y) ClampPosition(
        double x,
        double y,
        double windowWidth,
        double windowHeight,
        double screenLeft,
        double screenTop,
        double screenWidth,
        double screenHeight)
    {
        double maxX = screenLeft + screenWidth - windowWidth;
        double maxY = screenTop + screenHeight - windowHeight;

        double clampedX = maxX >= screenLeft ? Math.Clamp(x, screenLeft, maxX) : screenLeft;
        double clampedY = maxY >= screenTop ? Math.Clamp(y, screenTop, maxY) : screenTop;

        return (clampedX, clampedY);
    }
}
