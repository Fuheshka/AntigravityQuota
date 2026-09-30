import XCTest
@testable import AntigravityQuotaCore

final class UpdateCheckerTests: XCTestCase {

    func testSemVerComparison() {
        XCTAssertTrue(SemVer.isCandidate("1.2.0", newerThan: "1.1.0"))
        XCTAssertTrue(SemVer.isCandidate("v1.2.0", newerThan: "1.1.0"))
        XCTAssertTrue(SemVer.isCandidate("v1.10.0", newerThan: "v1.9.5"))
        XCTAssertTrue(SemVer.isCandidate("2.0.0", newerThan: "1.99.99"))
        XCTAssertTrue(SemVer.isCandidate("1.1.1", newerThan: "1.1.0"))
        XCTAssertTrue(SemVer.isCandidate("1.2", newerThan: "1.1.9"))

        // Equal or older versions
        XCTAssertFalse(SemVer.isCandidate("1.1.0", newerThan: "1.1.0"))
        XCTAssertFalse(SemVer.isCandidate("v1.1.0", newerThan: "1.1.0"))
        XCTAssertFalse(SemVer.isCandidate("1.0.9", newerThan: "1.1.0"))
        XCTAssertFalse(SemVer.isCandidate("0.9.0", newerThan: "1.0.0"))
    }

    func testPlatformAssetMatching() {
        let assets = [
            GitHubAsset(name: "AntigravityQuota-v1.2.0.zip", browserDownloadUrl: URL(string: "https://example.com/download.zip")!),
            GitHubAsset(name: "AntigravityQuota-v1.2.0.dmg", browserDownloadUrl: URL(string: "https://example.com/download.dmg")!),
            GitHubAsset(name: "AntigravityQuota-v1.2.0-setup.exe", browserDownloadUrl: URL(string: "https://example.com/download.exe")!),
            GitHubAsset(name: "AntigravityQuota-v1.2.0.AppImage", browserDownloadUrl: URL(string: "https://example.com/download.AppImage")!),
            GitHubAsset(name: "AntigravityQuota-v1.2.0.apk", browserDownloadUrl: URL(string: "https://example.com/download.apk")!)
        ]

        // macOS: prefers .dmg over .zip
        let macAsset = UpdateChecker.findBestAsset(in: assets, for: .macOS)
        XCTAssertEqual(macAsset?.name, "AntigravityQuota-v1.2.0.dmg")

        // Windows: prefers .exe
        let winAsset = UpdateChecker.findBestAsset(in: assets, for: .windows)
        XCTAssertEqual(winAsset?.name, "AntigravityQuota-v1.2.0-setup.exe")

        // Linux: prefers .AppImage
        let linuxAsset = UpdateChecker.findBestAsset(in: assets, for: .linux)
        XCTAssertEqual(linuxAsset?.name, "AntigravityQuota-v1.2.0.AppImage")

        // Android: prefers .apk
        let androidAsset = UpdateChecker.findBestAsset(in: assets, for: .android)
        XCTAssertEqual(androidAsset?.name, "AntigravityQuota-v1.2.0.apk")
    }

    func testParseGitHubReleaseJSON() throws {
        let json = """
        {
            "tag_name": "v1.2.0",
            "name": "AntigravityQuota 1.2.0",
            "html_url": "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0",
            "body": "### What's new\\n- Added auto-update checker\\n- UI polish",
            "published_at": "2026-10-01T00:00:00Z",
            "assets": [
                {
                    "name": "AntigravityQuota.dmg",
                    "browser_download_url": "https://github.com/Fuheshka/AntigravityQuota/releases/download/v1.2.0/AntigravityQuota.dmg"
                }
            ]
        }
        """

        let data = json.data(using: .utf8)!
        let release = try JSONDecoder().decode(GitHubReleaseInfo.self, from: data)

        XCTAssertEqual(release.tagName, "v1.2.0")
        XCTAssertEqual(release.cleanVersion, "1.2.0")
        XCTAssertEqual(release.name, "AntigravityQuota 1.2.0")
        XCTAssertEqual(release.htmlUrl.absoluteString, "https://github.com/Fuheshka/AntigravityQuota/releases/tag/v1.2.0")
        XCTAssertEqual(release.assets.count, 1)
        XCTAssertEqual(release.assets.first?.name, "AntigravityQuota.dmg")
    }

    func testCooldownEvaluation() {
        let now = Date()
        let oneHourAgo = now.addingTimeInterval(-3600)
        let twentyFiveHoursAgo = now.addingTimeInterval(-25 * 3600)

        // Under 24h cooldown without force -> should throttle
        XCTAssertTrue(UpdateChecker.shouldThrottle(lastCheck: oneHourAgo, now: now, cooldown: 86400, force: false))

        // Over 24h -> should NOT throttle
        XCTAssertFalse(UpdateChecker.shouldThrottle(lastCheck: twentyFiveHoursAgo, now: now, cooldown: 86400, force: false))

        // Force check -> never throttle
        XCTAssertFalse(UpdateChecker.shouldThrottle(lastCheck: oneHourAgo, now: now, cooldown: 86400, force: true))

        // No previous check -> never throttle
        XCTAssertFalse(UpdateChecker.shouldThrottle(lastCheck: nil, now: now, cooldown: 86400, force: false))
    }
}
