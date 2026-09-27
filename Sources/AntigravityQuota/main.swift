import Cocoa

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var hudController: QuotaHUDWindowController?
    private var statusBarController: StatusBarController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        let hud = QuotaHUDWindowController()
        self.hudController = hud
        self.statusBarController = StatusBarController(hudController: hud)
    }
}

let app = NSApplication.shared
let delegate = MainActor.assumeIsolated { AppDelegate() }
app.delegate = delegate
app.run()
