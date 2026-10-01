import Foundation

public enum QuotaTrend: String, Equatable, Sendable {
    case burning = "↓"
    case stable = "→"
    case recovering = "↑"
}

public enum QuotaPool: String, Equatable, Sendable {
    case gemini
    case claude
}

public struct QuotaBurnRate: Equatable, Sendable {
    /// Rate in percentage points per hour.
    /// Negative indicates consumption (e.g. -15.0 = -15%/hr).
    /// Positive indicates replenishment / reset (e.g. +85.0 = +85%/hr).
    /// 0.0 indicates no change.
    public let ratePerHour: Double
    public let trend: QuotaTrend
    /// Estimated time in seconds until depletion (0%), or nil if not burning or already at 0.
    public let estimatedTimeToDepletion: TimeInterval?

    public init(ratePerHour: Double, trend: QuotaTrend, estimatedTimeToDepletion: TimeInterval?) {
        self.ratePerHour = ratePerHour
        self.trend = trend
        self.estimatedTimeToDepletion = estimatedTimeToDepletion
    }

    public func formatted(isRussian: Bool = Localization.isRussian) -> String {
        switch trend {
        case .stable:
            return isRussian ? "~0%/ч" : "~0%/h"
        case .recovering:
            let pctStr = String(format: "+%.0f%%", abs(ratePerHour))
            return isRussian ? "\(pctStr)/ч (сброс)" : "\(pctStr)/h (reset)"
        case .burning:
            let rateStr = String(format: "%.0f%%", ratePerHour)
            if let time = estimatedTimeToDepletion {
                let totalSeconds = Int(time.rounded())
                let hours = totalSeconds / 3600
                let minutes = (totalSeconds % 3600) / 60

                let timeStr: String
                if hours > 0 && minutes > 0 {
                    timeStr = isRussian ? "~\(hours)ч \(minutes)м" : "~\(hours)h \(minutes)m left"
                } else if hours > 0 {
                    timeStr = isRussian ? "~\(hours)ч" : "~\(hours)h left"
                } else {
                    let m = max(1, minutes)
                    timeStr = isRussian ? "~\(m)м" : "~\(m)m left"
                }

                if isRussian {
                    return "\(rateStr)/ч (хватит на \(timeStr))"
                } else {
                    return "\(rateStr)/h (\(timeStr))"
                }
            } else {
                return isRussian ? "\(rateStr)/ч" : "\(rateStr)/h"
            }
        }
    }
}

public struct QuotaHistorySample: Codable, Equatable, Sendable {
    public let timestamp: Date
    public let geminiPercentage: Double?
    public let claudePercentage: Double?

    public init(timestamp: Date = Date(), geminiPercentage: Double?, claudePercentage: Double?) {
        self.timestamp = timestamp
        self.geminiPercentage = geminiPercentage
        self.claudePercentage = claudePercentage
    }

    enum CodingKeys: String, CodingKey {
        case timestamp
        case geminiPercentage
        case claudePercentage
        case gemini
        case claude
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.timestamp = try container.decode(Date.self, forKey: .timestamp)
        if let g = try container.decodeIfPresent(Double.self, forKey: .geminiPercentage) {
            self.geminiPercentage = g
        } else {
            self.geminiPercentage = try container.decodeIfPresent(Double.self, forKey: .gemini)
        }
        if let c = try container.decodeIfPresent(Double.self, forKey: .claudePercentage) {
            self.claudePercentage = c
        } else {
            self.claudePercentage = try container.decodeIfPresent(Double.self, forKey: .claude)
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(timestamp, forKey: .timestamp)
        try container.encodeIfPresent(geminiPercentage, forKey: .geminiPercentage)
        try container.encodeIfPresent(claudePercentage, forKey: .claudePercentage)
    }
}

public typealias QuotaHistoryRecord = QuotaHistorySample

public final class QuotaHistoryTracker: @unchecked Sendable {
    public static var defaultStorageURL: URL {
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL(fileURLWithPath: ("~/Library/Application Support" as NSString).expandingTildeInPath)
        return appSupport
            .appendingPathComponent("AntigravityQuota", isDirectory: true)
            .appendingPathComponent("history.jsonl", isDirectory: false)
    }

    public static let shared = QuotaHistoryTracker(storageURL: defaultStorageURL)

    public let windowDuration: TimeInterval
    public let storageURL: URL?
    public let maxRetentionDays: Int

