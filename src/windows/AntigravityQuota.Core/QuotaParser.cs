using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core;

public static class QuotaParser
{
    public static IReadOnlyList<QuotaGroup> ParseRetrieveUserQuotaSummary(string? json) => ParseSummary(json);

    public static IReadOnlyList<QuotaGroup> ParseSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<QuotaGroup>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseSummaryDocument(doc);
        }
        catch (JsonException)
        {
            return Array.Empty<QuotaGroup>();
        }
        catch (Exception)
        {
            return Array.Empty<QuotaGroup>();
        }
    }

    public static IReadOnlyList<QuotaGroup> ParseSummary(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
            return Array.Empty<QuotaGroup>();

        try
        {
            using var doc = JsonDocument.Parse(utf8Json);
            return ParseSummaryDocument(doc);
        }
        catch (JsonException)
        {
            return Array.Empty<QuotaGroup>();
        }
        catch (Exception)
        {
            return Array.Empty<QuotaGroup>();
        }
    }

    private static IReadOnlyList<QuotaGroup> ParseSummaryDocument(JsonDocument doc)
    {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return Array.Empty<QuotaGroup>();

        var container = root.TryGetProperty("response", out var resp) && resp.ValueKind == JsonValueKind.Object
            ? resp
            : root;

        if (!container.TryGetProperty("groups", out var groupsElement) || groupsElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<QuotaGroup>();

        var groups = new List<QuotaGroup>();

        foreach (var g in groupsElement.EnumerateArray())
        {
            if (g.ValueKind != JsonValueKind.Object)
                continue;

            var displayName = g.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String
                ? dn.GetString() ?? string.Empty
                : string.Empty;

            var description = g.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
                ? desc.GetString() ?? string.Empty
                : string.Empty;

            var buckets = new List<QuotaBucket>();

            if (g.TryGetProperty("buckets", out var bucketsElement) && bucketsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in bucketsElement.EnumerateArray())
                {
                    if (b.ValueKind != JsonValueKind.Object)
                        continue;

                    var bucketId = b.TryGetProperty("bucketId", out var bid) && bid.ValueKind == JsonValueKind.String
                        ? bid.GetString() ?? string.Empty
                        : string.Empty;

                    var window = b.TryGetProperty("window", out var win) && win.ValueKind == JsonValueKind.String
                        ? win.GetString() ?? string.Empty
                        : string.Empty;

                    var remainingFraction = 0.0;
                    if (b.TryGetProperty("remainingFraction", out var rf))
                    {
                        if (rf.ValueKind == JsonValueKind.Number)
                        {
                            remainingFraction = rf.GetDouble();
                        }
                        else if (rf.ValueKind == JsonValueKind.String &&
                                 double.TryParse(rf.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRf))
                        {
                            remainingFraction = parsedRf;
                        }
                    }

                    DateTimeOffset? resetTime = null;
                    if (b.TryGetProperty("resetTime", out var rt) && rt.ValueKind == JsonValueKind.String)
                    {
                        var rtStr = rt.GetString();
                        if (!string.IsNullOrWhiteSpace(rtStr) &&
                            DateTimeOffset.TryParse(rtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
                        {
                            resetTime = parsedDate;
                        }
                    }

                    buckets.Add(new QuotaBucket(bucketId, window, remainingFraction, resetTime));
                }
            }

            groups.Add(new QuotaGroup(displayName, description, buckets));
        }

        return groups;
    }

    public static IReadOnlyList<ModelConfig> ParseCascadeModelConfigData(string? json) => ParseCascadeModelConfigs(json);

    public static IReadOnlyList<ModelConfig> ParseCascadeModelConfigs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ModelConfig>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseCascadeModelConfigsDocument(doc);
        }
        catch (JsonException)
        {
            return Array.Empty<ModelConfig>();
        }
        catch (Exception)
        {
            return Array.Empty<ModelConfig>();
        }
    }

    public static IReadOnlyList<ModelConfig> ParseCascadeModelConfigs(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
            return Array.Empty<ModelConfig>();

        try
        {
            using var doc = JsonDocument.Parse(utf8Json);
            return ParseCascadeModelConfigsDocument(doc);
        }
        catch (JsonException)
        {
            return Array.Empty<ModelConfig>();
        }
        catch (Exception)
        {
            return Array.Empty<ModelConfig>();
        }
    }

    private static IReadOnlyList<ModelConfig> ParseCascadeModelConfigsDocument(JsonDocument doc)
    {
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return Array.Empty<ModelConfig>();

        var container = root;
        if (root.TryGetProperty("userStatus", out var userStatus) && userStatus.ValueKind == JsonValueKind.Object &&
            userStatus.TryGetProperty("cascadeModelConfigData", out var cascadeData) && cascadeData.ValueKind == JsonValueKind.Object)
        {
            container = cascadeData;
        }

        if (!container.TryGetProperty("clientModelConfigs", out var configsElement) || configsElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<ModelConfig>();

        var models = new List<ModelConfig>();

        foreach (var item in configsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var label = item.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String
                ? l.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(label))
                continue;

            var modelId = item.TryGetProperty("modelId", out var mid) && mid.ValueKind == JsonValueKind.String
                ? mid.GetString()
                : label;

            if (string.IsNullOrWhiteSpace(modelId))
                modelId = label;

            var remainingFraction = 0.0;
            DateTimeOffset? resetTime = null;

            if (item.TryGetProperty("quotaInfo", out var qi) && qi.ValueKind == JsonValueKind.Object)
            {
                if (qi.TryGetProperty("remainingFraction", out var rf))
                {
                    if (rf.ValueKind == JsonValueKind.Number)
                    {
                        remainingFraction = rf.GetDouble();
                    }
                    else if (rf.ValueKind == JsonValueKind.String &&
                             double.TryParse(rf.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRf))
                    {
                        remainingFraction = parsedRf;
                    }
                }

                if (qi.TryGetProperty("resetTime", out var rt) && rt.ValueKind == JsonValueKind.String)
                {
                    var rtStr = rt.GetString();
                    if (!string.IsNullOrWhiteSpace(rtStr) &&
                        DateTimeOffset.TryParse(rtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
                    {
                        resetTime = parsedDate;
                    }
                }
            }

            models.Add(new ModelConfig(modelId, label, remainingFraction, resetTime));
        }

        return models;
    }
}
