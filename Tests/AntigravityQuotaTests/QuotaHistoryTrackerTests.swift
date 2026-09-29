import XCTest
@testable import AntigravityQuotaCore

final class QuotaHistoryTrackerTests: XCTestCase {

    func testEmptyHistoryReturnsStableZeroBurnRate() {
        let tracker = QuotaHistoryTracker()
        let rate = tracker.burnRate(for: .gemini)

        XCTAssertEqual(rate.trend, .stable)
        XCTAssertEqual(rate.ratePerHour, 0.0, accuracy: 0.001)
        XCTAssertNil(rate.estimatedTimeToDepletion)
        XCTAssertEqual(rate.formatted(isRussian: true), "~0%/ч")
        XCTAssertEqual(rate.formatted(isRussian: false), "~0%/h")
    }

    func testSingleSampleReturnsStableZeroBurnRate() {
        let tracker = QuotaHistoryTracker()
        let now = Date()
        tracker.record(gemini: 95.0, claude: 90.0, at: now)

        let gRate = tracker.burnRate(for: .gemini, now: now)
        XCTAssertEqual(gRate.trend, .stable)
        XCTAssertEqual(gRate.ratePerHour, 0.0, accuracy: 0.001)
        XCTAssertNil(gRate.estimatedTimeToDepletion)
    }

