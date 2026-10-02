using System;

namespace AntigravityQuota.Core.Models;

public record QuotaBucket
{
    public string BucketId { get; init; }
    public string Window { get; init; }
    public double RemainingFraction { get; init; }
    public DateTimeOffset? ResetTime { get; init; }

    public double Percentage => Math.Clamp(RemainingFraction * 100.0, 0.0, 100.0);
    public TimeSpan? TimeUntilReset => ResetTime.HasValue ? (ResetTime.Value > DateTimeOffset.UtcNow ? ResetTime.Value - DateTimeOffset.UtcNow : TimeSpan.Zero) : null;

    public QuotaBucket(string bucketId, string window, double remainingFraction, DateTimeOffset? resetTime)
    {
        BucketId = bucketId ?? string.Empty;
        Window = window ?? string.Empty;
        RemainingFraction = remainingFraction;
        ResetTime = resetTime;
    }

    public QuotaBucket(double remainingFraction, DateTimeOffset? resetTime = null)
        : this(string.Empty, string.Empty, remainingFraction, resetTime)
    {
    }
}
