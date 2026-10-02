using System;
using System.Collections.Generic;
using System.Linq;

namespace AntigravityQuota.Core.Models;

public record QuotaGroup
{
    public string DisplayName { get; init; }
    public string Description { get; init; }
    public IReadOnlyList<QuotaBucket> Buckets { get; init; }

    public string GroupName
    {
        get
        {
            if (DisplayName.Contains("gemini", StringComparison.OrdinalIgnoreCase))
                return "gemini";
            if (DisplayName.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
                DisplayName.Contains("gpt", StringComparison.OrdinalIgnoreCase))
                return "claude";
            return DisplayName.ToLowerInvariant();
        }
    }

    public QuotaBucket? FiveHourBucket =>
        Buckets.FirstOrDefault(b => b.Window.Equals("5h", StringComparison.OrdinalIgnoreCase) ||
                                     b.BucketId.EndsWith("-5h", StringComparison.OrdinalIgnoreCase));

    public QuotaBucket? WeeklyBucket =>
        Buckets.FirstOrDefault(b => b.Window.Equals("weekly", StringComparison.OrdinalIgnoreCase) ||
                                     b.BucketId.EndsWith("-weekly", StringComparison.OrdinalIgnoreCase));

    public QuotaGroup(string displayName, string description, IReadOnlyList<QuotaBucket> buckets)
    {
        DisplayName = displayName ?? string.Empty;
        Description = description ?? string.Empty;
        Buckets = buckets ?? Array.Empty<QuotaBucket>();
    }

    public QuotaGroup(string groupName, IReadOnlyList<QuotaBucket> buckets)
        : this(groupName, string.Empty, buckets)
    {
    }
}
