import Cocoa
import AntigravityQuotaCore

@MainActor
public final class StatusBarController: NSObject {
    private let statusItem: NSStatusItem
    private let hudController: QuotaHUDWindowController
    private var pollTimer: Timer?
    private var latestSnapshot: QuotaSnapshot?

    public init(hudController: QuotaHUDWindowController) {
        self.hudController = hudController
        self.statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        super.init()

        configureStatusButton(title: "—%")
        hudController.viewModel.onRefreshRequested = { [weak self] in
            self?.refreshNow()
        }
        rebuildMenu()
        refreshNow()
        startPolling()
    }

    private func configureStatusButton(title: String) {
        guard let button = statusItem.button else { return }
        let symbolConfig = NSImage.SymbolConfiguration(pointSize: 13, weight: .semibold)
        let image = NSImage(
            systemSymbolName: "gauge.with.dots.needle.67percent",
            accessibilityDescription: "Antigravity Quota"
        )?.withSymbolConfiguration(symbolConfig) ?? NSImage(
            systemSymbolName: "bolt.horizontal.circle",
            accessibilityDescription: "Antigravity Quota"
        )?.withSymbolConfiguration(symbolConfig)

        image?.isTemplate = true
        button.image = image
        button.imagePosition = .imageLeading
        button.font = NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .medium)
        button.title = " " + title
    }

    private func startPolling() {
        pollTimer?.invalidate()
        pollTimer = Timer.scheduledTimer(withTimeInterval: 20.0, repeats: true) { [weak self] _ in
            Task { @MainActor in
                self?.refreshNow()
            }
        }
    }

    @objc public func refreshNow() {
        hudController.viewModel.isRefreshing = true
        Task { @MainActor in
            let snap = await QuotaClient.shared.fetchSnapshot(forceRefresh: true)
            self.hudController.viewModel.isRefreshing = false
            self.latestSnapshot = snap
            self.hudController.viewModel.snapshot = snap
            self.hudController.updateVisibility()
            self.configureStatusButton(title: snap?.menuBarTitle ?? "Offline")
            self.rebuildMenu()
        }
    }

    private func rebuildMenu() {
        let menu = NSMenu()

        let headerItem = NSMenuItem(title: Localization.appTitle, action: nil, keyEquivalent: "")
        headerItem.isEnabled = false
        menu.addItem(headerItem)
        menu.addItem(.separator())

        if let snap = latestSnapshot {
            if let gemini = snap.geminiGroup {
                addGroupSection(to: menu, title: Localization.geminiPoolTitle, group: gemini)
                menu.addItem(.separator())
            }
            if let claude = snap.claudeGroup {
                addGroupSection(to: menu, title: Localization.claudePoolTitle, group: claude)
                menu.addItem(.separator())
            }

            if !snap.models.isEmpty {
                let modelsItem = NSMenuItem(title: Localization.allModelsSubmenu, action: nil, keyEquivalent: "")
                let sub = NSMenu()
                for m in snap.models {
                    let resetStr = QuotaFormatter.formatCountdown(to: m.resetDate)
                    let line = String(format: "%@: %.1f%% (↻ %@)", m.label, m.percentage, resetStr)
                    let item = NSMenuItem(title: line, action: nil, keyEquivalent: "")
                    item.isEnabled = false
                    sub.addItem(item)
                }
                modelsItem.submenu = sub
                menu.addItem(modelsItem)
                menu.addItem(.separator())
            }

            let updatedTime = QuotaFormatter.formatClockTime(snap.updatedAt)
            let updatedItem = NSMenuItem(title: "\(Localization.updatedPrefix) \(updatedTime)", action: nil, keyEquivalent: "")
            updatedItem.isEnabled = false
            menu.addItem(updatedItem)
        } else {
            let offlineItem = NSMenuItem(title: Localization.serverOffline, action: nil, keyEquivalent: "")
            offlineItem.isEnabled = false
            menu.addItem(offlineItem)
        }

        let refreshItem = NSMenuItem(title: Localization.refreshNow, action: #selector(refreshNow), keyEquivalent: "r")
        refreshItem.target = self
        menu.addItem(refreshItem)

        menu.addItem(.separator())

        let toggleHUDItem = NSMenuItem(title: Localization.showHUD, action: #selector(toggleHUD), keyEquivalent: "h")
        toggleHUDItem.target = self
        toggleHUDItem.state = hudController.isHUDEnabled ? .on : .off
        menu.addItem(toggleHUDItem)

        let toggleCompactItem = NSMenuItem(title: Localization.compactHUDMode, action: #selector(toggleCompactHUD), keyEquivalent: "m")
        toggleCompactItem.target = self
        toggleCompactItem.state = hudController.viewModel.isCompact ? .on : .off
        menu.addItem(toggleCompactItem)

        let toggleAutoHideItem = NSMenuItem(title: Localization.hudOnlyInAntigravity, action: #selector(toggleAutoHideHUD), keyEquivalent: "")
        toggleAutoHideItem.target = self
        toggleAutoHideItem.state = hudController.onlyWhenAntigravityActive ? .on : .off
        menu.addItem(toggleAutoHideItem)

        let launchAtLoginItem = NSMenuItem(title: Localization.launchAtLogin, action: #selector(toggleLaunchAtLogin), keyEquivalent: "")
        launchAtLoginItem.target = self
        launchAtLoginItem.state = LaunchAgentManager.isEnabled ? .on : .off
        menu.addItem(launchAtLoginItem)

        menu.addItem(.separator())

        let quitItem = NSMenuItem(title: Localization.quitApp, action: #selector(quitApp), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        self.statusItem.menu = menu
    }

    private func addGroupSection(to menu: NSMenu, title: String, group: QuotaGroup) {
        let titleItem = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        titleItem.isEnabled = false
        menu.addItem(titleItem)

        if let b5 = group.fiveHourBucket {
            let cd = QuotaFormatter.formatCountdown(to: b5.resetDate)
            let clk = QuotaFormatter.formatClockTime(b5.resetDate)
            let line = String(format: "  • %@: %.1f%% (%@ %@ / %@)", Localization.fiveHourWindow, b5.percentage, Localization.resetPrefix, cd, clk)
            let item = NSMenuItem(title: line, action: nil, keyEquivalent: "")
            item.isEnabled = false
            menu.addItem(item)
        }

        if let bw = group.weeklyBucket {
            let cd = QuotaFormatter.formatCountdown(to: bw.resetDate)
            let line = String(format: "  • %@: %.1f%% (%@ %@)", Localization.weeklyWindow, bw.percentage, Localization.resetPrefix, cd)
            let item = NSMenuItem(title: line, action: nil, keyEquivalent: "")
            item.isEnabled = false
            menu.addItem(item)
        }
    }

    @objc private func toggleHUD() {
        hudController.isHUDEnabled.toggle()
        rebuildMenu()
    }

    @objc private func toggleCompactHUD() {
        hudController.viewModel.isCompact.toggle()
        rebuildMenu()
    }

    @objc private func toggleAutoHideHUD() {
        hudController.onlyWhenAntigravityActive.toggle()
        rebuildMenu()
    }

    @objc private func toggleLaunchAtLogin() {
        LaunchAgentManager.setEnabled(!LaunchAgentManager.isEnabled)
        rebuildMenu()
    }

    @objc private func quitApp() {
        NSApplication.shared.terminate(nil)
    }
}
