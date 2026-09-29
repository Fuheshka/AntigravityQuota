import XCTest
import Carbon
@testable import AntigravityQuotaCore

final class GlobalHotkeyTests: XCTestCase {

    func testActionKeyMapping() {
        let hud = GlobalHotkeyAction.toggleHUD
        XCTAssertEqual(hud.id, 1)
        XCTAssertEqual(hud.keyChar, "Q")
        XCTAssertEqual(hud.keyCode, 12, "kVK_ANSI_Q is 12")
        XCTAssertEqual(hud.carbonModifiers, 2560, "optionKey (2048) | shiftKey (512)")
        XCTAssertEqual(hud.symbolicShortcut, "⌥⇧Q")

        let pill = GlobalHotkeyAction.togglePillMode
        XCTAssertEqual(pill.id, 2)
        XCTAssertEqual(pill.keyChar, "M")
        XCTAssertEqual(pill.keyCode, 46, "kVK_ANSI_M is 46")
        XCTAssertEqual(pill.carbonModifiers, 2560)
        XCTAssertEqual(pill.symbolicShortcut, "⌥⇧M")

        let refresh = GlobalHotkeyAction.refreshQuotas
        XCTAssertEqual(refresh.id, 3)
        XCTAssertEqual(refresh.keyChar, "R")
        XCTAssertEqual(refresh.keyCode, 15, "kVK_ANSI_R is 15")
        XCTAssertEqual(refresh.carbonModifiers, 2560)
        XCTAssertEqual(refresh.symbolicShortcut, "⌥⇧R")

        XCTAssertEqual(GlobalHotkeyAction.allCases.count, 3)
    }

    func testGlobalHotkeyCoreMatching() {
        XCTAssertEqual(GlobalHotkeyCore.signature, 0x41475154, "Signature 'AGQT'")

        XCTAssertEqual(GlobalHotkeyCore.action(for: 1), .toggleHUD)
        XCTAssertEqual(GlobalHotkeyCore.action(for: 2), .togglePillMode)
        XCTAssertEqual(GlobalHotkeyCore.action(for: 3), .refreshQuotas)
        XCTAssertNil(GlobalHotkeyCore.action(for: 0))
        XCTAssertNil(GlobalHotkeyCore.action(for: 999))

        XCTAssertEqual(GlobalHotkeyCore.matches(signature: 0x41475154, id: 1), .toggleHUD)
        XCTAssertEqual(GlobalHotkeyCore.matches(signature: 0x41475154, id: 2), .togglePillMode)
        XCTAssertEqual(GlobalHotkeyCore.matches(signature: 0x41475154, id: 3), .refreshQuotas)

        // Invalid signature
        XCTAssertNil(GlobalHotkeyCore.matches(signature: 0x11223344, id: 1))
        // Invalid ID
        XCTAssertNil(GlobalHotkeyCore.matches(signature: 0x41475154, id: 42))
    }

    func testActionForKeyCodeAndModifiers() {
        let optShift: UInt32 = 2560

        XCTAssertEqual(GlobalHotkeyCore.action(forKeyCode: 12, modifiers: optShift), .toggleHUD)
        XCTAssertEqual(GlobalHotkeyCore.action(forKeyCode: 46, modifiers: optShift), .togglePillMode)
        XCTAssertEqual(GlobalHotkeyCore.action(forKeyCode: 15, modifiers: optShift), .refreshQuotas)

        // Wrong modifiers (e.g. Cmd only or empty)
        XCTAssertNil(GlobalHotkeyCore.action(forKeyCode: 12, modifiers: 256))
        XCTAssertNil(GlobalHotkeyCore.action(forKeyCode: 12, modifiers: 0))

        // Unknown key code
        XCTAssertNil(GlobalHotkeyCore.action(forKeyCode: 999, modifiers: optShift))
    }

    func testLocalizationStrings() {
        XCTAssertFalse(Localization.globalHotkeysTitle.isEmpty)
        XCTAssertFalse(Localization.globalHotkeysSetting.isEmpty)
        XCTAssertFalse(Localization.globalHotkeysMenuItem.isEmpty)
        XCTAssertFalse(Localization.globalHotkeyToggleHUD.isEmpty)
        XCTAssertFalse(Localization.globalHotkeyTogglePill.isEmpty)
        XCTAssertFalse(Localization.globalHotkeyRefresh.isEmpty)

        for action in GlobalHotkeyAction.allCases {
            XCTAssertFalse(action.localizedName.isEmpty)
        }
    }

    func testCarbonAPIActualRegistrationAndUnregistration() {
        for action in GlobalHotkeyAction.allCases {
            var hotKeyRef: EventHotKeyRef?
            let hotKeyID = EventHotKeyID(signature: GlobalHotkeyCore.signature, id: action.id)

            let regStatus = RegisterEventHotKey(
                action.keyCode,
                action.carbonModifiers,
                hotKeyID,
                GetApplicationEventTarget(),
                0,
                &hotKeyRef
            )

            XCTAssertEqual(regStatus, noErr, "Carbon RegisterEventHotKey for \(action.symbolicShortcut) must succeed with noErr (0)")
            XCTAssertNotNil(hotKeyRef, "Registered hotKeyRef must not be nil")

            if let ref = hotKeyRef {
                let unregStatus = UnregisterEventHotKey(ref)
                XCTAssertEqual(unregStatus, noErr, "Carbon UnregisterEventHotKey must return noErr (0)")
            }
        }
    }

    func testSimulatedDispatchLogic() {
        var hudToggled = false
        var pillToggled = false
        var quotasRefreshed = false

        let handler: (UInt32) -> Void = { id in
            guard let action = GlobalHotkeyCore.action(for: id) else { return }
            switch action {
            case .toggleHUD: hudToggled = true
            case .togglePillMode: pillToggled = true
            case .refreshQuotas: quotasRefreshed = true
            }
        }

        handler(1)
        XCTAssertTrue(hudToggled)
        XCTAssertFalse(pillToggled)
        XCTAssertFalse(quotasRefreshed)

        handler(2)
        XCTAssertTrue(pillToggled)
        XCTAssertFalse(quotasRefreshed)

        handler(3)
        XCTAssertTrue(quotasRefreshed)

        // Invalid ID
        handler(999)
    }
}
