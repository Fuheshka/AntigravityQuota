using System;
using System.Collections.Generic;
using System.Linq;

namespace AntigravityQuota.Core.Models;

public record QuotaSnapshot
{
    public IReadOnlyList<QuotaGroup> Groups { get; init; }
    public IReadOnlyList<ModelConfig> Models { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public QuotaGroup? GeminiGroup =>
        Groups.FirstOrDefault(g => g.GroupName == "gemini" ||
                                   g.DisplayName.Contains("gemini", StringComparison.OrdinalIgnoreCase));

    public QuotaGroup? ClaudeGroup =>
        Groups.FirstOrDefault(g => g.GroupName == "claude" ||
                                   g.DisplayName.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
                                   g.DisplayName.Contains("gpt", StringComparison.OrdinalIgnoreCase));

    public string MenuBarTitle
    {
        get
        {
            var gPct = GeminiGroup?.FiveHourBucket?.Percentage;
            var cPct = ClaudeGroup?.FiveHourBucket?.Percentage;
            if (gPct.HasValue && cPct.HasValue)
            {
                return $"{Math.Round(gPct.Value):0}% · {Math.Round(cPct.Value):0}%";
            }

            var gModelPct = Models.FirstOrDefault(m => m.Label.Contains("gemini", StringComparison.OrdinalIgnoreCase))?.Percentage;
            var cModelPct = Models.FirstOrDefault(m => m.Label.Contains("claude", StringComparison.OrdinalIgnoreCase))?.Percentage;
            if (gModelPct.HasValue && cModelPct.HasValue)
            {
                return $"{Math.Round(gModelPct.Value):0}% · {Math.Round(cModelPct.Value):0}%";
            }

            return "—%";
        }
    }

    public QuotaSnapshot(IReadOnlyList<QuotaGroup> groups, IReadOnlyList<ModelConfig> models, DateTimeOffset? updatedAt = null)
    {
        Groups = groups ?? Array.Empty<QuotaGroup>();
        Models = models ?? Array.Empty<ModelConfig>();
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }
}
