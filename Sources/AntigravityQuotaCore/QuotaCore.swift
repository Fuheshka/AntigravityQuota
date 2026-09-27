import Foundation
import CoreGraphics

public enum Localization {
    public static var isRussian: Bool {
        guard let lang = Locale.preferredLanguages.first?.lowercased() else { return true }
        return lang.hasPrefix("ru")
    }

    public static var appTitle: String {
        isRussian ? "AntigravityQuota • Лимиты моделей" : "AntigravityQuota • Model Limits"
    }
    public static var geminiPoolTitle: String {
        isRussian ? "Пул Gemini (Flash / Pro)" : "Gemini Pool (Flash / Pro)"
    }
    public static var claudePoolTitle: String {
        isRussian ? "Пул Claude и GPT-OSS" : "Claude & GPT-OSS Pool"
    }
    public static var fiveHourWindow: String {
        isRussian ? "Лимит 5ч" : "5h Limit"
    }
    public static var weeklyWindow: String {
        isRussian ? "Недельный" : "Weekly"
    }
    public static var resetPrefix: String {
        isRussian ? "сброс через" : "resets in"
    }
    public static var refreshNow: String {
        isRussian ? "Обновить лимиты сейчас" : "Refresh Quotas Now"
    }
    public static var showHUD: String {
        isRussian ? "Показывать плавающий HUD-виджет" : "Show Floating HUD Widget"
    }
    public static var hudOnlyInAntigravity: String {
        isRussian ? "Автоскрытие HUD вне окна Antigravity" : "Auto-Hide HUD Outside Antigravity"
    }
    public static var compactHUDMode: String {
        isRussian ? "Компактный режим HUD (таблетка)" : "Compact HUD Mode (Pill)"
    }
    public static var launchAtLogin: String {
        isRussian ? "Запускать при входе в систему" : "Launch at Login"
    }
    public static var allModelsSubmenu: String {
        isRussian ? "Все модели подробно" : "All Models Breakdown"
    }
    public static var serverOffline: String {
        isRussian ? "Ожидание запуска Antigravity..." : "Waiting for Antigravity..."
    }
    public static var updatedPrefix: String {
        isRussian ? "Обновлено в" : "Updated at"
    }
    public static var quitApp: String {
        isRussian ? "Выйти из AntigravityQuota" : "Quit AntigravityQuota"
    }
}

public struct QuotaBucket: Equatable, Sendable {
    public let bucketId: String
    public let window: String
    public let remainingFraction: Double
    public let resetDate: Date?

    public var percentage: Double {
        max(0.0, min(100.0, remainingFraction * 100.0))
    }

    public init(bucketId: String, window: String, remainingFraction: Double, resetDate: Date?) {
        self.bucketId = bucketId
        self.window = window
        self.remainingFraction = remainingFraction
        self.resetDate = resetDate
    }
}

public struct QuotaGroup: Equatable, Sendable {
    public let displayName: String
    public let description: String
    public let buckets: [QuotaBucket]

    public var fiveHourBucket: QuotaBucket? {
        buckets.first { $0.window == "5h" || $0.bucketId.hasSuffix("-5h") }
    }

    public var weeklyBucket: QuotaBucket? {
        buckets.first { $0.window == "weekly" || $0.bucketId.hasSuffix("-weekly") }
    }

    public init(displayName: String, description: String, buckets: [QuotaBucket]) {
        self.displayName = displayName
        self.description = description
        self.buckets = buckets
    }
}

public struct ModelQuotaItem: Equatable, Sendable {
    public let label: String
    public let remainingFraction: Double
    public let resetDate: Date?

    public var percentage: Double {
        max(0.0, min(100.0, remainingFraction * 100.0))
    }

    public init(label: String, remainingFraction: Double, resetDate: Date?) {
        self.label = label
        self.remainingFraction = remainingFraction
        self.resetDate = resetDate
    }
}

public struct QuotaSnapshot: Equatable, Sendable {
    public let groups: [QuotaGroup]
    public let models: [ModelQuotaItem]
    public let updatedAt: Date

