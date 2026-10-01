import XCTest
import CoreGraphics
@testable import AntigravityQuotaCore

final class QuotaSparklineTests: XCTestCase {

    func testEmptySamplesReturnsNotSufficientDataAndDefaultWindow() {
        let now = Date()
        let data = QuotaSparklineBuilder.build(from: [], now: now)

        XCTAssertFalse(data.hasSufficientData)
        XCTAssertEqual(data.windowDuration, 3600.0) // min 1 hour
        XCTAssertTrue(data.geminiPoints.isEmpty)
        XCTAssertTrue(data.claudePoints.isEmpty)
    }

    func testSingleSampleReturnsNotSufficientData() {
        let now = Date()
        let sample = QuotaHistorySample(timestamp: now.addingTimeInterval(-300), geminiPercentage: 90.0, claudePercentage: 80.0)
        let data = QuotaSparklineBuilder.build(from: [sample], now: now)

        XCTAssertFalse(data.hasSufficientData)
        XCTAssertEqual(data.windowDuration, 3600.0)
    }

    func testTwoSamplesWithinShortIntervalLessThanFiveSecondsReturnsNotSufficientData() {
        let now = Date()
        let s1 = QuotaHistorySample(timestamp: now.addingTimeInterval(-3), geminiPercentage: 100.0, claudePercentage: 100.0)
        let s2 = QuotaHistorySample(timestamp: now.addingTimeInterval(-1), geminiPercentage: 100.0, claudePercentage: 100.0)
        let data = QuotaSparklineBuilder.build(from: [s1, s2], now: now)

        XCTAssertFalse(data.hasSufficientData)
    }

    func testTwoValidSamplesBuildsSufficientData() {
        let now = Date()
        let s1 = QuotaHistorySample(timestamp: now.addingTimeInterval(-1800), geminiPercentage: 100.0, claudePercentage: 95.0)
        let s2 = QuotaHistorySample(timestamp: now.addingTimeInterval(-60), geminiPercentage: 85.0, claudePercentage: 90.0)
        let data = QuotaSparklineBuilder.build(from: [s1, s2], now: now)

        XCTAssertTrue(data.hasSufficientData)
        XCTAssertEqual(data.windowDuration, 3600.0) // 30 min span scales up to min 1 hour

        // Gemini points: s1, s2, and extended to now
        XCTAssertEqual(data.geminiPoints.count, 3)
        XCTAssertEqual(data.geminiPoints[0].percentage, 100.0)
        XCTAssertEqual(data.geminiPoints[1].percentage, 85.0)
        XCTAssertEqual(data.geminiPoints[2].percentage, 85.0) // extended to now
        XCTAssertEqual(data.geminiPoints[2].timestamp.timeIntervalSince(now), 0, accuracy: 0.1)

        // Claude points: s1, s2, and extended to now
        XCTAssertEqual(data.claudePoints.count, 3)
        XCTAssertEqual(data.claudePoints[0].percentage, 95.0)
        XCTAssertEqual(data.claudePoints[1].percentage, 90.0)
        XCTAssertEqual(data.claudePoints[2].percentage, 90.0)
    }

    func testWindowDurationScalesBetweenOneAndFiveHours() {
        let now = Date()

        // 1. Span of 2 hours (7200s) -> window should be 7200s
        let s2h = [
            QuotaHistorySample(timestamp: now.addingTimeInterval(-7200), geminiPercentage: 100.0, claudePercentage: 100.0),
            QuotaHistorySample(timestamp: now.addingTimeInterval(-3600), geminiPercentage: 80.0, claudePercentage: 90.0),
            QuotaHistorySample(timestamp: now.addingTimeInterval(-60), geminiPercentage: 60.0, claudePercentage: 80.0)
        ]
        let data2h = QuotaSparklineBuilder.build(from: s2h, now: now)
        XCTAssertEqual(data2h.windowDuration, 7200.0, accuracy: 1.0)

        // 2. Span of 6 hours (21600s) -> samples older than 5 hours (18000s) pruned, window capped at 18000s
        let s6h = [
            QuotaHistorySample(timestamp: now.addingTimeInterval(-21600), geminiPercentage: 100.0, claudePercentage: 100.0), // 6h ago (pruned)
            QuotaHistorySample(timestamp: now.addingTimeInterval(-18000), geminiPercentage: 90.0, claudePercentage: 90.0), // 5h ago
            QuotaHistorySample(timestamp: now.addingTimeInterval(-14400), geminiPercentage: 80.0, claudePercentage: 85.0), // 4h ago
            QuotaHistorySample(timestamp: now.addingTimeInterval(-3600), geminiPercentage: 50.0, claudePercentage: 70.0),
            QuotaHistorySample(timestamp: now.addingTimeInterval(-30), geminiPercentage: 40.0, claudePercentage: 65.0)
        ]
        let data6h = QuotaSparklineBuilder.build(from: s6h, now: now)
        XCTAssertEqual(data6h.windowDuration, 18000.0, accuracy: 1.0)
        // Earliest point in data6h should be the 5h ago sample, not 6h ago
        XCTAssertEqual(data6h.geminiPoints.first?.percentage, 90.0)
    }

