namespace AntigravityQuota.Core.Models;

/// <summary>
/// User configuration settings for the Quota HUD overlay, persisted to settings.json.
/// </summary>
public class HudSettings
{
    /// <summary>
    /// X screen coordinate (DIPs). Null indicates unpositioned (center or default).
    /// </summary>
    public double? X { get; set; }

    /// <summary>
    /// Y screen coordinate (DIPs). Null indicates unpositioned (center or default).
    /// </summary>
    public double? Y { get; set; }

    /// <summary>
    /// Whether the HUD overlay is enabled and visible.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether the HUD is currently in compact pill mode (true) or expanded card mode (false).
    /// </summary>
    public bool IsPillMode { get; set; } = false;

    /// <summary>
    /// Whether the sparkline chart is expanded in card mode.
    /// </summary>
    public bool IsSparklineExpanded { get; set; } = false;

    /// <summary>
    /// Whether the HUD automatically fades in when Antigravity/Code IDE is focused and fades out for other apps.
    /// </summary>
    public bool AutoHideEnabled { get; set; } = true;

    /// <summary>
    /// Whether click-through mode (WS_EX_TRANSPARENT) is enabled, passing clicks to underlying code unless Alt is held.
    /// </summary>
    public bool ClickThroughEnabled { get; set; } = false;

    /// <summary>
    /// Whether global system hotkeys (Alt+Shift+Q, Alt+Shift+M, Alt+Shift+R) are enabled.
    /// </summary>
    public bool HotkeysEnabled { get; set; } = true;
}
