import Foundation
import AppKit
import UserNotifications
import Intents
import AntigravityQuotaCore

@MainActor
public final class QuotaNotificationManager: NSObject, UNUserNotificationCenterDelegate {
    public static let shared = QuotaNotificationManager()
    public static let userDefaultsKey = "QuotaNotificationsEnabled"
    public static let soundAlertsUserDefaultsKey = "QuotaSoundAlertsEnabled"

    private let evaluator = QuotaNotificationEvaluator()
    private var hasRequestedAuth = false

    public var isNotificationsEnabled: Bool {
        get {
            // Default to true if not explicitly disabled by user
            if UserDefaults.standard.object(forKey: Self.userDefaultsKey) == nil {
                return true
            }
            return UserDefaults.standard.bool(forKey: Self.userDefaultsKey)
        }
        set {
            UserDefaults.standard.set(newValue, forKey: Self.userDefaultsKey)
            if newValue {
                requestAuthorizationIfNeeded()
            }
        }
    }

    public var isSoundAlertsEnabled: Bool {
        get {
            // Default to true if not explicitly disabled by user
            if UserDefaults.standard.object(forKey: Self.soundAlertsUserDefaultsKey) == nil {
                return true
            }
            return UserDefaults.standard.bool(forKey: Self.soundAlertsUserDefaultsKey)
        }
        set {
            UserDefaults.standard.set(newValue, forKey: Self.soundAlertsUserDefaultsKey)
        }
    }

    public static func isDoNotDisturbActive() -> Bool {
        if #available(macOS 12.0, *) {
            if INFocusStatusCenter.default.focusStatus.isFocused == true {
                return true
            }
        }
        return CFPreferencesGetAppBooleanValue("doNotDisturb" as CFString, "com.apple.notificationcenterui" as CFString, nil)
    }

    public var dndChecker: () -> Bool = { QuotaNotificationManager.isDoNotDisturbActive() }
    public var soundPlayer: (QuotaSoundAlert) -> Void = { soundAlert in
        for name in soundAlert.systemSoundNames {
            if let sound = NSSound(named: NSSound.Name(name)) {
                sound.play()
                return
            }
        }
    }

    private override init() {
        super.init()
        UNUserNotificationCenter.current().delegate = self
        if isNotificationsEnabled {
            requestAuthorizationIfNeeded()
        }
    }

    public func requestAuthorizationIfNeeded() {
        guard !hasRequestedAuth else { return }
        hasRequestedAuth = true

        UNUserNotificationCenter.current().getNotificationSettings { settings in
            if settings.authorizationStatus == .notDetermined {
                UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { granted, error in
                    if let error = error {
                        print("[AntigravityQuota] Notification authorization error: \(error.localizedDescription)")
                    }
                }
            }
        }
    }

    public func processSnapshot(_ snapshot: QuotaSnapshot) {
        guard isNotificationsEnabled || isSoundAlertsEnabled else { return }

        let events = evaluator.evaluate(snapshot: snapshot)
        let dndActive = dndChecker()

        for event in events {
            if isNotificationsEnabled {
                sendNotification(for: event)
            }
            if let soundAlert = QuotaSoundDecision.soundToPlay(
                for: event,
                soundAlertsEnabled: isSoundAlertsEnabled,
                isDoNotDisturbActive: dndActive
            ) {
                soundPlayer(soundAlert)
            }
        }
    }

    private func sendNotification(for event: QuotaNotificationEvent) {
        let content = UNMutableNotificationContent()
        content.title = event.title
        content.body = event.body
        content.sound = nil

        let identifier: String
        switch event {
        case .reset(let pool):
            identifier = "antigravity.quota.reset.\(pool).\(Int(Date().timeIntervalSince1970))"
        case .lowQuota(let pool, _):
            identifier = "antigravity.quota.low.\(pool).\(Int(Date().timeIntervalSince1970))"
        }

        let request = UNNotificationRequest(identifier: identifier, content: content, trigger: nil)
        UNUserNotificationCenter.current().add(request) { error in
            if let error = error {
                print("[AntigravityQuota] Failed to deliver notification: \(error.localizedDescription)")
            }
        }
    }

    // MARK: - UNUserNotificationCenterDelegate
    // Present banner even if app is active/accessory
    nonisolated public func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification,
        withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void
    ) {
        if #available(macOS 14.0, *) {
            completionHandler([.banner, .list])
        } else {
            completionHandler([.banner])
        }
    }
}

