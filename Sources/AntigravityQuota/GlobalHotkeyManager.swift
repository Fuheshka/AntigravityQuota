import Cocoa
import Carbon
import AntigravityQuotaCore

/// Global Hotkey Manager powered by native Carbon Event HotKey API.
/// Does NOT require Accessibility (TCC) permissions.
@MainActor
public final class GlobalHotkeyManager: NSObject {
    public static let shared = GlobalHotkeyManager()
    public static let userDefaultsKey = "GlobalHotkeysEnabled"

    public var isEnabled: Bool {
        get {
            if UserDefaults.standard.object(forKey: Self.userDefaultsKey) == nil {
                return true
            }
            return UserDefaults.standard.bool(forKey: Self.userDefaultsKey)
        }
        set {
            UserDefaults.standard.set(newValue, forKey: Self.userDefaultsKey)
            if newValue {
                registerHotkeys()
            } else {
                unregisterHotkeys()
            }
        }
    }

    public var onToggleHUD: (() -> Void)?
    public var onTogglePillMode: (() -> Void)?
    public var onRefreshQuotas: (() -> Void)?

    private var eventHandlerRef: EventHandlerRef?
    private var registeredHotKeys: [UInt32: EventHotKeyRef] = [:]
    private(set) public var isRegistered: Bool = false

    private override init() {
        super.init()
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleAppWillTerminate),
            name: NSApplication.willTerminateNotification,
            object: nil
        )
    }

    deinit {
        // Direct unregistration in deinit
        for (_, ref) in registeredHotKeys {
            UnregisterEventHotKey(ref)
        }
        registeredHotKeys.removeAll()
        if let handler = eventHandlerRef {
            RemoveEventHandler(handler)
        }
    }

    @objc private func handleAppWillTerminate() {
        unregisterHotkeys()
    }

    public func registerHotkeys() {
        guard isEnabled else { return }

        // If already registered, unregister first to prevent duplicates
        if isRegistered {
            unregisterHotkeys()
        }

        installEventHandlerIfNeeded()

        for action in GlobalHotkeyAction.allCases {
            var hotKeyRef: EventHotKeyRef?
            let hotKeyID = EventHotKeyID(signature: GlobalHotkeyCore.signature, id: action.id)
            let status = RegisterEventHotKey(
                action.keyCode,
                action.carbonModifiers,
                hotKeyID,
                GetApplicationEventTarget(),
                0,
                &hotKeyRef
            )

            if status == noErr, let ref = hotKeyRef {
                registeredHotKeys[action.id] = ref
            } else {
                NSLog("[GlobalHotkeyManager] Failed to register %@ (code: %u): %d", action.symbolicShortcut, action.keyCode, status)
            }
        }

        isRegistered = !registeredHotKeys.isEmpty
    }

    public func unregisterHotkeys() {
        for (_, ref) in registeredHotKeys {
            UnregisterEventHotKey(ref)
        }
        registeredHotKeys.removeAll()

        if let handler = eventHandlerRef {
            RemoveEventHandler(handler)
            eventHandlerRef = nil
        }

        isRegistered = false
    }

    public func dispatchAction(id: UInt32) {
        guard isEnabled else { return }
        guard let action = GlobalHotkeyCore.action(for: id) else { return }

        switch action {
        case .toggleHUD:
            onToggleHUD?()
        case .togglePillMode:
            onTogglePillMode?()
        case .refreshQuotas:
            onRefreshQuotas?()
        }
    }

    private func installEventHandlerIfNeeded() {
        guard eventHandlerRef == nil else { return }

        var eventType = EventTypeSpec(
            eventClass: OSType(kEventClassKeyboard),
            eventKind: UInt32(kEventHotKeyPressed)
        )

        let status = InstallEventHandler(
            GetApplicationEventTarget(),
            { (nextHandler, theEvent, userData) -> OSStatus in
                guard let theEvent = theEvent else {
                    return OSStatus(eventNotHandledErr)
                }

                var hotKeyID = EventHotKeyID()
                let paramStatus = GetEventParameter(
                    theEvent,
                    EventParamName(kEventParamDirectObject),
                    EventParamType(typeEventHotKeyID),
                    nil,
                    MemoryLayout<EventHotKeyID>.size,
                    nil,
                    &hotKeyID
                )

                if paramStatus == noErr, hotKeyID.signature == GlobalHotkeyCore.signature {
                    if let ptr = userData {
                        let manager = Unmanaged<GlobalHotkeyManager>.fromOpaque(ptr).takeUnretainedValue()
                        DispatchQueue.main.async {
                            manager.dispatchAction(id: hotKeyID.id)
                        }
                        return noErr
                    }
                }

                return CallNextEventHandler(nextHandler, theEvent)
            },
            1,
            &eventType,
            UnsafeMutableRawPointer(Unmanaged.passUnretained(self).toOpaque()),
            &eventHandlerRef
        )

        if status != noErr {
            NSLog("[GlobalHotkeyManager] Failed to install Carbon event handler: %d", status)
        }
    }
}
