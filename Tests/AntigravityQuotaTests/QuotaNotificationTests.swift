import XCTest
import Foundation
@testable import AntigravityQuotaCore

final class QuotaNotificationTests: XCTestCase {

    private func makeGroup(name: String, bucketId: String, fraction: Double) -> QuotaGroup {
        QuotaGroup(
            displayName: name,
            description: "Test group",
            buckets: [
                QuotaBucket(
                    bucketId: bucketId,
                    window: "5h",
                    remainingFraction: fraction,
                    resetDate: Date().addingTimeInterval(3600)
                )
            ]
        )
    }

    private func makeSnapshot(geminiFraction: Double, claudeFraction: Double) -> QuotaSnapshot {
        QuotaSnapshot(
            groups: [
                makeGroup(name: "Gemini Models", bucketId: "gemini-5h", fraction: geminiFraction),
                makeGroup(name: "Claude and GPT models", bucketId: "3p-5h", fraction: claudeFraction)
            ],
            models: []
        )
    }

    func testBaselineSnapshotDoesNotFireNotificationsOnLaunch() {
        let evaluator = QuotaNotificationEvaluator()
        let initialSnapshot = makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.08)

        // The first snapshot sets baseline state and must not trigger alert spam
        let events = evaluator.evaluate(snapshot: initialSnapshot)
        XCTAssertTrue(events.isEmpty, "Initial baseline snapshot should not trigger notifications")
    }

    func testResetNotificationFiresWhenTransitioningFromBelow100To100() {
        let evaluator = QuotaNotificationEvaluator()

        // 1. Baseline: Gemini is at 60%, Claude at 100%
        _ = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 0.60, claudeFraction: 1.0))

        // 2. Gemini resets to 100%
        let events = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 1.0))

        XCTAssertEqual(events.count, 1)
        if case .reset(let poolName) = events.first {
            XCTAssertEqual(poolName, "Gemini")
        } else {
            XCTFail("Expected .reset event for Gemini, got \(events)")
        }
    }

    func testResetNotificationDoesNotDuplicateOnSubsequent100PercentPolls() {
        let evaluator = QuotaNotificationEvaluator()

        // 1. Gemini is below 100%
        _ = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 0.85, claudeFraction: 1.0))

        // 2. Gemini resets to 100% -> Event 1
        let events1 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 1.0))
        XCTAssertEqual(events1.count, 1)

        // 3. Subsequent poll with Gemini still at 100% -> Suppressed (no duplicate)
        let events2 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 1.0))
        XCTAssertTrue(events2.isEmpty, "Repeated 100% polls must be suppressed")
    }

    func testLowQuotaWarningFiresWhenDroppingBelow10Percent() {
        let evaluator = QuotaNotificationEvaluator()

        // 1. Claude is at 25%
        _ = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.25))

        // 2. Claude drops to 8% (below 10%)
        let events = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.08))

        XCTAssertEqual(events.count, 1)
        if case .lowQuota(let poolName, let percentage) = events.first {
            XCTAssertEqual(poolName, "Claude")
            XCTAssertEqual(percentage, 8.0, accuracy: 0.1)
        } else {
            XCTFail("Expected .lowQuota event for Claude, got \(events)")
        }
    }

    func testLowQuotaWarningDoesNotDuplicateWhileStayingBelow10Percent() {
        let evaluator = QuotaNotificationEvaluator()

        // 1. Claude starts at 20%
        _ = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.20))

        // 2. Claude drops to 9% -> Warning fires
        let events1 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.09))
        XCTAssertEqual(events1.count, 1)

        // 3. Claude stays at 8% -> Suppressed (no repeated alert)
        let events2 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.08))
        XCTAssertTrue(events2.isEmpty, "Repeated readings below 10% must not spam alerts")
    }

    func testLowQuotaWarningFiresSecondaryAlertWhenDroppingBelow5Percent() {
        let evaluator = QuotaNotificationEvaluator()

        // 1. Claude starts at 20%
        _ = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.20))

        // 2. Claude drops to 9% -> Warning 1 (10% threshold)
        let events1 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.09))
        XCTAssertEqual(events1.count, 1)

        // 3. Claude drops to 4% -> Warning 2 (critical 5% threshold)
        let events2 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.04))
        XCTAssertEqual(events2.count, 1)
        if case .lowQuota(let poolName, let percentage) = events2.first {
            XCTAssertEqual(poolName, "Claude")
            XCTAssertEqual(percentage, 4.0, accuracy: 0.1)
        } else {
            XCTFail("Expected .lowQuota for 4%, got \(events2)")
        }

        // 4. Stays at 3% -> Suppressed
        let events3 = evaluator.evaluate(snapshot: makeSnapshot(geminiFraction: 1.0, claudeFraction: 0.03))
        XCTAssertTrue(events3.isEmpty)
    }

    func testLocalizedNotificationMessagesRussianAndEnglish() {
        let ruResetTitle = Localization.notificationsResetTitle(isRussian: true)
        let enResetTitle = Localization.notificationsResetTitle(isRussian: false)
        XCTAssertEqual(ruResetTitle, "Квоты сброшены")
        XCTAssertEqual(enResetTitle, "Quotas Reset")

        let ruResetBody = Localization.notificationsResetBody(pool: "Gemini", isRussian: true)
        let enResetBody = Localization.notificationsResetBody(pool: "Gemini", isRussian: false)
        XCTAssertTrue(ruResetBody.contains("Gemini"))
        XCTAssertTrue(ruResetBody.contains("100%"))
        XCTAssertTrue(enResetBody.contains("Gemini"))
        XCTAssertTrue(enResetBody.contains("100%"))

        let ruLowTitle = Localization.notificationsLowTitle(isRussian: true)
        let enLowTitle = Localization.notificationsLowTitle(isRussian: false)
        XCTAssertEqual(ruLowTitle, "Низкий остаток квоты")
        XCTAssertEqual(enLowTitle, "Low Quota Warning")

        let ruLowBody = Localization.notificationsLowBody(pool: "Claude", percent: 8.0, isRussian: true)
        let enLowBody = Localization.notificationsLowBody(pool: "Claude", percent: 8.0, isRussian: false)
        XCTAssertTrue(ruLowBody.contains("Claude"))
        XCTAssertTrue(ruLowBody.contains("8%"))
        XCTAssertTrue(enLowBody.contains("Claude"))
        XCTAssertTrue(enLowBody.contains("8%"))
    }

    func testSoundAlertDecisionForResetEventPlaysGlass() {
        let event = QuotaNotificationEvent.reset(poolName: "Gemini")
        let sound = QuotaSoundDecision.soundToPlay(
            for: event,
            soundAlertsEnabled: true,
            isDoNotDisturbActive: false
        )
        XCTAssertEqual(sound, .glass)
        XCTAssertEqual(sound?.systemSoundNames, ["Glass"])
    }

    func testSoundAlertDecisionForCriticalLowQuotaBelow5PercentPlaysSubmarineOrSosumi() {
        let event = QuotaNotificationEvent.lowQuota(poolName: "Claude", remainingPercentage: 4.5)
        let sound = QuotaSoundDecision.soundToPlay(
            for: event,
            soundAlertsEnabled: true,
            isDoNotDisturbActive: false
        )
        XCTAssertEqual(sound, .submarineOrSosumi)
        XCTAssertEqual(sound?.systemSoundNames, ["Submarine", "Sosumi"])
    }

    func testSoundAlertDecisionForNonCriticalLowQuotaAbove5PercentDoesNotPlaySound() {
        let event = QuotaNotificationEvent.lowQuota(poolName: "Claude", remainingPercentage: 8.0)
        let sound = QuotaSoundDecision.soundToPlay(
            for: event,
            soundAlertsEnabled: true,
            isDoNotDisturbActive: false
        )
        XCTAssertNil(sound, "Quotas above 5% must not trigger critical sound alerts")
    }

    func testSoundAlertsDisabledByUserSuppressesAllSounds() {
        let resetEvent = QuotaNotificationEvent.reset(poolName: "Gemini")
        let lowEvent = QuotaNotificationEvent.lowQuota(poolName: "Claude", remainingPercentage: 3.0)

        let resetSound = QuotaSoundDecision.soundToPlay(
            for: resetEvent,
            soundAlertsEnabled: false,
            isDoNotDisturbActive: false
        )
        let lowSound = QuotaSoundDecision.soundToPlay(
            for: lowEvent,
            soundAlertsEnabled: false,
            isDoNotDisturbActive: false
        )

        XCTAssertNil(resetSound, "Sound alerts disabled by user must suppress reset sound")
        XCTAssertNil(lowSound, "Sound alerts disabled by user must suppress critical sound")
    }

    func testDoNotDisturbActiveSuppressesAllSounds() {
        let resetEvent = QuotaNotificationEvent.reset(poolName: "Gemini")
        let lowEvent = QuotaNotificationEvent.lowQuota(poolName: "Claude", remainingPercentage: 2.0)

        let resetSound = QuotaSoundDecision.soundToPlay(
            for: resetEvent,
            soundAlertsEnabled: true,
            isDoNotDisturbActive: true
        )
        let lowSound = QuotaSoundDecision.soundToPlay(
            for: lowEvent,
            soundAlertsEnabled: true,
            isDoNotDisturbActive: true
        )

        XCTAssertNil(resetSound, "Active Do Not Disturb must suppress reset sound")
        XCTAssertNil(lowSound, "Active Do Not Disturb must suppress critical sound")
    }

    func testSoundAlertsLocalizationRussianAndEnglish() {
        let ruMenuItem = Localization.soundAlertsMenuItem(isRussian: true)
        let enMenuItem = Localization.soundAlertsMenuItem(isRussian: false)
        XCTAssertEqual(ruMenuItem, "Звуковые сигналы")
        XCTAssertEqual(enMenuItem, "Sound Alerts")

        let ruAboutSetting = Localization.aboutSettingSoundAlerts(isRussian: true)
        let enAboutSetting = Localization.aboutSettingSoundAlerts(isRussian: false)
        XCTAssertTrue(ruAboutSetting.contains("Звуковые сигналы"))
        XCTAssertTrue(ruAboutSetting.contains("Glass"))
        XCTAssertTrue(ruAboutSetting.contains("Submarine"))
        XCTAssertTrue(enAboutSetting.contains("Sound Alerts") || enAboutSetting.contains("sound alerts"))
        XCTAssertTrue(enAboutSetting.contains("Glass"))
        XCTAssertTrue(enAboutSetting.contains("Submarine"))
    }
}