    func testNormalizedPointsCalculation() {
        let now = Date()
        let window: TimeInterval = 3600.0 // 1 hour
        let windowStart = now.addingTimeInterval(-window)

        let points = [
            QuotaSparklinePoint(timestamp: windowStart, percentage: 100.0),
            QuotaSparklinePoint(timestamp: windowStart.addingTimeInterval(1800), percentage: 50.0),
            QuotaSparklinePoint(timestamp: now, percentage: 0.0)
        ]

        let canvasSize = CGSize(width: 200, height: 40)
        let coords = QuotaSparklineBuilder.normalizedPoints(for: points, in: canvasSize, windowStart: windowStart, windowDuration: window)

        XCTAssertEqual(coords.count, 3)

        // First point: at windowStart (x=0), percentage 100% (y = topInset = 2)
        XCTAssertEqual(coords[0].x, 0.0, accuracy: 0.1)
        XCTAssertEqual(coords[0].y, 2.0, accuracy: 0.1)

        // Second point: at windowStart + 1800s (x = 100), percentage 50% (y = 2 + (40-4)*0.5 = 20)
        XCTAssertEqual(coords[1].x, 100.0, accuracy: 0.1)
        XCTAssertEqual(coords[1].y, 20.0, accuracy: 0.1)

        // Third point: at now (x = 200), percentage 0% (y = 2 + 36 = 38)
        XCTAssertEqual(coords[2].x, 200.0, accuracy: 0.1)
        XCTAssertEqual(coords[2].y, 38.0, accuracy: 0.1)
    }

    func testClampsPercentageBetweenZeroAndHundred() {
        let now = Date()
        let s1 = QuotaHistorySample(timestamp: now.addingTimeInterval(-1000), geminiPercentage: 120.0, claudePercentage: -10.0)
        let s2 = QuotaHistorySample(timestamp: now.addingTimeInterval(-10), geminiPercentage: -5.0, claudePercentage: 150.0)

        let data = QuotaSparklineBuilder.build(from: [s1, s2], now: now)
        XCTAssertEqual(data.geminiPoints[0].percentage, 100.0)
        XCTAssertEqual(data.geminiPoints[1].percentage, 0.0)
        XCTAssertEqual(data.claudePoints[0].percentage, 0.0)
        XCTAssertEqual(data.claudePoints[1].percentage, 100.0)
    }

    func testTrackerSparklineDataIntegration() {
        let tracker = QuotaHistoryTracker()
        let now = Date()

        tracker.record(gemini: 95.0, claude: 90.0, at: now.addingTimeInterval(-1800))
        tracker.record(gemini: 80.0, claude: 85.0, at: now)

        let sparkData = tracker.sparklineData(now: now)
        XCTAssertTrue(sparkData.hasSufficientData)
        XCTAssertEqual(sparkData.geminiPoints.count, 2)
        XCTAssertEqual(sparkData.claudePoints.count, 2)
    }

    func testNormalizedPointsEmptyOrZeroSize() {
        let now = Date()
        let emptyCoords = QuotaSparklineBuilder.normalizedPoints(
            for: [],
            in: CGSize(width: 100, height: 40),
            windowStart: now.addingTimeInterval(-3600),
            windowDuration: 3600
        )
        XCTAssertTrue(emptyCoords.isEmpty)

        let zeroCoords = QuotaSparklineBuilder.normalizedPoints(
            for: [QuotaSparklinePoint(timestamp: now, percentage: 50.0)],
            in: .zero,
            windowStart: now.addingTimeInterval(-3600),
            windowDuration: 3600
        )
        XCTAssertTrue(zeroCoords.isEmpty)
    }

    func testSparklineLocalizationKeys() {
        XCTAssertFalse(Localization.showSparklineChart.isEmpty)
        XCTAssertFalse(Localization.hideSparklineChart.isEmpty)
        XCTAssertFalse(Localization.showSparklineInHUD.isEmpty)
    }
}

