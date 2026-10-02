using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AntigravityQuota.Core.Models;

namespace AntigravityQuota.Core;

/// <summary>
/// Direction of quota consumption trend.
/// </summary>
public enum QuotaTrend
{
    Falling,
    Steady,
    Rising
}

/// <summary>
/// Quota model pool identifier.
/// </summary>
public enum QuotaPool
{
    Gemini,
    Claude
}

/// <summary>
/// 2D coordinate point for WPF Sparkline rendering.
/// </summary>
public readonly record struct SparklinePoint(double X, double Y);

/// <summary>
/// Normalized sparkline dataset containing points for Bezier curve rendering in WPF.
/// </summary>
public record SparklineData : IReadOnlyList<SparklinePoint>
{
    public IReadOnlyList<SparklinePoint> GeminiPoints { get; init; } = Array.Empty<SparklinePoint>();
    public IReadOnlyList<SparklinePoint> ClaudePoints { get; init; } = Array.Empty<SparklinePoint>();
    public TimeSpan WindowDuration { get; init; } = TimeSpan.FromHours(1);
    public bool HasSufficientData { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public IReadOnlyList<SparklinePoint> Points => GeminiPoints.Count > 0 ? GeminiPoints : ClaudePoints;

    public int Count => Points.Count;
    public SparklinePoint this[int index] => Points[index];
    public IEnumerator<SparklinePoint> GetEnumerator() => Points.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Generates a smooth cubic Bezier curve path string in WPF/SVG mini-language format.
    /// Format: "M x0,y0 C cp1x,cp1y cp2x,cp2y x1,y1 ..."
    /// </summary>
    public string ToPathGeometry(QuotaPool pool = QuotaPool.Gemini)
    {
        var pts = pool == QuotaPool.Gemini ? GeminiPoints : ClaudePoints;
        if (pts.Count == 0 && Points.Count > 0)
        {
            pts = Points;
        }

        if (pts.Count < 2)
        {
            return string.Empty;
        }

        if (pts.Count == 2)
        {
            return string.Create(CultureInfo.InvariantCulture, $"M {pts[0].X:F1},{pts[0].Y:F1} L {pts[1].X:F1},{pts[1].Y:F1}");
        }

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"M {pts[0].X:F1},{pts[0].Y:F1}");

        for (var i = 0; i < pts.Count - 1; i++)
        {
            var pPrev = i > 0 ? pts[i - 1] : pts[i];
            var pCurr = pts[i];
            var pNext = pts[i + 1];
            var pAfter = (i + 2 < pts.Count) ? pts[i + 2] : pts[i + 1];

            var cp1X = pCurr.X + (pNext.X - pPrev.X) / 6.0;
            var cp1Y = pCurr.Y + (pNext.Y - pPrev.Y) / 6.0;
            var cp2X = pNext.X - (pAfter.X - pCurr.X) / 6.0;
            var cp2Y = pNext.Y - (pAfter.Y - pCurr.Y) / 6.0;

            sb.Append(CultureInfo.InvariantCulture, $" C {cp1X:F1},{cp1Y:F1} {cp2X:F1},{cp2Y:F1} {pNext.X:F1},{pNext.Y:F1}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Quota consumption rate and trend calculations.
/// </summary>
public record QuotaBurnRate(
    double BurnRatePerHour,
    QuotaTrend Trend,
    TimeSpan? EstimatedTimeToDepletion)
{
    public string Formatted(bool isRussian = true)
    {
        switch (Trend)
        {
            case QuotaTrend.Steady:
                return isRussian ? "~0%/ч" : "~0%/h";

            case QuotaTrend.Rising:
                var pctReset = Math.Abs(BurnRatePerHour);
                return isRussian ? $"+{pctReset:0}%/ч (сброс)" : $"+{pctReset:0}%/h (reset)";

            case QuotaTrend.Falling:
                var rateStr = $"{BurnRatePerHour:0}%";
                if (EstimatedTimeToDepletion is { } time)
                {
                    var totalSeconds = (int)Math.Round(time.TotalSeconds);
                    var hours = totalSeconds / 3600;
                    var minutes = (totalSeconds % 3600) / 60;

                    string timeStr;
                    if (hours > 0 && minutes > 0)
                    {
                        timeStr = isRussian ? $"~{hours}ч {minutes}м" : $"~{hours}h {minutes}m left";
                    }
                    else if (hours > 0)
                    {
                        timeStr = isRussian ? $"~{hours}ч" : $"~{hours}h left";
                    }
                    else
                    {
                        var m = Math.Max(1, minutes);
                        timeStr = isRussian ? $"~{m}м" : $"~{m}m left";
                    }

                    return isRussian ? $"{rateStr}/ч (хватит на {timeStr})" : $"{rateStr}/h ({timeStr})";
                }
                return isRussian ? $"{rateStr}/ч" : $"{rateStr}/h";

            default:
                return "~0%/ч";
        }
    }
}

/// <summary>
/// Individual quota observation sample.
/// </summary>
public record QuotaHistorySample(
    DateTimeOffset Timestamp,
    double? GeminiPercentage,
    double? ClaudePercentage);

/// <summary>
/// Thread-safe sliding window quota history tracker for Burn Rate and Sparkline calculation.
/// </summary>
public class QuotaHistoryTracker
{
    private readonly TimeSpan _windowDuration;
    private readonly object _lock = new();
    private readonly List<QuotaHistorySample> _samples = new();

    public TimeSpan WindowDuration => _windowDuration;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _samples.Count;
            }
        }
    }

    public double BurnRatePerHour => CalculateBurnRate(QuotaPool.Gemini).BurnRatePerHour;
    public QuotaTrend Trend => CalculateBurnRate(QuotaPool.Gemini).Trend;
    public TimeSpan? EstimatedTimeToDepletion => CalculateBurnRate(QuotaPool.Gemini).EstimatedTimeToDepletion;

    public QuotaHistoryTracker(TimeSpan? windowDuration = null)
    {
        _windowDuration = windowDuration ?? TimeSpan.FromHours(1);
    }

    public void Record(double? geminiPercentage, double? claudePercentage, DateTimeOffset? timestamp = null)
    {
        var time = timestamp ?? DateTimeOffset.UtcNow;
        var clampedG = geminiPercentage.HasValue ? Math.Clamp(geminiPercentage.Value, 0.0, 100.0) : (double?)null;
        var clampedC = claudePercentage.HasValue ? Math.Clamp(claudePercentage.Value, 0.0, 100.0) : (double?)null;

        var sample = new QuotaHistorySample(time, clampedG, clampedC);

        lock (_lock)
        {
            _samples.Add(sample);
            PruneInternal(time);
        }
    }

    public void Record(QuotaSnapshot snapshot, DateTimeOffset? timestamp = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var time = timestamp ?? snapshot.UpdatedAt;
        var g = snapshot.GeminiGroup?.FiveHourBucket?.Percentage
                ?? snapshot.GeminiGroup?.Buckets.FirstOrDefault()?.Percentage
                ?? snapshot.Models.FirstOrDefault(m => m.Label.Contains("gemini", StringComparison.OrdinalIgnoreCase))?.Percentage;
        var c = snapshot.ClaudeGroup?.FiveHourBucket?.Percentage
                ?? snapshot.ClaudeGroup?.Buckets.FirstOrDefault()?.Percentage
                ?? snapshot.Models.FirstOrDefault(m => m.Label.Contains("claude", StringComparison.OrdinalIgnoreCase) ||
                                                       m.Label.Contains("gpt", StringComparison.OrdinalIgnoreCase))?.Percentage;

        Record(g, c, time);
    }

    public void Prune(DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            PruneInternal(now ?? DateTimeOffset.UtcNow);
        }
    }

