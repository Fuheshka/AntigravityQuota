import XCTest
import CoreGraphics
@testable import AntigravityQuotaCore

final class QuotaCoreTests: XCTestCase {

    func testParseRetrieveUserQuotaSummary() throws {
        let json = """
        {
            "response": {
                "groups": [
                    {
                        "displayName": "Gemini Models",
                        "description": "Models within this group: Gemini Flash, Gemini Pro",
                        "buckets": [
                            {
                                "bucketId": "gemini-weekly",
                                "displayName": "Weekly Limit Remaining",
                                "window": "weekly",
                                "remainingFraction": 0.95666826,
                                "resetTime": "2026-09-30T12:11:22Z"
                            },
                            {
                                "bucketId": "gemini-5h",
                                "displayName": "Five Hour Limit Remaining",
                                "window": "5h",
                                "remainingFraction": 0.853634,
                                "resetTime": "2026-09-27T17:06:50Z"
                            }
                        ]
                    },
                    {
                        "displayName": "Claude and GPT models",
                        "description": "Models within this group: Claude Opus, Claude Sonnet, GPT-OSS",
                        "buckets": [
                            {
                                "bucketId": "3p-weekly",
                                "displayName": "Weekly Limit Remaining",
                                "window": "weekly",
                                "remainingFraction": 0.9999768,
                                "resetTime": "2026-09-30T12:11:33Z"
                            },
                            {
                                "bucketId": "3p-5h",
                                "displayName": "Five Hour Limit Remaining",
                                "window": "5h",
                                "remainingFraction": 1.0,
                                "resetTime": "2026-09-27T17:39:56Z"
                            }
                        ]
                    }
                ]
            }
        }
        """.data(using: .utf8)!

        let groups = try QuotaParser.parseSummary(data: json)
        XCTAssertEqual(groups.count, 2)
        XCTAssertEqual(groups[0].displayName, "Gemini Models")
        XCTAssertEqual(groups[0].fiveHourBucket?.bucketId, "gemini-5h")
        XCTAssertEqual(groups[0].fiveHourBucket?.remainingFraction ?? 0, 0.853634, accuracy: 0.0001)
        XCTAssertEqual(groups[0].weeklyBucket?.bucketId, "gemini-weekly")
        XCTAssertNotNil(groups[0].fiveHourBucket?.resetDate)

        let snapshot = QuotaSnapshot(groups: groups, models: [])
        XCTAssertEqual(snapshot.geminiGroup?.displayName, "Gemini Models")
        XCTAssertEqual(snapshot.claudeGroup?.displayName, "Claude and GPT models")
        XCTAssertEqual(snapshot.menuBarTitle, "85% · 100%")
    }

    func testParseCascadeModelConfigData() throws {
        let json = """
        {
            "clientModelConfigs": [
                {
                    "label": "Gemini 3.8 Flash (High)",
                    "quotaInfo": {
                        "remainingFraction": 0.9062434,
                        "resetTime": "2026-09-27T17:06:50Z"
                    }
                },
                {
                    "label": "Claude Opus 4.6 (Thinking)",
                    "quotaInfo": {
                        "remainingFraction": 1.0,
                        "resetTime": "2026-09-27T17:26:05Z"
                    }
                }
            ]
        }
        """.data(using: .utf8)!

        let models = try QuotaParser.parseModelConfigs(data: json)
        XCTAssertEqual(models.count, 2)
        XCTAssertEqual(models[0].label, "Gemini 3.8 Flash (High)")
        XCTAssertEqual(models[0].percentage, 90.62434, accuracy: 0.01)
        XCTAssertNotNil(models[0].resetDate)
    }