    public var geminiGroup: QuotaGroup? {
        groups.first { $0.displayName.localizedCaseInsensitiveContains("gemini") }
    }

    public var claudeGroup: QuotaGroup? {
        groups.first {
            $0.displayName.localizedCaseInsensitiveContains("claude") ||
            $0.displayName.localizedCaseInsensitiveContains("gpt")
        }
    }

    public var menuBarTitle: String {
        if let g = geminiGroup?.fiveHourBucket?.percentage,
           let c = claudeGroup?.fiveHourBucket?.percentage {
            return String(format: "%.0f%% · %.0f%%", g.rounded(), c.rounded())
        }
        let gModel = models.first { $0.label.localizedCaseInsensitiveContains("gemini") }?.percentage
        let cModel = models.first { $0.label.localizedCaseInsensitiveContains("claude") }?.percentage
        if let g = gModel, let c = cModel {
            return String(format: "%.0f%% · %.0f%%", g.rounded(), c.rounded())
        }
        return "—%"
    }

    public init(groups: [QuotaGroup], models: [ModelQuotaItem], updatedAt: Date = Date()) {
        self.groups = groups
        self.models = models
        self.updatedAt = updatedAt
    }
}

public struct ServerEndpoint: Equatable, Sendable {
    public let pid: Int
    public let csrfToken: String
    public let ports: [Int]

    public init(pid: Int, csrfToken: String, ports: [Int]) {
        self.pid = pid
        self.csrfToken = csrfToken
        self.ports = ports
    }
}

public enum QuotaParser {
    private static func parseISO8601(_ raw: String?) -> Date? {
        guard let raw = raw, !raw.isEmpty else { return nil }
        let formatterWithFrac = ISO8601DateFormatter()
        formatterWithFrac.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        if let d = formatterWithFrac.date(from: raw) { return d }

        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime]
        return formatter.date(from: raw)
    }

    public static func parseSummary(data: Data) throws -> [QuotaGroup] {
        guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return []
        }
        let responseDict = (root["response"] as? [String: Any]) ?? root
        guard let rawGroups = responseDict["groups"] as? [[String: Any]] else {
            return []
        }

        return rawGroups.map { g in
            let displayName = (g["displayName"] as? String) ?? ""
            let description = (g["description"] as? String) ?? ""
            let rawBuckets = (g["buckets"] as? [[String: Any]]) ?? []
            let buckets: [QuotaBucket] = rawBuckets.map { b in
                let bucketId = (b["bucketId"] as? String) ?? ""
                let window = (b["window"] as? String) ?? ""
                let fraction = (b["remainingFraction"] as? NSNumber)?.doubleValue ?? 0.0
                let resetDate = parseISO8601(b["resetTime"] as? String)
                return QuotaBucket(
                    bucketId: bucketId,
                    window: window,
                    remainingFraction: fraction,
                    resetDate: resetDate
                )
            }
            return QuotaGroup(displayName: displayName, description: description, buckets: buckets)
        }
    }

    public static func parseModelConfigs(data: Data) throws -> [ModelQuotaItem] {
        guard let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return []
        }
        let container: [String: Any]
        if let userStatus = root["userStatus"] as? [String: Any],
           let cascadeData = userStatus["cascadeModelConfigData"] as? [String: Any] {
            container = cascadeData
        } else {
            container = root
        }

        guard let configs = container["clientModelConfigs"] as? [[String: Any]] else {
            return []
        }

        return configs.compactMap { item in
            guard let label = item["label"] as? String else { return nil }
            let quotaInfo = item["quotaInfo"] as? [String: Any]
            let fraction = (quotaInfo?["remainingFraction"] as? NSNumber)?.doubleValue ?? 0.0
            let resetDate = parseISO8601(quotaInfo?["resetTime"] as? String)
            return ModelQuotaItem(label: label, remainingFraction: fraction, resetDate: resetDate)
        }
    }
}

