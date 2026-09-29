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

public struct QuotaHistorySample: Equatable, Sendable {
    public let timestamp: Date
    public let geminiPercentage: Double?
    public let claudePercentage: Double?

    public init(timestamp: Date = Date(), geminiPercentage: Double?, claudePercentage: Double?) {
        self.timestamp = timestamp
        self.geminiPercentage = geminiPercentage
        self.claudePercentage = claudePercentage
    }
}

public final class QuotaHistoryTracker: @unchecked Sendable {
    public static let shared = QuotaHistoryTracker()

    public let windowDuration: TimeInterval
    private var samples: [QuotaHistorySample] = []
    private let lock = NSLock()

    public init(windowDuration: TimeInterval = 3600.0) {
        self.windowDuration = windowDuration
    }

    public func record(gemini: Double?, claude: Double?, at date: Date = Date()) {
        lock.lock()
        defer { lock.unlock() }
        samples.append(QuotaHistorySample(timestamp: date, geminiPercentage: gemini, claudePercentage: claude))
        pruneInternal(now: date)
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