    private var samples: [QuotaHistorySample] = []
    private let lock = NSLock()
    private let diskQueue = DispatchQueue(label: "com.fuheshka.AntigravityQuota.history-disk", qos: .utility)

    public init(
        windowDuration: TimeInterval = 18000.0,
        storageURL: URL? = nil,
        maxRetentionDays: Int = 7,
        startDate: Date = Date()
    ) {
        self.windowDuration = windowDuration
        self.storageURL = storageURL
        self.maxRetentionDays = maxRetentionDays

        if let url = storageURL, FileManager.default.fileExists(atPath: url.path) {
            _ = diskQueue.sync {
                self.rotateHistoryDirect(now: startDate, retentionDays: maxRetentionDays, url: url)
            }
            let cutoff = startDate.addingTimeInterval(-windowDuration)
            let loaded = loadHistoryRecords().filter { $0.timestamp >= cutoff }
            self.samples = loaded
        }
    }

    public func record(gemini: Double?, claude: Double?, at date: Date = Date()) {
        let sample = QuotaHistorySample(timestamp: date, geminiPercentage: gemini, claudePercentage: claude)
        lock.lock()
        samples.append(sample)
        pruneInternal(now: date)
        lock.unlock()

        if storageURL != nil {
            diskQueue.async { [weak self] in
                self?.appendSampleToDisk(sample)
            }
        }
    }

    public func flushDiskQueue() {
        diskQueue.sync {}
    }

    private func appendSampleToDisk(_ sample: QuotaHistorySample) {
        guard let url = storageURL else { return }
        do {
            let dir = url.deletingLastPathComponent()
            if !FileManager.default.fileExists(atPath: dir.path) {
                try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            }
            let encoder = JSONEncoder()
            encoder.dateEncodingStrategy = .iso8601
            var data = try encoder.encode(sample)
            data.append(UInt8(ascii: "\n"))

            if FileManager.default.fileExists(atPath: url.path) {
                let fileHandle = try FileHandle(forWritingTo: url)
                defer { try? fileHandle.close() }
                try fileHandle.seekToEnd()
                try fileHandle.write(contentsOf: data)
            } else {
                try data.write(to: url, options: .atomic)
            }
        } catch {
            // Silently ignore disk write error to avoid failing foreground flows
        }
    }

    public func loadHistoryRecords() -> [QuotaHistorySample] {
        guard let url = storageURL else { return [] }
        return diskQueue.sync {
            self.loadHistoryRecordsDirect(from: url)
        }
    }

    private func loadHistoryRecordsDirect(from url: URL) -> [QuotaHistorySample] {
        guard FileManager.default.fileExists(atPath: url.path),
              let content = try? String(contentsOf: url, encoding: .utf8) else {
            return []
        }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601

        var records: [QuotaHistorySample] = []
        let lines = content.components(separatedBy: "\n")
        for line in lines {
            let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmed.isEmpty, let lineData = trimmed.data(using: .utf8) else { continue }
            if let sample = try? decoder.decode(QuotaHistorySample.self, from: lineData) {
                records.append(sample)
            }
        }
        return records
    }

    @discardableResult
    public func rotateHistory(now: Date = Date(), retentionDays: Int? = nil) -> Int {
        guard let url = storageURL else { return 0 }
        let days = retentionDays ?? maxRetentionDays
        return diskQueue.sync {
            self.rotateHistoryDirect(now: now, retentionDays: days, url: url)
        }
    }

    private func rotateHistoryDirect(now: Date, retentionDays: Int, url: URL) -> Int {
        guard FileManager.default.fileExists(atPath: url.path) else { return 0 }
        let existing = loadHistoryRecordsDirect(from: url)
        let cutoff = now.addingTimeInterval(-Double(retentionDays) * 86400.0)
        let retained = existing.filter { $0.timestamp >= cutoff }
        let prunedCount = existing.count - retained.count

        if prunedCount > 0 {
            rewriteHistoryFileDirect(records: retained, to: url)
        }
        return prunedCount
    }

    private func rewriteHistoryFileDirect(records: [QuotaHistorySample], to url: URL) {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        var buffer = ""
        for record in records {
            if let data = try? encoder.encode(record), let line = String(data: data, encoding: .utf8) {
                buffer.append(line)
                buffer.append("\n")
            }
        }
        try? buffer.write(to: url, atomically: true, encoding: .utf8)
    }

