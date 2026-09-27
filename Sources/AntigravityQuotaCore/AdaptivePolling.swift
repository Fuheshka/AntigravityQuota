import Foundation

public enum AdaptivePollState: String, Equatable, Sendable {
    case activeForeground
    case background
    case offlineOrClosed
}

public enum AdaptivePollPolicy {
    public static let activeInterval: TimeInterval = 20.0
    public static let backgroundInterval: TimeInterval = 60.0
    public static let offlineInterval: TimeInterval = 120.0

    public static func interval(for state: AdaptivePollState) -> TimeInterval {
        switch state {
        case .activeForeground:
            return activeInterval
        case .background:
            return backgroundInterval
        case .offlineOrClosed:
            return offlineInterval
        }
    }

    public static func determineState(
        isAntigravityRunning: Bool,
        isAntigravityFrontmost: Bool,
        isLanguageServerRunning: Bool
    ) -> AdaptivePollState {
        guard isAntigravityRunning && isLanguageServerRunning else {
            return .offlineOrClosed
        }
        return isAntigravityFrontmost ? .activeForeground : .background
    }

    public static func interval(
        isAntigravityRunning: Bool,
        isAntigravityFrontmost: Bool,
        isLanguageServerRunning: Bool
    ) -> TimeInterval {
        let state = determineState(
            isAntigravityRunning: isAntigravityRunning,
            isAntigravityFrontmost: isAntigravityFrontmost,
            isLanguageServerRunning: isLanguageServerRunning
        )
        return interval(for: state)
    }

    public static func isAntigravityApp(
        bundleIdentifier: String? = nil,
        localizedName: String? = nil
    ) -> Bool {
        let bundleId = bundleIdentifier?.lowercased() ?? ""
        let name = localizedName?.lowercased() ?? ""
        return bundleId.contains("antigravity") || name.contains("antigravity")
    }
}

public final class AdaptivePollScheduler {
    public private(set) var currentState: AdaptivePollState
    public private(set) var currentInterval: TimeInterval

    public init(initialState: AdaptivePollState = .offlineOrClosed) {
        self.currentState = initialState
        self.currentInterval = AdaptivePollPolicy.interval(for: initialState)
    }

    @discardableResult
    public func update(
        isAntigravityRunning: Bool,
        isAntigravityFrontmost: Bool,
        isLanguageServerRunning: Bool
    ) -> (state: AdaptivePollState, interval: TimeInterval, didChange: Bool) {
        let newState = AdaptivePollPolicy.determineState(
            isAntigravityRunning: isAntigravityRunning,
            isAntigravityFrontmost: isAntigravityFrontmost,
            isLanguageServerRunning: isLanguageServerRunning
        )
        let newInterval = AdaptivePollPolicy.interval(for: newState)
        let didChange = (newInterval != currentInterval) || (newState != currentState)
        self.currentState = newState
        self.currentInterval = newInterval
        return (newState, newInterval, didChange)
    }
}
