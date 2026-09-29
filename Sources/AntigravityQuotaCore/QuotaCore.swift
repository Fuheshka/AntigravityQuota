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
    public static var hudSettingsSubmenu: String {
        isRussian ? "Настройки HUD" : "HUD Settings"
    }
    public static var clickThroughMenuItem: String {
        isRussian ? "Сквозной клик мыши (⌥ Option для захвата)" : "Click-Through Mode (⌥ Option to interact)"
    }
    public static var hudOpacityTitle: String {
        isRussian ? "Прозрачность матового стекла" : "Frosted Glass Opacity"
    }
    public static var opacity100: String {
        isRussian ? "100% (Непрозрачный)" : "100% (Opaque)"
    }
    public static var opacity75: String {
        isRussian ? "75% (Полупрозрачный)" : "75% (Translucent)"
    }
    public static var opacity50: String {
        isRussian ? "50% (Высокая прозрачность)" : "50% (High Translucency)"
    }
    public static var launchAtLogin: String {
        isRussian ? "Запускать при входе в систему" : "Launch at Login"
    }
    public static var showTrendInMenuBar: String {
        isRussian ? "Показывать динамику расхода в строке меню" : "Show Trend Indicators in Menu Bar"
    }
    public static var globalHotkeysTitle: String {
        isRussian ? "Глобальные горячие клавиши" : "Global Hotkeys"
    }
    public static var globalHotkeysSetting: String {
        isRussian ? "Включить глобальные горячие клавиши" : "Enable Global Hotkeys"
    }
    public static var globalHotkeysMenuItem: String {
        isRussian ? "Глобальные горячие клавиши (⌥⇧Q, ⌥⇧M, ⌥⇧R)" : "Global Hotkeys (⌥⇧Q, ⌥⇧M, ⌥⇧R)"
    }
    public static var globalHotkeyToggleHUD: String {
        isRussian ? "⌥⇧Q — Показать / скрыть виджет HUD" : "⌥⇧Q — Toggle HUD visibility"
    }
    public static var globalHotkeyTogglePill: String {
        isRussian ? "⌥⇧M — Переключить режим таблетки / карточки" : "⌥⇧M — Toggle Pill / Card Mode"
    }
    public static var globalHotkeyRefresh: String {
        isRussian ? "⌥⇧R — Принудительно обновить квоты" : "⌥⇧R — Force refresh quotas"
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
    public static var aboutMenuItem: String {
        isRussian ? "О программе AntigravityQuota..." : "About AntigravityQuota..."
    }
    public static var aboutTitle: String {
        isRussian ? "О программе AntigravityQuota" : "About AntigravityQuota"
    }
    public static var aboutAuthor: String {
        isRussian ? "Автор: Даниил К. (Fuheshka)" : "Created by Daniil K. (Fuheshka)"
    }
    public static var aboutSubtitle: String {
        isRussian ? "Монитор квот и лимитов моделей Google Antigravity в реальном времени" : "Real-time model quota monitor & HUD for Google Antigravity"
    }
    public static var aboutVersionLabel: String {
        isRussian ? "Версия 1.0.0 (macOS 13+)" : "Version 1.0.0 (macOS 13+)"
    }
    public static var aboutServerStatusTitle: String {
        isRussian ? "Статус подключения" : "Connection Status"
    }
    public static var aboutServerConnected: String {
        isRussian ? "Сервер Antigravity обнаружен" : "Antigravity Server Connected"
    }
    public static var aboutServerDisconnected: String {
        isRussian ? "Ожидание запуска Antigravity..." : "Waiting for Antigravity..."
    }
    public static var aboutPid: String {
        isRussian ? "PID процесса" : "Process PID"
    }
    public static var aboutPort: String {
        isRussian ? "Порты API" : "API Ports"
    }
    public static var aboutShortcutsTitle: String {
        isRussian ? "Горячие клавиши" : "Keyboard Shortcuts"
    }
    public static var aboutShortcutRefresh: String {
        isRussian ? "⌘R — Принудительное обновление квот" : "⌘R — Force refresh quotas"
    }
    public static var aboutShortcutHUD: String {
        isRussian ? "⌘H — Показать / скрыть виджет HUD" : "⌘H — Toggle floating HUD"
    }
    public static var aboutShortcutCompact: String {
        isRussian ? "⌘M — Переключить компактный режим" : "⌘M — Toggle compact mode"
    }
    public static var aboutShortcutQuit: String {
        isRussian ? "⌘Q — Выйти из приложения" : "⌘Q — Quit application"
    }
    public static var aboutTipsTitle: String {
        isRussian ? "Полезные возможности" : "Helpful Tips"
    }
    public static var aboutTipHUDDrag: String {
        isRussian ? "Плавающий HUD можно перетаскивать мышью в любое место экрана. Положение автоматически сохраняется." : "Drag the floating HUD anywhere on screen. Position is saved automatically."
    }
    public static var aboutTipPillMode: String {
        isRussian ? "Клик по компактной таблетке разворачивает ее в подробную карточку." : "Click on the compact pill to expand it back into the detailed card."
    }
    public static var aboutTipAutoHide: String {
        isRussian ? "Автоскрытие прячет виджет, когда активно любое другое окно помимо Antigravity." : "Auto-hide conceals the HUD whenever you switch away from Antigravity."
    }
    public static var aboutTipAllModels: String {
        isRussian ? "В меню статус-бара доступен полный расклад по квотам для каждой модели с таймером сброса." : "The status bar menu provides a complete quota breakdown for each model with countdown timers."
    }
    public static var aboutTipClickThrough: String {
        isRussian ? "В режиме сквозного клика виджет пропускает клики в редактор. Зажмите ⌥ Option для взаимодействия или перемещения окна." : "In Click-Through mode, the HUD passes mouse clicks through. Hold ⌥ Option to interact or drag."
    }
    public static var aboutGitHubButton: String {
        isRussian ? "Репозиторий GitHub" : "GitHub Repository"
    }
    public static var aboutReleasesButton: String {
        isRussian ? "Релизы и загрузки" : "Releases & Downloads"
    }
    public static var aboutCopyDiagnosticsButton: String {
        isRussian ? "Скопировать диагностику" : "Copy Diagnostics"
    }
    public static var aboutCopiedNotice: String {
        isRussian ? "Скопировано в буфер обмена!" : "Copied to Clipboard!"
    }
    public static var aboutCloseButton: String {
        isRussian ? "Закрыть" : "Close"
    }
    public static var notificationsMenuItem: String {
        isRussian ? "Уведомления о сбросе и лимитах" : "Quota Alerts & Reset Notifications"
    }
    public static var aboutSettingNotifications: String {
        isRussian ? "Уведомления macOS о сбросе и низком остатке (<10%)" : "macOS alerts on quota reset & low remaining (<10%)"
    }

    public static func notificationsResetTitle(isRussian: Bool = Localization.isRussian) -> String {
        isRussian ? "Квоты сброшены" : "Quotas Reset"
    }

    public static func notificationsResetBody(pool: String, isRussian: Bool = Localization.isRussian) -> String {
        isRussian ? "Пул \(pool) снова доступен на 100%" : "\(pool) pool is back to 100%"
    }

    public static func notificationsLowTitle(isRussian: Bool = Localization.isRussian) -> String {
        isRussian ? "Низкий остаток квоты" : "Low Quota Warning"
    }

    public static func notificationsLowBody(pool: String, percent: Double, isRussian: Bool = Localization.isRussian) -> String {
        let pctStr = String(format: "%.0f%%", percent)
        return isRussian ? "\(pool): осталось \(pctStr)" : "\(pool): \(pctStr) remaining"
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

    public var shortPoolName: String {
        if displayName.localizedCaseInsensitiveContains("gemini") {
            return "Gemini"
        }
        if displayName.localizedCaseInsensitiveContains("claude") || displayName.localizedCaseInsensitiveContains("gpt") {
            return "Claude"
        }
        return displayName
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

    public func menuBarTitle(
        geminiTrend: QuotaTrend? = nil,
        claudeTrend: QuotaTrend? = nil,
        showTrend: Bool = false
    ) -> String {
        guard showTrend else { return menuBarTitle }

        let gTrendStr = geminiTrend != nil ? "\(geminiTrend!.rawValue)" : ""
        let cTrendStr = claudeTrend != nil ? "\(claudeTrend!.rawValue)" : ""

        if let g = geminiGroup?.fiveHourBucket?.percentage,
           let c = claudeGroup?.fiveHourBucket?.percentage {
            return String(format: "%.0f%%%@ · %.0f%%%@", g.rounded(), gTrendStr, c.rounded(), cTrendStr)
        }
        let gModel = models.first { $0.label.localizedCaseInsensitiveContains("gemini") }?.percentage
        let cModel = models.first { $0.label.localizedCaseInsensitiveContains("claude") }?.percentage
        if let g = gModel, let c = cModel {
            return String(format: "%.0f%%%@ · %.0f%%%@", g.rounded(), gTrendStr, c.rounded(), cTrendStr)
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

    public static func snapOriginToScreenEdges(
        origin: CGPoint,
        size: CGSize,
        screenBounds: CGRect,
        threshold: CGFloat = 16.0
    ) -> CGPoint {
        var newX = origin.x
        var newY = origin.y

        // Left edge
        if abs(origin.x - screenBounds.minX) < threshold {
            newX = screenBounds.minX
        } else if abs((origin.x + size.width) - screenBounds.maxX) < threshold {
            // Right edge
            newX = screenBounds.maxX - size.width
        }

        // Bottom edge
        if abs(origin.y - screenBounds.minY) < threshold {
            newY = screenBounds.minY
        } else if abs((origin.y + size.height) - screenBounds.maxY) < threshold {
            // Top edge
            newY = screenBounds.maxY - size.height
        }

        return CGPoint(x: newX, y: newY)
    }
}

public enum QuotaNotificationEvent: Equatable, Sendable {
    case reset(poolName: String)
    case lowQuota(poolName: String, remainingPercentage: Double)

    public var title: String {
        switch self {
        case .reset:
            return Localization.notificationsResetTitle()
        case .lowQuota:
            return Localization.notificationsLowTitle()
        }
    }

    public var body: String {
        switch self {
        case .reset(let pool):
            return Localization.notificationsResetBody(pool: pool)
        case .lowQuota(let pool, let pct):
            return Localization.notificationsLowBody(pool: pool, percent: pct)
        }
    }
}

public final class QuotaNotificationEvaluator {
    private struct PoolState {
        var wasBelow100: Bool
        var lastAlertedThreshold: Double
    }

    private var poolStates: [String: PoolState] = [:]
    private var isFirstEvaluation: Bool = true

    public init() {}

    public func evaluate(snapshot: QuotaSnapshot) -> [QuotaNotificationEvent] {
        var events: [QuotaNotificationEvent] = []

        let pools: [(id: String, name: String, percentage: Double)]
        if !snapshot.groups.isEmpty {
            pools = snapshot.groups.compactMap { g in
                guard let bucket = g.fiveHourBucket else { return nil }
                return (id: bucket.bucketId, name: g.shortPoolName, percentage: bucket.percentage)
            }
        } else {
            pools = snapshot.models.map { m in
                (id: m.label, name: m.label, percentage: m.percentage)
            }
        }

        if isFirstEvaluation {
            isFirstEvaluation = false
            for p in pools {
                let below100 = p.percentage < 99.99
                let threshold: Double
                if p.percentage <= 5.0 {
                    threshold = 5.0
                } else if p.percentage <= 10.0 {
                    threshold = 10.0
                } else {
                    threshold = 100.0
                }
                poolStates[p.id] = PoolState(wasBelow100: below100, lastAlertedThreshold: threshold)
            }
            return []
        }

        for p in pools {
            var state = poolStates[p.id] ?? PoolState(wasBelow100: p.percentage < 99.99, lastAlertedThreshold: 100.0)

            if p.percentage >= 99.99 {
                if state.wasBelow100 {
                    events.append(.reset(poolName: p.name))
                    state.wasBelow100 = false
                }
                state.lastAlertedThreshold = 100.0
            } else {
                state.wasBelow100 = true

                if p.percentage <= 5.0 {
                    if state.lastAlertedThreshold > 5.0 {
                        events.append(.lowQuota(poolName: p.name, remainingPercentage: p.percentage))
                        state.lastAlertedThreshold = 5.0
                    }
                } else if p.percentage <= 10.0 {
                    if state.lastAlertedThreshold > 10.0 {
                        events.append(.lowQuota(poolName: p.name, remainingPercentage: p.percentage))
                        state.lastAlertedThreshold = 10.0
                    }
                } else if p.percentage > 15.0 {
                    state.lastAlertedThreshold = 100.0
                }
            }

            poolStates[p.id] = state
        }

        return events
    }

    public func resetState() {
        poolStates.removeAll()
        isFirstEvaluation = true
    }
}

// MARK: - Global Hotkeys

public enum GlobalHotkeyAction: UInt32, CaseIterable, Sendable {
    case toggleHUD = 1
    case togglePillMode = 2
    case refreshQuotas = 3

    public var id: UInt32 { rawValue }

    public var keyChar: String {
        switch self {
        case .toggleHUD: return "Q"
        case .togglePillMode: return "M"
        case .refreshQuotas: return "R"
        }
    }

    /// Virtual key code corresponding to Carbon kVK_ANSI_* constants:
    /// kVK_ANSI_Q = 12 (0x0C), kVK_ANSI_M = 46 (0x2E), kVK_ANSI_R = 15 (0x0F)
    public var keyCode: UInt32 {
        switch self {
        case .toggleHUD: return 12
        case .togglePillMode: return 46
        case .refreshQuotas: return 15
        }
    }

    /// Carbon modifier flags: (optionKey = 2048 | shiftKey = 512) = 2560
    public var carbonModifiers: UInt32 {
        return 2560
    }

    public var symbolicShortcut: String {
        switch self {
        case .toggleHUD: return "⌥⇧Q"
        case .togglePillMode: return "⌥⇧M"
        case .refreshQuotas: return "⌥⇧R"
        }
    }

    public var localizedName: String {
        switch self {
        case .toggleHUD:
            return Localization.isRussian ? "Показать / скрыть виджет HUD" : "Toggle HUD visibility"
        case .togglePillMode:
            return Localization.isRussian ? "Переключить режим таблетки (Pill Mode)" : "Toggle Pill / Card Mode"
        case .refreshQuotas:
            return Localization.isRussian ? "Принудительно обновить квоты" : "Force refresh quotas"
        }
    }
}

public struct GlobalHotkeyCore: Sendable {
    /// 4-character code 'AGQT' (0x41475154)
    public static let signature: UInt32 = 0x41475154

    public static func action(for id: UInt32) -> GlobalHotkeyAction? {
        GlobalHotkeyAction(rawValue: id)
    }

    public static func action(forKeyCode keyCode: UInt32, modifiers: UInt32) -> GlobalHotkeyAction? {
        guard modifiers == 2560 else { return nil }
        return GlobalHotkeyAction.allCases.first { $0.keyCode == keyCode }
    }

    public static func matches(signature sig: UInt32, id: UInt32) -> GlobalHotkeyAction? {
        guard sig == signature else { return nil }
        return GlobalHotkeyAction(rawValue: id)
    }
}