    func testParseServerDiscoveryFromPsAndLsof() {
        let psSample = """
        fuheshka         49291   0.4  1.6 413290320 270832   ??  S     3:00PM   0:20.74 /Applications/Antigravity.app/Contents/Resources/bin/language_server --standalone --override_ide_name antigravity --subclient_type hub --override_ide_version 2.17.0 --https_server_port 0 --csrf_token 1325a2f7-0382-4f3a-92d1-9d5fecc7e2fe --app_data_dir antigravity
        fuheshka         49306   0.0  0.3 411655696  53728   ??  S     3:00PM   0:00.33 /Applications/Antigravity.app/Contents/Resources/bin/language_server multicall schedule
        """
        let parsed = ServerDiscovery.parseProcessLine(psSample)
        XCTAssertNotNil(parsed)
        XCTAssertEqual(parsed?.pid, 49291)
        XCTAssertEqual(parsed?.csrfToken, "1325a2f7-0382-4f3a-92d1-9d5fecc7e2fe")

        let lsofSample = """
        language_ 49291 fuheshka    7u  IPv4 0x77d618b0ae8ec7fb      0t0  TCP 127.0.0.1:63659 (LISTEN)
        language_ 49291 fuheshka    8u  IPv4 0x42399abd6a7d0ef7      0t0  TCP 127.0.0.1:63660 (LISTEN)
        """
        let ports = ServerDiscovery.parseLsofPorts(lsofSample, pid: 49291)
        XCTAssertEqual(ports, [63660, 63659])
    }

    func testCountdownFormatterRussianAndEnglish() {
        let now = Date(timeIntervalSince1970: 1_700_000_000)
        let in94Min = now.addingTimeInterval(94 * 60)
        let in2Days5Hours = now.addingTimeInterval((2 * 24 + 5) * 3600)

        XCTAssertEqual(QuotaFormatter.formatCountdown(to: in94Min, from: now, isRussian: true), "1ч 34м")
        XCTAssertEqual(QuotaFormatter.formatCountdown(to: in94Min, from: now, isRussian: false), "1h 34m")
        XCTAssertEqual(QuotaFormatter.formatCountdown(to: in2Days5Hours, from: now, isRussian: true), "2д 5ч")
        XCTAssertEqual(QuotaFormatter.formatCountdown(to: in2Days5Hours, from: now, isRussian: false), "2d 5h")
    }

    func testQuotaClientBuildsValidConnectRPCRequest() throws {
        let endpoint = ServerEndpoint(pid: 49291, csrfToken: "test-csrf-123", ports: [63660, 63659])
        let req = try QuotaClient.makeRPCRequest(
            port: 63660,
            useTLS: false,
            csrfToken: endpoint.csrfToken,
            method: "RetrieveUserQuotaSummary",
            body: ["forceRefresh": true]
        )
        XCTAssertEqual(req.url?.absoluteString, "http://127.0.0.1:63660/exa.language_server_pb.LanguageServerService/RetrieveUserQuotaSummary")
        XCTAssertEqual(req.httpMethod, "POST")
        XCTAssertEqual(req.value(forHTTPHeaderField: "x-codeium-csrf-token"), "test-csrf-123")
        XCTAssertEqual(req.value(forHTTPHeaderField: "Connect-Protocol-Version"), "1")
    }

    func testHUDFrameClampingOnToggle() {
        let screen = CGRect(x: 0, y: 0, width: 1728, height: 1080)
        let oldFrame = CGRect(x: 1460, y: 32, width: 248, height: 126)
        let compactSize = CGSize(width: 210, height: 32)

        let compactFrame = QuotaFormatter.anchoredHUDFrame(
            oldFrame: oldFrame,
            newSize: compactSize,
            screenBounds: screen
        )
        XCTAssertEqual(compactFrame.width, 210)
        XCTAssertEqual(compactFrame.height, 32)
        // Bottom-right corner stays anchored at maxX=1708, minY=32
        XCTAssertEqual(compactFrame.maxX, 1708)
        XCTAssertEqual(compactFrame.minY, 32)
    }

    func testQuotaClientDiagnosticReport() {
        let endpoint = ServerEndpoint(pid: 12345, csrfToken: "sample-token-abc", ports: [50001, 50000])
        let snap = QuotaSnapshot(
            groups: [
                QuotaGroup(
                    displayName: "Gemini Models",
                    description: "Gemini",
                    buckets: [
                        QuotaBucket(bucketId: "gemini-5h", window: "5h", remainingFraction: 0.8, resetDate: nil)
                    ]
                )
            ],
            models: []
        )
        let report = QuotaClient.buildDiagnosticReport(endpoint: endpoint, snapshot: snap)
        XCTAssertTrue(report.contains("PID: 12345"))
        XCTAssertTrue(report.contains("50001"))
        XCTAssertTrue(report.contains("Gemini 5h: 80.0%"))
    }
}