public enum ServerDiscovery {
    public static func parseProcessLine(_ psOutput: String) -> (pid: Int, csrfToken: String)? {
        for line in psOutput.components(separatedBy: .newlines) {
            guard line.contains("language_server"),
                  line.contains("--csrf_token"),
                  !line.contains("multicall") else {
                continue
            }
            let tokens = line.split(whereSeparator: { $0.isWhitespace }).map(String.init)
            guard tokens.count >= 3, let pid = Int(tokens[1]) else { continue }
            if let csrfIndex = tokens.firstIndex(of: "--csrf_token"),
               csrfIndex + 1 < tokens.count {
                return (pid: pid, csrfToken: tokens[csrfIndex + 1])
            }
        }
        return nil
    }

    public static func parseLsofPorts(_ lsofOutput: String, pid: Int) -> [Int] {
        let pidStr = String(pid)
        var ports = Set<Int>()
        for line in lsofOutput.components(separatedBy: .newlines) {
            guard line.contains("LISTEN") else { continue }
            let cols = line.split(whereSeparator: { $0.isWhitespace }).map(String.init)
            guard cols.count >= 9, cols[1] == pidStr else { continue }
            for col in cols where col.contains("127.0.0.1:") || col.contains("*:") {
                if let portStr = col.split(separator: ":").last,
                   let port = Int(portStr) {
                    ports.insert(port)
                }
            }
        }
        // Sort descending because the higher port is the plain HTTP listener in language_server
        return ports.sorted(by: >)
    }

    public static func discoverActiveServer() -> ServerEndpoint? {
        guard let psOut = runCommand("/bin/ps", args: ["aux"]),
              let proc = parseProcessLine(psOut),
              let lsofOut = runCommand("/usr/sbin/lsof", args: ["-nP", "-iTCP", "-sTCP:LISTEN", "-a", "-p", String(proc.pid)]) else {
            return nil
        }
        let ports = parseLsofPorts(lsofOut, pid: proc.pid)
        guard !ports.isEmpty else { return nil }
        return ServerEndpoint(pid: proc.pid, csrfToken: proc.csrfToken, ports: ports)
    }

    private static func runCommand(_ launchPath: String, args: [String]) -> String? {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: launchPath)
        process.arguments = args
        let pipe = Pipe()
        process.standardOutput = pipe
        process.standardError = Pipe()
        do {
            try process.run()
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()
            return String(data: data, encoding: .utf8)
        } catch {
            return nil
        }
    }
}

public enum QuotaFormatter {
    public static func formatCountdown(
        to resetDate: Date?,
        from now: Date = Date(),
        isRussian: Bool = Localization.isRussian
    ) -> String {
        guard let resetDate = resetDate else { return "—" }
        let diff = max(0, Int(resetDate.timeIntervalSince(now).rounded()))
        let days = diff / 86400
        let hours = (diff % 86400) / 3600
        let minutes = (diff % 3600) / 60

        if days > 0 {
            return isRussian ? "\(days)д \(hours)ч" : "\(days)d \(hours)h"
        } else if hours > 0 {
            return isRussian ? "\(hours)ч \(minutes)м" : "\(hours)h \(minutes)m"
        } else {
            return isRussian ? "\(max(1, minutes))м" : "\(max(1, minutes))m"
        }
    }

    public static func formatClockTime(_ date: Date?) -> String {
        guard let date = date else { return "—" }
        let df = DateFormatter()
        df.dateFormat = "HH:mm"
        return df.string(from: date)
    }

    public static func anchoredHUDFrame(
        oldFrame: CGRect,
        newSize: CGSize,
        screenBounds: CGRect
    ) -> CGRect {
        // Keep the right edge (maxX) and bottom edge (minY) anchored, then clamp to screenBounds
        var x = oldFrame.maxX - newSize.width
        var y = oldFrame.minY
        x = max(screenBounds.minX + 8, min(x, screenBounds.maxX - newSize.width - 8))
        y = max(screenBounds.minY + 8, min(y, screenBounds.maxY - newSize.height - 8))
        return CGRect(x: x, y: y, width: newSize.width, height: newSize.height)
    }
}
