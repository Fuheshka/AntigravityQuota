import Foundation

public enum CLICommand: Equatable, Sendable {
    case gui
    case status
    case json
    case history
    case exportHistory
    case help
    case unknown(String)
}

public enum CLIArguments {
    public static func parse(args: [String]) -> CLICommand {
        guard args.count > 1 else { return .gui }
        return parseFlag(args[1])
    }

    public static func parseFlag(_ flag: String) -> CLICommand {
        switch flag {
        case "--status", "-s":
            return .status
        case "--json", "-j":
            return .json
        case "--history":
            return .history
        case "--export-history":
            return .exportHistory
        case "--help", "-h":
            return .help
        default:
            return .unknown(flag)
        }
    }
}

public enum CLIFormatter {
    private static let isoFormatter: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter()
        f.formatOptions = [.withInternetDateTime]
        return f
    }()

    public static func formatStatus(
        snapshot: QuotaSnapshot,
        now: Date = Date(),
        isRussian: Bool = Localization.isRussian
    ) -> String {
        let geminiPart: String
        let claudePart: String

        // 1. Check groups first
        if let gBucket = snapshot.geminiGroup?.fiveHourBucket {
            geminiPart = formatPool(prefix: "G", percentage: gBucket.percentage, resetDate: gBucket.resetDate, now: now, isRussian: isRussian)
        } else if let gModel = snapshot.models.first(where: { $0.label.localizedCaseInsensitiveContains("gemini") }) {
            geminiPart = formatPool(prefix: "G", percentage: gModel.percentage, resetDate: gModel.resetDate, now: now, isRussian: isRussian)
        } else {
            geminiPart = "G —%"
        }

        if let cBucket = snapshot.claudeGroup?.fiveHourBucket {
            claudePart = formatPool(prefix: "C", percentage: cBucket.percentage, resetDate: cBucket.resetDate, now: now, isRussian: isRussian)
        } else if let cModel = snapshot.models.first(where: {
            $0.label.localizedCaseInsensitiveContains("claude") || $0.label.localizedCaseInsensitiveContains("gpt")
        }) {
            claudePart = formatPool(prefix: "C", percentage: cModel.percentage, resetDate: cModel.resetDate, now: now, isRussian: isRussian)
        } else {
            claudePart = "C —%"
        }

        return "\(geminiPart) · \(claudePart)"
    }

    private static func formatPool(
        prefix: String,
        percentage: Double,
        resetDate: Date?,
        now: Date,
        isRussian: Bool
    ) -> String {
        let pctStr = String(format: "%.1f%%", percentage)
        if percentage < 99.99, let resetDate = resetDate {
            let cd = QuotaFormatter.formatCountdown(to: resetDate, from: now, isRussian: isRussian)
            if cd != "—" {
                return "\(prefix) \(pctStr) (\(cd))"
            }
        }
        return "\(prefix) \(pctStr)"
    }

    private static func roundPercent(_ val: Double) -> Double {
        (val * 10.0).rounded() / 10.0
    }

    public static func formatJSON(
        snapshot: QuotaSnapshot,
        now: Date = Date(),
        isRussian: Bool = Localization.isRussian
    ) throws -> String {
        var summaryDict: [String: Any] = [
            "status": formatStatus(snapshot: snapshot, now: now, isRussian: isRussian)
        ]

        if let gBucket = snapshot.geminiGroup?.fiveHourBucket {
            var gDict: [String: Any] = ["percentage": roundPercent(gBucket.percentage)]
            if let rd = gBucket.resetDate {
                gDict["resetTime"] = isoFormatter.string(from: rd)
                gDict["resetCountdown"] = QuotaFormatter.formatCountdown(to: rd, from: now, isRussian: isRussian)
            } else {
                gDict["resetTime"] = NSNull()
                gDict["resetCountdown"] = NSNull()
            }
            summaryDict["gemini"] = gDict
        } else if let gModel = snapshot.models.first(where: { $0.label.localizedCaseInsensitiveContains("gemini") }) {
            var gDict: [String: Any] = ["percentage": roundPercent(gModel.percentage)]
            if let rd = gModel.resetDate {
                gDict["resetTime"] = isoFormatter.string(from: rd)
                gDict["resetCountdown"] = QuotaFormatter.formatCountdown(to: rd, from: now, isRussian: isRussian)
            } else {
                gDict["resetTime"] = NSNull()
                gDict["resetCountdown"] = NSNull()
            }
            summaryDict["gemini"] = gDict
        }

        if let cBucket = snapshot.claudeGroup?.fiveHourBucket {
            var cDict: [String: Any] = ["percentage": roundPercent(cBucket.percentage)]
            if let rd = cBucket.resetDate {
                cDict["resetTime"] = isoFormatter.string(from: rd)
                cDict["resetCountdown"] = QuotaFormatter.formatCountdown(to: rd, from: now, isRussian: isRussian)
            } else {
                cDict["resetTime"] = NSNull()
                cDict["resetCountdown"] = NSNull()
            }
            summaryDict["claude"] = cDict
        } else if let cModel = snapshot.models.first(where: {
            $0.label.localizedCaseInsensitiveContains("claude") || $0.label.localizedCaseInsensitiveContains("gpt")
        }) {
            var cDict: [String: Any] = ["percentage": roundPercent(cModel.percentage)]
            if let rd = cModel.resetDate {
                cDict["resetTime"] = isoFormatter.string(from: rd)
                cDict["resetCountdown"] = QuotaFormatter.formatCountdown(to: rd, from: now, isRussian: isRussian)
            } else {
                cDict["resetTime"] = NSNull()
                cDict["resetCountdown"] = NSNull()
            }
            summaryDict["claude"] = cDict
        }

        let groupsArray: [[String: Any]] = snapshot.groups.map { group in
            let buckets: [[String: Any]] = group.buckets.map { b in
                var bDict: [String: Any] = [
                    "bucketId": b.bucketId,
                    "window": b.window,
                    "percentage": roundPercent(b.percentage),
                    "remainingFraction": b.remainingFraction
                ]
                if let rd = b.resetDate {
                    bDict["resetTime"] = isoFormatter.string(from: rd)
                    bDict["resetCountdown"] = QuotaFormatter.formatCountdown(to: rd, from: now, isRussian: isRussian)
                } else {
                    bDict["resetTime"] = NSNull()
                    bDict["resetCountdown"] = NSNull()
                }
                return bDict
            }
            return [
                "displayName": group.displayName,
                "shortName": group.shortPoolName,
                "description": group.description,
                "buckets": buckets
            ]
        }

        let modelsArray: [[String: Any]] = snapshot.models.map { model in
            var mDict: [String: Any] = [
                "label": model.label,
                "percentage": roundPercent(model.percentage),
                "remainingFraction": model.remainingFraction
            ]
            if let rd = model.resetDate {
                mDict["resetTime"] = isoFormatter.string(from: rd)
                mDict["resetCountdown"] = QuotaFormatter.formatCountdown(to: rd, from: now, isRussian: isRussian)
            } else {
                mDict["resetTime"] = NSNull()
                mDict["resetCountdown"] = NSNull()
            }
            return mDict
        }

        let root: [String: Any] = [
            "updatedAt": isoFormatter.string(from: snapshot.updatedAt),
            "summary": summaryDict,
            "groups": groupsArray,
            "models": modelsArray
        ]

        let data = try JSONSerialization.data(withJSONObject: root, options: [.prettyPrinted, .sortedKeys])
        guard let string = String(data: data, encoding: .utf8) else {
            throw URLError(.cannotDecodeContentData)
        }
        return string
    }

    private static let historyDateFormatter: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "yyyy-MM-dd HH:mm:ss"
        return f
    }()

    public static func formatHistoryTable(
        records: [QuotaHistorySample],
        limit: Int? = 50,
        isRussian: Bool = Localization.isRussian
    ) -> String {
        guard !records.isEmpty else {
            return Localization.historyEmpty(isRussian: isRussian)
        }

        let slice = limit != nil && records.count > limit! ? Array(records.suffix(limit!)) : records

        let colDateTitle = Localization.historyTableHeaderDate(isRussian: isRussian)
        let colGeminiTitle = Localization.historyTableHeaderGemini(isRussian: isRussian)
        let colClaudeTitle = Localization.historyTableHeaderClaude(isRussian: isRussian)

        // Column widths: Date: 21, Gemini: 14, Claude: 14
        let wDate = 21
        let wGemini = 14
        let wClaude = 14

        let topBorder = "┌" + String(repeating: "─", count: wDate) + "┬" + String(repeating: "─", count: wGemini) + "┬" + String(repeating: "─", count: wClaude) + "┐"
        let sepBorder = "├" + String(repeating: "─", count: wDate) + "┼" + String(repeating: "─", count: wGemini) + "┼" + String(repeating: "─", count: wClaude) + "┤"
        let bottomBorder = "└" + String(repeating: "─", count: wDate) + "┴" + String(repeating: "─", count: wGemini) + "┴" + String(repeating: "─", count: wClaude) + "┘"

        func centerPad(_ text: String, width: Int) -> String {
            if text.count >= width { return text }
            let totalPad = width - text.count
            let leftPad = totalPad / 2
            let rightPad = totalPad - leftPad
            return String(repeating: " ", count: leftPad) + text + String(repeating: " ", count: rightPad)
        }

        func rightPad(_ text: String, width: Int) -> String {
            if text.count >= width { return text }
            return String(repeating: " ", count: width - text.count) + text
        }

        func leftPad(_ text: String, width: Int) -> String {
            if text.count >= width { return text }
            return text + String(repeating: " ", count: width - text.count)
        }

        let headerRow = "│" + centerPad(colDateTitle, width: wDate) + "│" + centerPad(colGeminiTitle, width: wGemini) + "│" + centerPad(colClaudeTitle, width: wClaude) + "│"

        var lines: [String] = [topBorder, headerRow, sepBorder]

        for sample in slice {
            let dateStr = " " + historyDateFormatter.string(from: sample.timestamp)
            let gStr = (sample.geminiPercentage != nil ? String(format: "%.1f%%", sample.geminiPercentage!) : "—") + "  "
            let cStr = (sample.claudePercentage != nil ? String(format: "%.1f%%", sample.claudePercentage!) : "—") + "  "

            let row = "│" + leftPad(dateStr, width: wDate) + "│" + rightPad(gStr, width: wGemini) + "│" + rightPad(cStr, width: wClaude) + "│"
            lines.append(row)
        }

        lines.append(bottomBorder)
        return lines.joined(separator: "\n")
    }

    public static func formatHistoryJSON(records: [QuotaHistorySample]) throws -> String {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(records)
        guard let string = String(data: data, encoding: .utf8) else {
            throw URLError(.cannotDecodeContentData)
        }
        return string
    }

    public static func helpMessage() -> String {
        """
        AntigravityQuota - Real-time model quota monitor for Google Antigravity
        Created by Daniil K. (Fuheshka)

        Usage:
          antigravity-quota [option]

        Options:
          --status, -s        Print compact single-line quota status (e.g. G 85.4% (1ч 12м) · C 100.0%)
          --json, -j          Print complete quota snapshot as formatted JSON
          --history           Print table of recent quota measurements from disk history
          --export-history    Export entire quota measurement history as JSON
          -h, --help          Display this help information

        Integrations:
          SketchyBar:
            sketchybar --set antigravity_quota label="$(antigravity-quota --status)"

          SwiftBar / BitBar:
            antigravity-quota --status
            echo "---"
            echo "Open Antigravity | bash='/Applications/Antigravity.app'"

          Raycast / tmux:
            antigravity-quota --status
        """
    }
}