    public func record(snapshot: QuotaSnapshot, at date: Date = Date()) {
        let g = snapshot.geminiGroup?.fiveHourBucket?.percentage ?? snapshot.models.first(where: { $0.label.localizedCaseInsensitiveContains("gemini") })?.percentage
        let c = snapshot.claudeGroup?.fiveHourBucket?.percentage ?? snapshot.models.first(where: { $0.label.localizedCaseInsensitiveContains("claude") || $0.label.localizedCaseInsensitiveContains("gpt") })?.percentage
        record(gemini: g, claude: c, at: date)
    }

    public func prune(now: Date = Date()) {
        lock.lock()
        defer { lock.unlock() }
        pruneInternal(now: now)
    }

    private func pruneInternal(now: Date) {
        let cutoff = now.addingTimeInterval(-windowDuration)
        samples.removeAll { $0.timestamp < cutoff }
    }

    public func clear() {
        lock.lock()
        defer { lock.unlock() }
        samples.removeAll()
    }

    public var sampleCount: Int {
        lock.lock()
        defer { lock.unlock() }
        return samples.count
    }

    public func recentSamples(maxAge: TimeInterval = 18000.0, now: Date = Date()) -> [QuotaHistorySample] {
        lock.lock()
        defer { lock.unlock() }
        let cutoff = now.addingTimeInterval(-maxAge)
        return samples.filter { $0.timestamp >= cutoff && $0.timestamp <= now }
    }

    public func sparklineData(now: Date = Date()) -> QuotaSparklineData {
        let recent = recentSamples(maxAge: 18000.0, now: now)
        return QuotaSparklineBuilder.build(from: recent, now: now)
    }

    public func burnRate(for pool: QuotaPool, now: Date = Date()) -> QuotaBurnRate {
        lock.lock()
        defer { lock.unlock() }

        pruneInternal(now: now)

        let poolSamples: [(date: Date, pct: Double)] = samples.compactMap { s in
            let val = (pool == .gemini) ? s.geminiPercentage : s.claudePercentage
            guard let pct = val else { return nil }
            return (date: s.timestamp, pct: pct)
        }

        guard poolSamples.count >= 2 else {
            return QuotaBurnRate(ratePerHour: 0.0, trend: .stable, estimatedTimeToDepletion: nil)
        }

        // Check if there was a quota reset (sudden increase >= 15%) in the sample history
        var startIndex = 0
        for i in 1..<poolSamples.count {
            if poolSamples[i].pct - poolSamples[i - 1].pct >= 15.0 {
                startIndex = i
            }
        }

        let baselineSample = poolSamples[startIndex]
        let latestSample = poolSamples.last!
        let elapsed = latestSample.date.timeIntervalSince(baselineSample.date)

        // If the reset happened at the very latest sample (or elapsed time is too short to measure subsequent burn)
        if elapsed < 15.0 {
            if startIndex > 0 && startIndex == poolSamples.count - 1 {
                let prevSample = poolSamples[startIndex - 1]
                let jump = latestSample.pct - prevSample.pct
                let jumpElapsed = latestSample.date.timeIntervalSince(prevSample.date)
                let rate = (jump / max(1.0, jumpElapsed)) * 3600.0
                return QuotaBurnRate(ratePerHour: rate, trend: .recovering, estimatedTimeToDepletion: nil)
            }
            return QuotaBurnRate(ratePerHour: 0.0, trend: .stable, estimatedTimeToDepletion: nil)
        }

        let deltaPct = latestSample.pct - baselineSample.pct
        let hours = elapsed / 3600.0
        let ratePerHour = deltaPct / hours

        let trend: QuotaTrend
        let estimatedTimeToDepletion: TimeInterval?

        if ratePerHour < -0.5 {
            trend = .burning
            if latestSample.pct > 0 {
                let hoursRemaining = latestSample.pct / abs(ratePerHour)
                estimatedTimeToDepletion = hoursRemaining * 3600.0
            } else {
                estimatedTimeToDepletion = nil
            }
        } else if ratePerHour > 0.5 {
            trend = .recovering
            estimatedTimeToDepletion = nil
        } else {
            trend = .stable
            estimatedTimeToDepletion = nil
        }

        return QuotaBurnRate(
            ratePerHour: ratePerHour,
            trend: trend,
            estimatedTimeToDepletion: estimatedTimeToDepletion
        )
    }
}
