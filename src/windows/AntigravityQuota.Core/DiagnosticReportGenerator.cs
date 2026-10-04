using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core;

/// <summary>
/// Generates formatted diagnostic reports for troubleshooting, bug reports, and issue tickets.
/// Adheres to security rules by masking sensitive CSRF tokens.
/// </summary>
public static class DiagnosticReportGenerator
{
    public const string PlatformString = ".NET 9.0 (Windows x64)";

    /// <summary>
    /// Masks a CSRF token to prevent accidental credential leakage in bug reports.
    /// Preserves prefix and suffix for debugging verification while concealing the body.
    /// </summary>
    public static string MaskCsrfToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "None";
        }

        string trimmed = token.Trim();
        if (trimmed.Length <= 8)
        {
            return "***";
        }

        return $"{trimmed[..4]}...{trimmed[^4..]}";
    }

    /// <summary>
    /// Generates a structured markdown/plain-text diagnostic report.
    /// </summary>
    public static string GenerateReport(
        ServerEndpoint? endpoint,
        QuotaSnapshot? snapshot,
        string appVersion = "1.2.0",
        string? osDescription = null,
        DateTimeOffset? lastUpdate = null,
        DateTimeOffset? timestamp = null)
    {
        var now = timestamp ?? DateTimeOffset.UtcNow;
        var os = osDescription ?? RuntimeInformation.OSDescription;
        var sb = new StringBuilder();

        sb.AppendLine("=== AntigravityQuota Diagnostic Report ===");
        sb.AppendLine($"App Version: {appVersion}");
        sb.AppendLine($"Platform: {PlatformString}");
        sb.AppendLine($"Timestamp: {now.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Status: {(endpoint != null ? "Connected" : "Disconnected (language_server process not detected)")}");
        sb.AppendLine();

        sb.AppendLine("[Language Server Discovery]");
        if (endpoint != null)
        {
            sb.AppendLine($"PID: {endpoint.Pid}");
            string portsStr = (endpoint.Ports != null && endpoint.Ports.Count > 0)
                ? string.Join(", ", endpoint.Ports)
                : "None";
            sb.AppendLine($"Listening Ports: {portsStr}");
            sb.AppendLine($"CSRF Token: {MaskCsrfToken(endpoint.CsrfToken)}");
        }
        else
        {
            sb.AppendLine("PID: Not detected");
            sb.AppendLine("Listening Ports: None");
            sb.AppendLine("CSRF Token: None");
        }

        string lastSyncStr = lastUpdate.HasValue
            ? $"{lastUpdate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC"
            : "Never";
        sb.AppendLine($"Last Quota Sync: {lastSyncStr}");
        sb.AppendLine();

        sb.AppendLine("[Quota Snapshot]");
        if (snapshot == null || snapshot.Groups.Count == 0)
        {
            sb.AppendLine("Snapshot: No data available");
        }
        else
        {
            foreach (var group in snapshot.Groups)
            {
                sb.AppendLine($"Group: {group.DisplayName}");
                foreach (var bucket in group.Buckets)
                {
                    string resetStr = bucket.ResetTime.HasValue
                        ? bucket.ResetTime.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
                        : "N/A";
                    string windowName = string.IsNullOrEmpty(bucket.Window) ? bucket.BucketId : bucket.Window;
                    sb.AppendLine($"  - {windowName}: {bucket.Percentage.ToString("F1", CultureInfo.InvariantCulture)}% available (Reset: {resetStr})");
                }
            }

            if (snapshot.Models != null && snapshot.Models.Count > 0)
            {
                sb.AppendLine("Models:");
                foreach (var model in snapshot.Models)
                {
                    sb.AppendLine($"  Model: {model.Label} ({model.ModelId}) - {model.Percentage.ToString("F1", CultureInfo.InvariantCulture)}% available");
                }
            }
        }
        sb.AppendLine();

        sb.AppendLine("[System Environment]");
        sb.AppendLine($"OS: {os}");
        sb.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"Runtime: .NET {Environment.Version}");
        sb.AppendLine("=========================================");

        return sb.ToString();
    }
}
