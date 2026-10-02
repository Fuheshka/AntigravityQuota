using System;
using System.Collections.Generic;
using System.Linq;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using Xunit;

namespace AntigravityQuota.Tests;

public class QuotaHistoryTrackerTests
{
    [Fact]
    public void EmptyHistory_ReturnsStableZeroBurnRate()
    {
        var tracker = new QuotaHistoryTracker();
        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini);

        Assert.Equal(QuotaTrend.Steady, rate.Trend);
        Assert.Equal(0.0, rate.BurnRatePerHour, precision: 3);
        Assert.Null(rate.EstimatedTimeToDepletion);
        Assert.Equal(0, tracker.Count);
        Assert.Equal("~0%/ч", rate.Formatted(isRussian: true));
        Assert.Equal("~0%/h", rate.Formatted(isRussian: false));
    }

    [Fact]
    public void SingleSample_ReturnsStableZeroBurnRate()
    {
        var tracker = new QuotaHistoryTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.Record(geminiPercentage: 95.0, claudePercentage: 90.0, timestamp: now);

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: now);

        Assert.Equal(1, tracker.Count);
        Assert.Equal(QuotaTrend.Steady, rate.Trend);
        Assert.Equal(0.0, rate.BurnRatePerHour, precision: 3);
        Assert.Null(rate.EstimatedTimeToDepletion);
    }

    [Fact]
    public void InsufficientElapsedTime_ReturnsStableZeroBurnRate()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        tracker.Record(100.0, 100.0, timestamp: start);
        tracker.Record(95.0, 100.0, timestamp: start.AddSeconds(5)); // only 5s elapsed

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddSeconds(5));

        Assert.Equal(QuotaTrend.Steady, rate.Trend);
        Assert.Equal(0.0, rate.BurnRatePerHour, precision: 3);
        Assert.Null(rate.EstimatedTimeToDepletion);
    }

    [Fact]
    public void ConstantQuota_ReturnsStableZeroBurnRate()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        tracker.Record(100.0, 100.0, timestamp: start);
        tracker.Record(100.0, 100.0, timestamp: start.AddMinutes(30));

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddMinutes(30));

        Assert.Equal(QuotaTrend.Steady, rate.Trend);
        Assert.Equal(0.0, rate.BurnRatePerHour, precision: 3);
        Assert.Null(rate.EstimatedTimeToDepletion);
        Assert.Equal("~0%/ч", rate.Formatted(isRussian: true));
    }

    [Fact]
    public void SteadyConsumption_CalculatesFallingBurnRateAndDepletion()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        // 100% down to 70% in 1 hour (3600 seconds) -> -30%/h
        tracker.Record(100.0, 100.0, timestamp: start);
        tracker.Record(70.0, 100.0, timestamp: start.AddHours(1));

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddHours(1));

        Assert.Equal(QuotaTrend.Falling, rate.Trend);
        Assert.Equal(-30.0, rate.BurnRatePerHour, precision: 1);

        // 70% remaining at 30%/h = 2h 20m = 8400s
        Assert.NotNull(rate.EstimatedTimeToDepletion);
        var depletion = rate.EstimatedTimeToDepletion!.Value;
        Assert.InRange(depletion.TotalSeconds, 8390.0, 8410.0);

        Assert.Equal("-30%/ч (хватит на ~2ч 20м)", rate.Formatted(isRussian: true));
        Assert.Equal("-30%/h (~2h 20m left)", rate.Formatted(isRussian: false));
    }

    [Fact]
    public void RapidConsumptionOverShortPeriod_CalculatesAccurateBurnRate()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        // Drops from 80% to 70% in 10 minutes (600s) -> -10% in 1/6 hr = -60%/hr
        tracker.Record(80.0, 100.0, timestamp: start);
        tracker.Record(70.0, 100.0, timestamp: start.AddMinutes(10));

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddMinutes(10));

        Assert.Equal(QuotaTrend.Falling, rate.Trend);
        Assert.Equal(-60.0, rate.BurnRatePerHour, precision: 1);

        // 70% remaining at 60%/h = 1h 10m = 4200 seconds
        Assert.NotNull(rate.EstimatedTimeToDepletion);
        var depletion = rate.EstimatedTimeToDepletion!.Value;
        Assert.InRange(depletion.TotalSeconds, 4190.0, 4210.0);

        Assert.Equal("-60%/ч (хватит на ~1ч 10м)", rate.Formatted(isRussian: true));
    }

    [Fact]
    public void QuotaReset_IdentifiesRisingTrendWithoutDepletion()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        // Quota was at 10%, resets to 100% 5 minutes later
        tracker.Record(10.0, 10.0, timestamp: start);
        tracker.Record(100.0, 10.0, timestamp: start.AddMinutes(5));

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddMinutes(5));

        Assert.Equal(QuotaTrend.Rising, rate.Trend);
        Assert.True(rate.BurnRatePerHour > 0);
        Assert.Null(rate.EstimatedTimeToDepletion);
        Assert.Contains("сброс", rate.Formatted(isRussian: true));
        Assert.Contains("reset", rate.Formatted(isRussian: false));
    }

    [Fact]
    public void ConsumptionAfterReset_CalculatesBurnRateFromResetBaseline()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        // Old samples before reset
        tracker.Record(25.0, null, timestamp: start);
        tracker.Record(15.0, null, timestamp: start.AddMinutes(5));
        // Reset jump to 100% at +10 min
        tracker.Record(100.0, null, timestamp: start.AddMinutes(10));
        // Burned 10% in 20 minutes (from 100% to 90% at +30 min)
        tracker.Record(90.0, null, timestamp: start.AddMinutes(30));

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddMinutes(30));

        // From reset at +10m to +30m: 10% consumed over 20 minutes (1/3 hr) -> -30%/h
        Assert.Equal(QuotaTrend.Falling, rate.Trend);
        Assert.Equal(-30.0, rate.BurnRatePerHour, precision: 1);
        Assert.NotNull(rate.EstimatedTimeToDepletion);
    }

    [Fact]
    public void SlidingBuffer_PrunesSamplesOlderThanOneHour()
    {
        var tracker = new QuotaHistoryTracker(windowDuration: TimeSpan.FromHours(1));
        var start = DateTimeOffset.UtcNow;

        // Samples older than 1 hour
        tracker.Record(100.0, 100.0, timestamp: start.AddMinutes(-90));
        tracker.Record(95.0, 100.0, timestamp: start.AddMinutes(-70));

        // Samples within 1 hour
        tracker.Record(80.0, 100.0, timestamp: start.AddMinutes(-30));
        tracker.Record(70.0, 100.0, timestamp: start);

        Assert.Equal(2, tracker.Count);

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start);
        // From 80% to 70% in 30 minutes (0.5 hr) -> -20%/h
        Assert.Equal(QuotaTrend.Falling, rate.Trend);
        Assert.Equal(-20.0, rate.BurnRatePerHour, precision: 1);
    }

    [Fact]
    public void BoundaryValues_ZeroPercentQuota_DepletionIsZero()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        tracker.Record(20.0, 100.0, timestamp: start);
        tracker.Record(0.0, 100.0, timestamp: start.AddMinutes(30));

        var rate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddMinutes(30));

        Assert.Equal(QuotaTrend.Falling, rate.Trend);
        Assert.Equal(-40.0, rate.BurnRatePerHour, precision: 1);
        Assert.NotNull(rate.EstimatedTimeToDepletion);
        Assert.Equal(TimeSpan.Zero, rate.EstimatedTimeToDepletion!.Value);
    }

    [Fact]
    public void BoundaryValues_InputOutOfRange_ClampsToZeroAndHundred()
    {
        var tracker = new QuotaHistoryTracker();
        var now = DateTimeOffset.UtcNow;

        tracker.Record(-15.0, 150.0, timestamp: now);

        var samples = tracker.GetSamples();
        Assert.Single(samples);
        Assert.Equal(0.0, samples[0].GeminiPercentage);
        Assert.Equal(100.0, samples[0].ClaudePercentage);
    }

    [Fact]
    public void IndependentPoolTracking_GeminiFallingWhileClaudeStable()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        // Gemini drops by 15% in 1 hr (-15%/h), Claude stays 100%
        tracker.Record(100.0, 100.0, timestamp: start);
        tracker.Record(85.0, 100.0, timestamp: start.AddHours(1));

        var gRate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddHours(1));
        var cRate = tracker.CalculateBurnRate(QuotaPool.Claude, now: start.AddHours(1));

        Assert.Equal(QuotaTrend.Falling, gRate.Trend);
        Assert.Equal(-15.0, gRate.BurnRatePerHour, precision: 1);
        Assert.Equal("-15%/ч (хватит на ~5ч 40м)", gRate.Formatted(isRussian: true));

        Assert.Equal(QuotaTrend.Steady, cRate.Trend);
        Assert.Equal(0.0, cRate.BurnRatePerHour, precision: 3);
        Assert.Equal("~0%/ч", cRate.Formatted(isRussian: true));
    }

    [Fact]
    public void RecordFromQuotaSnapshot_ExtractsPoolPercentagesAccurately()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        var bG1 = new QuotaBucket(remainingFraction: 1.0);
        var bC1 = new QuotaBucket(remainingFraction: 0.9);
        var snap1 = new QuotaSnapshot(
            groups: new[]
            {
                new QuotaGroup("Gemini Models", "gemini-desc", new[] { bG1 }),
                new QuotaGroup("Claude Models", "claude-desc", new[] { bC1 })
            },
            models: Array.Empty<ModelConfig>(),
            updatedAt: start
        );

        var bG2 = new QuotaBucket(remainingFraction: 0.85);
        var bC2 = new QuotaBucket(remainingFraction: 0.9);
        var snap2 = new QuotaSnapshot(
            groups: new[]
            {
                new QuotaGroup("Gemini Models", "gemini-desc", new[] { bG2 }),
                new QuotaGroup("Claude Models", "claude-desc", new[] { bC2 })
            },
            models: Array.Empty<ModelConfig>(),
            updatedAt: start.AddHours(1)
        );

        tracker.Record(snap1, timestamp: start);
        tracker.Record(snap2, timestamp: start.AddHours(1));

        var gRate = tracker.CalculateBurnRate(QuotaPool.Gemini, now: start.AddHours(1));
        var cRate = tracker.CalculateBurnRate(QuotaPool.Claude, now: start.AddHours(1));

        Assert.Equal(QuotaTrend.Falling, gRate.Trend);
        Assert.Equal(-15.0, gRate.BurnRatePerHour, precision: 1);
        Assert.Equal(QuotaTrend.Steady, cRate.Trend);
    }

    [Fact]
    public void ConvenienceProperties_ReflectGeminiPoolByDefault()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        tracker.Record(100.0, 100.0, timestamp: start);
        tracker.Record(70.0, 100.0, timestamp: start.AddHours(1));

        Assert.Equal(QuotaTrend.Falling, tracker.Trend);
        Assert.Equal(-30.0, tracker.BurnRatePerHour, precision: 1);
        Assert.NotNull(tracker.EstimatedTimeToDepletion);
    }

    [Fact]
    public void SparklineData_EmptyOrSingleSample_ReturnsInsufficientData()
    {
        var tracker = new QuotaHistoryTracker();
        var sparkEmpty = tracker.GetSparklineData(width: 100, height: 30);

        Assert.False(sparkEmpty.HasSufficientData);
        Assert.Empty(sparkEmpty.GeminiPoints);
        Assert.Empty(sparkEmpty.ClaudePoints);

        tracker.Record(90.0, 95.0);
        var sparkSingle = tracker.GetSparklineData(width: 100, height: 30);
        Assert.False(sparkSingle.HasSufficientData);
    }

    [Fact]
    public void SparklineData_ValidSamples_NormalizesCoordinatesCorrectly()
    {
        var tracker = new QuotaHistoryTracker(windowDuration: TimeSpan.FromHours(1));
        var start = DateTimeOffset.UtcNow;

        tracker.Record(geminiPercentage: 100.0, claudePercentage: 0.0, timestamp: start.AddMinutes(-30));
        tracker.Record(geminiPercentage: 50.0, claudePercentage: 50.0, timestamp: start);

        var data = tracker.GetSparklineData(width: 100, height: 40, now: start);

        Assert.True(data.HasSufficientData);
        Assert.True(data.GeminiPoints.Count >= 2);
        Assert.True(data.ClaudePoints.Count >= 2);

        // First point should be at x = 0 (start of data span)
        var firstG = data.GeminiPoints[0];
        Assert.Equal(0.0, firstG.X, precision: 1);
        // 100% quota should map to top (Y near 2.0 with default insets)
        Assert.InRange(firstG.Y, 0.0, 4.0);

        // Claude at 0% should map to bottom (Y near height - inset = 38.0)
        var firstC = data.ClaudePoints[0];
        Assert.Equal(0.0, firstC.X, precision: 1);
        Assert.InRange(firstC.Y, 36.0, 40.0);

        // Later point should have greater X
        var lastG = data.GeminiPoints[^1];
        Assert.True(lastG.X > firstG.X);
    }

    [Fact]
    public void SparklineData_InvalidDimensions_ReturnsEmptyWithoutCrashing()
    {
        var tracker = new QuotaHistoryTracker();
        tracker.Record(100.0, 100.0, timestamp: DateTimeOffset.UtcNow.AddMinutes(-30));
        tracker.Record(80.0, 90.0, timestamp: DateTimeOffset.UtcNow);

        var zeroW = tracker.GetSparklineData(width: 0, height: 40);
        var zeroH = tracker.GetSparklineData(width: 100, height: 0);
        var neg = tracker.GetSparklineData(width: -10, height: -20);

        Assert.False(zeroW.HasSufficientData);
        Assert.False(zeroH.HasSufficientData);
        Assert.False(neg.HasSufficientData);
    }

    [Fact]
    public void SparklineData_ToPathGeometry_GeneratesValidSvgBezierPath()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        tracker.Record(100.0, 100.0, timestamp: start.AddMinutes(-30));
        tracker.Record(85.0, 95.0, timestamp: start.AddMinutes(-15));
        tracker.Record(70.0, 90.0, timestamp: start);

        var data = tracker.GetSparklineData(width: 100, height: 40, now: start);
        var pathData = data.ToPathGeometry(QuotaPool.Gemini);

        Assert.False(string.IsNullOrWhiteSpace(pathData));
        Assert.StartsWith("M", pathData);
        // Should contain cubic Bezier 'C' commands for smooth curves
        Assert.Contains("C", pathData);
    }

    [Fact]
    public void SparklineData_TwoPoints_GeneratesLineSegment()
    {
        var tracker = new QuotaHistoryTracker();
        var start = DateTimeOffset.UtcNow;

        tracker.Record(100.0, 100.0, timestamp: start.AddMinutes(-30));
        tracker.Record(80.0, 80.0, timestamp: start);

        var data = tracker.GetSparklineData(width: 100, height: 40, now: start);
        var pathData = data.ToPathGeometry(QuotaPool.Gemini);

        Assert.StartsWith("M", pathData);
        Assert.Contains("L", pathData);
        Assert.DoesNotContain("C", pathData);
    }

    [Fact]
    public void Clear_RemovesAllSamples()
    {
        var tracker = new QuotaHistoryTracker();
        tracker.Record(100.0, 100.0);
        tracker.Record(90.0, 95.0);
        Assert.Equal(2, tracker.Count);

        tracker.Clear();

        Assert.Equal(0, tracker.Count);
        Assert.Empty(tracker.GetSamples());
        Assert.Equal(QuotaTrend.Steady, tracker.Trend);
    }

    [Fact]
    public void ThreadSafety_ConcurrentRecordsAndReads_DoesNotThrow()
    {
        var tracker = new QuotaHistoryTracker();
        var baseTime = DateTimeOffset.UtcNow;

        System.Threading.Tasks.Parallel.For(0, 100, i =>
        {
            tracker.Record(100.0 - (i % 20), 100.0 - (i % 30), baseTime.AddSeconds(i));
            _ = tracker.CalculateBurnRate(QuotaPool.Gemini, baseTime.AddSeconds(i));
            _ = tracker.CalculateBurnRate(QuotaPool.Claude, baseTime.AddSeconds(i));
            _ = tracker.GetSparklineData(100, 30, baseTime.AddSeconds(i));
            _ = tracker.Count;
        });

        Assert.True(tracker.Count > 0);
    }
}
