import Cocoa
import AntigravityQuotaCore

@MainActor
public final class StatusBarController: NSObject {
    private let statusItem: NSStatusItem
    private let hudController: QuotaHUDWindowController
    private var pollTimer: Timer?
    private var latestSnapshot: QuotaSnapshot?
    private let pollScheduler = AdaptivePollScheduler()
    private var workspaceObservers: [NSObjectProtocol] = []

    public init(hudController: QuotaHUDWindowController) {
        self.hudController = hudController
        self.statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        super.init()

        configureStatusButton(title: "—%")
        hudController.viewModel.onRefreshRequested = { [weak self] in
            self?.refreshNow()
        }
        rebuildMenu()
        setupWorkspaceObservers()
        refreshNow()
        updatePollingSchedule(forceReschedule: true)
    }

    deinit {
        for obs in workspaceObservers {
            NSWorkspace.shared.notificationCenter.removeObserver(obs)
        }
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

    private func isAntigravityRunning() -> Bool {
        NSWorkspace.shared.runningApplications.contains { app in
            AdaptivePollPolicy.isAntigravityApp(
                bundleIdentifier: app.bundleIdentifier,
                localizedName: app.localizedName
            )
        }
    }

    private func updatePollingSchedule(forceReschedule: Bool = false) {
        let isRunning = isAntigravityRunning()
        let isFrontmost = hudController.isAntigravityFrontmost()
        let isServerUp = (latestSnapshot != nil) || (QuotaClient.shared.currentEndpoint != nil)

        let updateResult = pollScheduler.update(
            isAntigravityRunning: isRunning,
            isAntigravityFrontmost: isFrontmost,
            isLanguageServerRunning: isServerUp
        )

        if forceReschedule || pollTimer == nil || updateResult.didChange {
            scheduleTimer(interval: updateResult.interval)
        }
    }

    private func scheduleTimer(interval: TimeInterval) {
        pollTimer?.invalidate()
        pollTimer = Timer.scheduledTimer(withTimeInterval: interval, repeats: true) { [weak self] _ in
            Task { @MainActor in
                self?.refreshNow()
            }
        }
    }

    private func setupWorkspaceObservers() {
        let nc = NSWorkspace.shared.notificationCenter

        // 1. Instant refresh on wake from sleep without waiting for timer tick
        workspaceObservers.append(
            nc.addObserver(
                forName: NSWorkspace.didWakeNotification,
                object: nil,
                queue: .main
            ) { [weak self] _ in
                Task { @MainActor in
                    self?.handleWakeFromSleep()
                }
            }
        )

        // 2. Frontmost app activated (switch between 20s foreground and 60s background)
        workspaceObservers.append(
            nc.addObserver(
                forName: NSWorkspace.didActivateApplicationNotification,
                object: nil,
                queue: .main
            ) { [weak self] _ in
                Task { @MainActor in
                    self?.updatePollingSchedule()
                }
            }
        )

        // 3. Application launched (instant switch if Antigravity launched)
        workspaceObservers.append(
            nc.addObserver(
                forName: NSWorkspace.didLaunchApplicationNotification,
                object: nil,
                queue: .main
            ) { [weak self] notification in
                guard let self = self else { return }
                let app = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication
                if AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: app?.bundleIdentifier, localizedName: app?.localizedName) {
                    Task { @MainActor in
                        self.handleAntigravityLaunched()
                    }
                }
            }
        )

        // 4. Application terminated (instant switch to offline/closed mode if Antigravity terminated)
        workspaceObservers.append(
            nc.addObserver(
                forName: NSWorkspace.didTerminateApplicationNotification,
                object: nil,
                queue: .main
            ) { [weak self] notification in
                guard let self = self else { return }
                let app = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication
                if AdaptivePollPolicy.isAntigravityApp(bundleIdentifier: app?.bundleIdentifier, localizedName: app?.localizedName) {
                    Task { @MainActor in
                        self.handleAntigravityTerminated()
                    }
                }
            }
        )
    }

    private func handleWakeFromSleep() {
        refreshNow()
        updatePollingSchedule(forceReschedule: true)
    }

    private func handleAntigravityLaunched() {
        refreshNow()
        updatePollingSchedule(forceReschedule: true)
    }

    private func handleAntigravityTerminated() {
        self.latestSnapshot = nil
        self.configureStatusButton(title: "Offline")
        self.hudController.viewModel.snapshot = nil
        self.hudController.updateVisibility()
        self.rebuildMenu()
        updatePollingSchedule(forceReschedule: true)
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
            if let snap = snap {
                QuotaNotificationManager.shared.processSnapshot(snap)
            }
            self.rebuildMenu()
            self.updatePollingSchedule()
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

        let toggleNotificationsItem = NSMenuItem(title: Localization.notificationsMenuItem, action: #selector(toggleNotifications), keyEquivalent: "")
        toggleNotificationsItem.target = self
        toggleNotificationsItem.state = QuotaNotificationManager.shared.isNotificationsEnabled ? .on : .off
        menu.addItem(toggleNotificationsItem)

        menu.addItem(.separator())

        let aboutItem = NSMenuItem(title: Localization.aboutMenuItem, action: #selector(showAboutWindow), keyEquivalent: "")
        aboutItem.target = self
        menu.addItem(aboutItem)

        let quitItem = NSMenuItem(title: Localization.quitApp, action: #selector(quitApp), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        self.statusItem.menu = menu
    }

    @objc private func showAboutWindow() {
        AboutWindowController.shared.show(snapshot: latestSnapshot)
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

    @objc private func toggleNotifications() {
        QuotaNotificationManager.shared.isNotificationsEnabled.toggle()
        rebuildMenu()
    }

    @objc private func quitApp() {
        NSApplication.shared.terminate(nil)
    }
}
