import XCTest
@testable import AntigravityQuotaCore

final class CLITests: XCTestCase {
    func testCLIArgumentsParsing() {
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary"]), .gui)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "--status"]), .status)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "-s"]), .status)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "--json"]), .json)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "-j"]), .json)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "--help"]), .help)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "-h"]), .help)
        XCTAssertEqual(CLIArguments.parse(args: ["/path/to/binary", "--invalid"]), .unknown("--invalid"))
    }

    func testFormatStatusSingleLine() {
        let now = Date(timeIntervalSince1970: 1700000000)
        let resetDate = now.addingTimeInterval(3600 + 12 * 60) // 1h 12m

        let geminiBucket = QuotaBucket(
            bucketId: "gemini-5h",
            window: "5h",
            remainingFraction: 0.854,
            resetDate: resetDate
        )
        let claudeBucket = QuotaBucket(
            bucketId: "claude-5h",
            window: "5h",
            remainingFraction: 1.0,
            resetDate: nil
        )

        let groups = [
            QuotaGroup(displayName: "Пул Gemini (Flash / Pro)", description: "Gemini", buckets: [geminiBucket]),
            QuotaGroup(displayName: "Пул Claude и GPT-OSS", description: "Claude", buckets: [claudeBucket])
        ]
        let snapshot = QuotaSnapshot(groups: groups, models: [], updatedAt: now)

        let statusRU = CLIFormatter.formatStatus(snapshot: snapshot, now: now, isRussian: true)
        XCTAssertEqual(statusRU, "G 85.4% (1ч 12м) · C 100.0%")

        let statusEN = CLIFormatter.formatStatus(snapshot: snapshot, now: now, isRussian: false)
        XCTAssertEqual(statusEN, "G 85.4% (1h 12m) · C 100.0%")
    }

    func testFormatStatusFallbackToModels() {
        let now = Date(timeIntervalSince1970: 1700000000)
        let resetDate = now.addingTimeInterval(45 * 60) // 45m

        let models = [
            ModelQuotaItem(label: "Gemini 2.5 Flash", remainingFraction: 0.50, resetDate: resetDate),
            ModelQuotaItem(label: "Claude 3.7 Sonnet", remainingFraction: 0.90, resetDate: nil)
        ]
        let snapshot = QuotaSnapshot(groups: [], models: models, updatedAt: now)

        let status = CLIFormatter.formatStatus(snapshot: snapshot, now: now, isRussian: true)
        XCTAssertEqual(status, "G 50.0% (45м) · C 90.0%")
    }

    func testFormatJSONOutput() throws {
        let now = Date(timeIntervalSince1970: 1700000000)
        let resetDate = now.addingTimeInterval(3600)

        let geminiBucket = QuotaBucket(
            bucketId: "gemini-5h",
            window: "5h",
            remainingFraction: 0.854,
            resetDate: resetDate
        )
        let group = QuotaGroup(displayName: "Gemini Pool", description: "Gemini", buckets: [geminiBucket])
        let model = ModelQuotaItem(label: "Gemini 2.5 Flash", remainingFraction: 0.854, resetDate: resetDate)
        let snapshot = QuotaSnapshot(groups: [group], models: [model], updatedAt: now)

        let jsonString = try CLIFormatter.formatJSON(snapshot: snapshot, now: now, isRussian: true)
        guard let data = jsonString.data(using: .utf8),
              let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            XCTFail("Output is not valid JSON")
            return
        }

        XCTAssertNotNil(json["updatedAt"])
        XCTAssertNotNil(json["summary"])
        guard let summary = json["summary"] as? [String: Any] else {
            XCTFail("Missing summary object in JSON")
            return
        }
        XCTAssertEqual(summary["status"] as? String, "G 85.4% (1ч 0м) · C —%")
        guard let gemini = summary["gemini"] as? [String: Any] else {
            XCTFail("Missing gemini in summary")
            return
        }
        XCTAssertEqual(gemini["percentage"] as? Double, 85.4)

        guard let groups = json["groups"] as? [[String: Any]], groups.count == 1 else {
            XCTFail("Missing groups in JSON")
            return
        }
        XCTAssertEqual(groups[0]["displayName"] as? String, "Gemini Pool")

        guard let models = json["models"] as? [[String: Any]], models.count == 1 else {
            XCTFail("Missing models in JSON")
            return
        }
        XCTAssertEqual(models[0]["label"] as? String, "Gemini 2.5 Flash")
    }

    func testHelpMessage() {
        let help = CLIFormatter.helpMessage()
        XCTAssertTrue(help.contains("--status"))
        XCTAssertTrue(help.contains("--json"))
        XCTAssertTrue(help.contains("--help"))
    }
}
