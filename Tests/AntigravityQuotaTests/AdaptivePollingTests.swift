import XCTest
@testable import AntigravityQuotaCore

final class AdaptivePollingTests: XCTestCase {

    func testIntervalConstants() {
        XCTAssertEqual(AdaptivePollPolicy.activeInterval, 20.0)
        XCTAssertEqual(AdaptivePollPolicy.backgroundInterval, 60.0)
        XCTAssertEqual(AdaptivePollPolicy.offlineInterval, 120.0)
    }

    func testIntervalForState() {
        XCTAssertEqual(AdaptivePollPolicy.interval(for: .activeForeground), 20.0)
        XCTAssertEqual(AdaptivePollPolicy.interval(for: .background), 60.0)
        XCTAssertEqual(AdaptivePollPolicy.interval(for: .offlineOrClosed), 120.0)
    }

    func testDetermineStateWhenActiveForegroundAndServerRunning() {
        let state = AdaptivePollPolicy.determineState(
            isAntigravityRunning: true,
            isAntigravityFrontmost: true,
            isLanguageServerRunning: true
        )
        XCTAssertEqual(state, .activeForeground)
        XCTAssertEqual(AdaptivePollPolicy.interval(for: state), 20.0)
    }

    func testDetermineStateWhenBackgroundAndServerRunning() {
        let state = AdaptivePollPolicy.determineState(
            isAntigravityRunning: true,
            isAntigravityFrontmost: false,
            isLanguageServerRunning: true
        )
        XCTAssertEqual(state, .background)
        XCTAssertEqual(AdaptivePollPolicy.interval(for: state), 60.0)
    }

    func testDetermineStateWhenLanguageServerNotRunning() {
        // Even if Antigravity is frontmost, if language_server is down, poll at offline rate (120s)
        let state = AdaptivePollPolicy.determineState(
            isAntigravityRunning: true,
            isAntigravityFrontmost: true,
            isLanguageServerRunning: false
        )
        XCTAssertEqual(state, .offlineOrClosed)
        XCTAssertEqual(AdaptivePollPolicy.interval(for: state), 120.0)
    }

    func testDetermineStateWhenAntigravityClosed() {
        let state = AdaptivePollPolicy.determineState(
            isAntigravityRunning: false,
            isAntigravityFrontmost: false,
            isLanguageServerRunning: false
        )
        XCTAssertEqual(state, .offlineOrClosed)
        XCTAssertEqual(AdaptivePollPolicy.interval(for: state), 120.0)
    }

    func testSchedulerTracksTransitionsAndReportsChanges() {
        let scheduler = AdaptivePollScheduler(initialState: .offlineOrClosed)
        XCTAssertEqual(scheduler.currentState, .offlineOrClosed)
        XCTAssertEqual(scheduler.currentInterval, 120.0)

        // Launch Antigravity in foreground with server up -> transitions to 20s
        let t1 = scheduler.update(
            isAntigravityRunning: true,
            isAntigravityFrontmost: true,
            isLanguageServerRunning: true
        )
        XCTAssertTrue(t1.didChange)
        XCTAssertEqual(t1.state, .activeForeground)
        XCTAssertEqual(t1.interval, 20.0)
        XCTAssertEqual(scheduler.currentInterval, 20.0)

        // Same state update -> didChange should be false
        let t2 = scheduler.update(
            isAntigravityRunning: true,
            isAntigravityFrontmost: true,
            isLanguageServerRunning: true
        )
        XCTAssertFalse(t2.didChange)
        XCTAssertEqual(t2.interval, 20.0)

        // Switch to background (e.g. browser) -> transitions to 60s
        let t3 = scheduler.update(
            isAntigravityRunning: true,
            isAntigravityFrontmost: false,
            isLanguageServerRunning: true
        )
        XCTAssertTrue(t3.didChange)
        XCTAssertEqual(t3.state, .background)
        XCTAssertEqual(t3.interval, 60.0)
        XCTAssertEqual(scheduler.currentInterval, 60.0)

        // Antigravity closed -> transitions to 120s
        let t4 = scheduler.update(
            isAntigravityRunning: false,
            isAntigravityFrontmost: false,
            isLanguageServerRunning: false
        )
        XCTAssertTrue(t4.didChange)
        XCTAssertEqual(t4.state, .offlineOrClosed)
        XCTAssertEqual(t4.interval, 120.0)
        XCTAssertEqual(scheduler.currentInterval, 120.0)
    }

    func testIsAntigravityAppDetection() {
        XCTAssertTrue(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: "com.google.antigravity", localizedName: "Antigravity"))
        XCTAssertTrue(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: "com.google.Antigravity", localizedName: nil))
        XCTAssertTrue(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: nil, localizedName: "Antigravity"))
        XCTAssertTrue(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: "org.example.antigravity.helper", localizedName: "Helper"))

        XCTAssertFalse(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: "com.apple.Safari", localizedName: "Safari"))
        XCTAssertFalse(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: "com.apple.finder", localizedName: "Finder"))
        XCTAssertFalse(AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: nil, localizedName: nil))
    }
}
