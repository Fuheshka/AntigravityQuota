using System;

namespace AntigravityQuota.Core.Models;

public record ModelConfig
{
    public string ModelId { get; init; }
    public string Label { get; init; }
    public double RemainingFraction { get; init; }
    public DateTimeOffset? ResetTime { get; init; }

    public double Percentage => Math.Clamp(RemainingFraction * 100.0, 0.0, 100.0);

    public ModelConfig(string modelId, string label, double remainingFraction, DateTimeOffset? resetTime)
    {
        ModelId = modelId ?? string.Empty;
        Label = label ?? string.Empty;
        RemainingFraction = remainingFraction;
        ResetTime = resetTime;
    }

    public ModelConfig(string label, double remainingFraction, DateTimeOffset? resetTime = null)
        : this(label, label, remainingFraction, resetTime)
    {
    }
}