    func testInsufficientElapsedTimeReturnsStable() {
        let tracker = QuotaHistoryTracker()
        let start = Date()
        tracker.record(gemini: 100.0, claude: 100.0, at: start)
        tracker.record(gemini: 95.0, claude: 100.0, at: start.addingTimeInterval(5.0)) // only 5s

        let rate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(5.0))
        XCTAssertEqual(rate.trend, .stable)
        XCTAssertEqual(rate.ratePerHour, 0.0, accuracy: 0.001)
        XCTAssertNil(rate.estimatedTimeToDepletion)
    }

    func testConstantQuotaReturnsStableZeroBurnRate() {
        let tracker = QuotaHistoryTracker()
        let start = Date()
        tracker.record(gemini: 100.0, claude: 100.0, at: start)
        tracker.record(gemini: 100.0, claude: 100.0, at: start.addingTimeInterval(1800.0))

        let rate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(1800.0))
        XCTAssertEqual(rate.trend, .stable)
        XCTAssertEqual(rate.ratePerHour, 0.0, accuracy: 0.001)
        XCTAssertNil(rate.estimatedTimeToDepletion)
        XCTAssertEqual(rate.formatted(isRussian: true), "~0%/ч")
    }

    func testSteadyBurnRateAndDepletionCalculation() {
        let tracker = QuotaHistoryTracker()
        let start = Date()
        // 100% down to 70% in 1 hour (3600 seconds) -> -30%/h
        tracker.record(gemini: 100.0, claude: 100.0, at: start)
        tracker.record(gemini: 70.0, claude: 100.0, at: start.addingTimeInterval(3600.0))

        let rate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(3600.0))
        XCTAssertEqual(rate.trend, .burning)
        XCTAssertEqual(rate.ratePerHour, -30.0, accuracy: 0.1)

        // 70% remaining at 30%/h = 2h 20m = 8400 seconds
        XCTAssertNotNil(rate.estimatedTimeToDepletion)
        if let time = rate.estimatedTimeToDepletion {
            XCTAssertEqual(time, 8400.0, accuracy: 5.0)
        }

        XCTAssertEqual(rate.formatted(isRussian: true), "-30%/ч (хватит на ~2ч 20м)")
        XCTAssertEqual(rate.formatted(isRussian: false), "-30%/h (~2h 20m left)")
    }

    func testRapidBurnRateOverShortPeriod() {
        let tracker = QuotaHistoryTracker()
        let start = Date()
        // Drops from 80% to 70% in 10 minutes (600s) -> -10% in 1/6 hr = -60%/hr
        tracker.record(gemini: 80.0, claude: 100.0, at: start)
        tracker.record(gemini: 70.0, claude: 100.0, at: start.addingTimeInterval(600.0))

        let rate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(600.0))
        XCTAssertEqual(rate.trend, .burning)
        XCTAssertEqual(rate.ratePerHour, -60.0, accuracy: 0.5)

        // 70% at 60%/hr = 1h 10m = 4200 seconds
        XCTAssertNotNil(rate.estimatedTimeToDepletion)
        if let time = rate.estimatedTimeToDepletion {
            XCTAssertEqual(time, 4200.0, accuracy: 10.0)
        }
        XCTAssertEqual(rate.formatted(isRussian: true), "-60%/ч (хватит на ~1ч 10м)")
    }

    func testQuotaResetShowsRecoveringTrend() {
        let tracker = QuotaHistoryTracker()
        let start = Date()
        // Quota was at 10%, resets to 100% 5 minutes later
        tracker.record(gemini: 10.0, claude: 10.0, at: start)
        tracker.record(gemini: 100.0, claude: 10.0, at: start.addingTimeInterval(300.0))

        let rate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(300.0))
        XCTAssertEqual(rate.trend, .recovering)
        XCTAssertGreaterThan(rate.ratePerHour, 0.0)
        XCTAssertNil(rate.estimatedTimeToDepletion)
        XCTAssertTrue(rate.formatted(isRussian: true).contains("сброс"))
        XCTAssertTrue(rate.formatted(isRussian: false).contains("reset"))
    }

    func testBurnRateCalculationAfterReset() {
        let tracker = QuotaHistoryTracker()
        let start = Date()
        // History contains old session before reset, then a reset at +600s, then burning from 100% to 90% at +1800s (20m)
        tracker.record(gemini: 25.0, claude: nil, at: start)
        tracker.record(gemini: 15.0, claude: nil, at: start.addingTimeInterval(300.0))
        tracker.record(gemini: 100.0, claude: nil, at: start.addingTimeInterval(600.0)) // Reset
        tracker.record(gemini: 90.0, claude: nil, at: start.addingTimeInterval(1800.0)) // Burned 10% in 1200s (20m)

        let rate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(1800.0))
        // Since reset at +600s: -10% in 1200s (1/3 hr) -> -30%/h
        XCTAssertEqual(rate.trend, .burning)
        XCTAssertEqual(rate.ratePerHour, -30.0, accuracy: 0.5)
        XCTAssertNotNil(rate.estimatedTimeToDepletion)
    }

    func testSlidingBufferPrunesOldSamples() {
        let tracker = QuotaHistoryTracker(windowDuration: 3600.0)
        let start = Date()

        // Old samples from 2 hours ago
        tracker.record(gemini: 100.0, claude: 100.0, at: start.addingTimeInterval(-7200.0))
        tracker.record(gemini: 95.0, claude: 100.0, at: start.addingTimeInterval(-5000.0))

        // New samples within 1 hour
        tracker.record(gemini: 80.0, claude: 100.0, at: start.addingTimeInterval(-1800.0))
        tracker.record(gemini: 70.0, claude: 100.0, at: start)

        XCTAssertEqual(tracker.sampleCount, 2) // older 2 pruned
        let rate = tracker.burnRate(for: .gemini, now: start)
        // From 80% to 70% in 1800s (0.5 hr) -> -20%/h
        XCTAssertEqual(rate.ratePerHour, -20.0, accuracy: 0.5)
        XCTAssertEqual(rate.trend, .burning)
    }

    func testIndependentPoolTracking() {
        let tracker = QuotaHistoryTracker()
        let start = Date()

        // Gemini drops by 15% in 3600s (-15%/h), Claude stays 100%
        tracker.record(gemini: 100.0, claude: 100.0, at: start)
        tracker.record(gemini: 85.0, claude: 100.0, at: start.addingTimeInterval(3600.0))

        let gRate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(3600.0))
        let cRate = tracker.burnRate(for: .claude, now: start.addingTimeInterval(3600.0))

        XCTAssertEqual(gRate.trend, .burning)
        XCTAssertEqual(gRate.ratePerHour, -15.0, accuracy: 0.1)
        XCTAssertEqual(gRate.formatted(isRussian: true), "-15%/ч (хватит на ~5ч 40м)")

        XCTAssertEqual(cRate.trend, .stable)
        XCTAssertEqual(cRate.ratePerHour, 0.0, accuracy: 0.001)
        XCTAssertEqual(cRate.formatted(isRussian: true), "~0%/ч")
    }

    func testRecordFromQuotaSnapshot() {
        let tracker = QuotaHistoryTracker()
        let start = Date()

        let bG = QuotaBucket(bucketId: "gemini-5h", window: "5h", remainingFraction: 1.0, resetDate: nil)
        let bC = QuotaBucket(bucketId: "claude-5h", window: "5h", remainingFraction: 0.9, resetDate: nil)
        let snap1 = QuotaSnapshot(
            groups: [
                QuotaGroup(displayName: "Gemini Models", description: "", buckets: [bG]),
                QuotaGroup(displayName: "Claude & GPT", description: "", buckets: [bC])
            ],
            models: [],
            updatedAt: start
        )

        let bG2 = QuotaBucket(bucketId: "gemini-5h", window: "5h", remainingFraction: 0.85, resetDate: nil)
        let bC2 = QuotaBucket(bucketId: "claude-5h", window: "5h", remainingFraction: 0.9, resetDate: nil)
        let snap2 = QuotaSnapshot(
            groups: [
                QuotaGroup(displayName: "Gemini Models", description: "", buckets: [bG2]),
                QuotaGroup(displayName: "Claude & GPT", description: "", buckets: [bC2])
            ],
            models: [],
            updatedAt: start.addingTimeInterval(3600.0)
        )

        tracker.record(snapshot: snap1, at: start)
        tracker.record(snapshot: snap2, at: start.addingTimeInterval(3600.0))

        let gRate = tracker.burnRate(for: .gemini, now: start.addingTimeInterval(3600.0))
        let cRate = tracker.burnRate(for: .claude, now: start.addingTimeInterval(3600.0))

        XCTAssertEqual(gRate.trend, .burning)
        XCTAssertEqual(gRate.ratePerHour, -15.0, accuracy: 0.1)
        XCTAssertEqual(cRate.trend, .stable)
    }

    func testFormattedHoursOnlyOrMinutesOnly() {
        // Exactly 1 hour left
        let rate1h = QuotaBurnRate(ratePerHour: -50.0, trend: .burning, estimatedTimeToDepletion: 3600.0)
        XCTAssertEqual(rate1h.formatted(isRussian: true), "-50%/ч (хватит на ~1ч)")
        XCTAssertEqual(rate1h.formatted(isRussian: false), "-50%/h (~1h left)")

        // 25 minutes left
        let rate25m = QuotaBurnRate(ratePerHour: -40.0, trend: .burning, estimatedTimeToDepletion: 1500.0)
        XCTAssertEqual(rate25m.formatted(isRussian: true), "-40%/ч (хватит на ~25м)")
        XCTAssertEqual(rate25m.formatted(isRussian: false), "-40%/h (~25m left)")
    }
}