    private void PruneInternal(DateTimeOffset now)
    {
        var cutoff = now - _windowDuration;
        _samples.RemoveAll(s => s.Timestamp < cutoff);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _samples.Clear();
        }
    }

    public IReadOnlyList<QuotaHistorySample> GetSamples(DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            PruneInternal(now ?? DateTimeOffset.UtcNow);
            return _samples.ToList();
        }
    }

    public QuotaBurnRate CalculateBurnRate(QuotaPool pool = QuotaPool.Gemini, DateTimeOffset? now = null)
    {
        lock (_lock)
        {
            var currentTime = now ?? DateTimeOffset.UtcNow;
            PruneInternal(currentTime);

            var poolSamples = _samples
                .Select(s => (
                    s.Timestamp,
                    Pct: pool == QuotaPool.Gemini ? s.GeminiPercentage : s.ClaudePercentage
                ))
                .Where(s => s.Pct.HasValue)
                .Select(s => (s.Timestamp, Pct: s.Pct!.Value))
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (poolSamples.Count < 2)
            {
                return new QuotaBurnRate(0.0, QuotaTrend.Steady, null);
            }

            // Check if there was a quota reset (sudden increase >= 15%) in sample history
            var startIndex = 0;
            for (var i = 1; i < poolSamples.Count; i++)
            {
                if (poolSamples[i].Pct - poolSamples[i - 1].Pct >= 15.0)
                {
                    startIndex = i;
                }
            }

            var baselineSample = poolSamples[startIndex];
            var latestSample = poolSamples[^1];
            var elapsedSeconds = (latestSample.Timestamp - baselineSample.Timestamp).TotalSeconds;

            if (elapsedSeconds < 15.0)
            {
                // If reset occurred at the very latest sample
                if (startIndex > 0 && startIndex == poolSamples.Count - 1)
                {
                    var prevSample = poolSamples[startIndex - 1];
                    var jump = latestSample.Pct - prevSample.Pct;
                    var jumpElapsed = Math.Max(1.0, (latestSample.Timestamp - prevSample.Timestamp).TotalSeconds);
                    var rate = (jump / jumpElapsed) * 3600.0;
                    return new QuotaBurnRate(rate, QuotaTrend.Rising, null);
                }

                return new QuotaBurnRate(0.0, QuotaTrend.Steady, null);
            }

            var deltaPct = latestSample.Pct - baselineSample.Pct;
            var hours = elapsedSeconds / 3600.0;
            var ratePerHour = deltaPct / hours;

            if (ratePerHour < -0.5)
            {
                TimeSpan? depletion;
                if (latestSample.Pct <= 0.0)
                {
                    depletion = TimeSpan.Zero;
                }
                else
                {
                    var hoursRemaining = latestSample.Pct / Math.Abs(ratePerHour);
                    depletion = TimeSpan.FromHours(hoursRemaining);
                }

                return new QuotaBurnRate(ratePerHour, QuotaTrend.Falling, depletion);
            }

            if (ratePerHour > 0.5)
            {
                return new QuotaBurnRate(ratePerHour, QuotaTrend.Rising, null);
            }

            return new QuotaBurnRate(ratePerHour, QuotaTrend.Steady, null);
        }
    }

    public SparklineData GetSparklineData(int width, int height, DateTimeOffset? now = null)
    {
        if (width <= 0 || height <= 0)
        {
            return new SparklineData
            {
                GeminiPoints = Array.Empty<SparklinePoint>(),
                ClaudePoints = Array.Empty<SparklinePoint>(),
                WindowDuration = _windowDuration,
                HasSufficientData = false,
                Width = width,
                Height = height
            };
        }

        lock (_lock)
        {
            var currentTime = now ?? DateTimeOffset.UtcNow;
            PruneInternal(currentTime);

            var validSamples = _samples
                .Where(s => s.Timestamp <= currentTime && s.Timestamp >= currentTime - _windowDuration)
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (validSamples.Count < 2)
            {
                return new SparklineData
                {
                    GeminiPoints = Array.Empty<SparklinePoint>(),
                    ClaudePoints = Array.Empty<SparklinePoint>(),
                    WindowDuration = _windowDuration,
                    HasSufficientData = false,
                    Width = width,
                    Height = height
                };
            }

            var firstTime = validSamples[0].Timestamp;
            var timeSpan = (currentTime - firstTime).TotalSeconds;

            if (timeSpan < 5.0)
            {
                return new SparklineData
                {
                    GeminiPoints = Array.Empty<SparklinePoint>(),
                    ClaudePoints = Array.Empty<SparklinePoint>(),
                    WindowDuration = _windowDuration,
                    HasSufficientData = false,
                    Width = width,
                    Height = height
                };
            }

            var effectiveWindowSeconds = Math.Max(timeSpan, 60.0);
            const double topInset = 2.0;
            const double bottomInset = 2.0;
            var usableHeight = Math.Max(1.0, height - topInset - bottomInset);

            List<SparklinePoint> NormalizePoints(Func<QuotaHistorySample, double?> selector)
            {
                var pts = new List<SparklinePoint>();
                foreach (var s in validSamples)
                {
                    var val = selector(s);
                    if (val.HasValue)
                    {
                        var progress = (s.Timestamp - firstTime).TotalSeconds / Math.Max(1.0, (currentTime - firstTime).TotalSeconds);
                        var clampedProgress = Math.Clamp(progress, 0.0, 1.0);
                        var x = clampedProgress * width;

                        var yRatio = Math.Clamp(val.Value / 100.0, 0.0, 1.0);
                        var y = topInset + (1.0 - yRatio) * usableHeight;
                        pts.Add(new SparklinePoint(x, y));
                    }
                }

                if (pts.Count > 0 && pts[^1].X < width - 0.5)
                {
                    pts.Add(new SparklinePoint(width, pts[^1].Y));
                }

                return pts;
            }

            var geminiPoints = NormalizePoints(s => s.GeminiPercentage);
            var claudePoints = NormalizePoints(s => s.ClaudePercentage);

            var hasSufficientData = geminiPoints.Count >= 2 || claudePoints.Count >= 2;

            return new SparklineData
            {
                GeminiPoints = geminiPoints,
                ClaudePoints = claudePoints,
                WindowDuration = TimeSpan.FromSeconds(effectiveWindowSeconds),
                HasSufficientData = hasSufficientData,
                Width = width,
                Height = height
            };
        }
    }
}
