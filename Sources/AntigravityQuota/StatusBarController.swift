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

    private var isTrendInMenuBarEnabled: Bool {
        get {
            if UserDefaults.standard.object(forKey: "ShowTrendInMenuBar") == nil {
                return true
            }
            return UserDefaults.standard.bool(forKey: "ShowTrendInMenuBar")
        }
        set {
            UserDefaults.standard.set(newValue, forKey: "ShowTrendInMenuBar")
        }
    }

    public init(hudController: QuotaHUDWindowController) {
        self.hudController = hudController
        self.statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        super.init()

        configureStatusButton(title: "—%")
        hudController.viewModel.onRefreshRequested = { [weak self] in
            self?.refreshNow()
        }
        setupGlobalHotkeys()
        rebuildMenu()
        setupWorkspaceObservers()
        refreshNow()
        updatePollingSchedule(forceReschedule: true)
        checkUpdatesOnStartup()
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
        self.hudController.viewModel.geminiBurnRate = nil
        self.hudController.viewModel.claudeBurnRate = nil
        self.hudController.updateVisibility()
        self.rebuildMenu()
        updatePollingSchedule(forceReschedule: true)
    }

    private func updateStatusButtonTitle() {
        guard let snap = latestSnapshot else {
            configureStatusButton(title: "Offline")
            return
        }
        let gBurn = QuotaHistoryTracker.shared.burnRate(for: .gemini)
        let cBurn = QuotaHistoryTracker.shared.burnRate(for: .claude)
        let title = snap.menuBarTitle(
            geminiTrend: gBurn.trend,
            claudeTrend: cBurn.trend,
            showTrend: isTrendInMenuBarEnabled
        )
        configureStatusButton(title: title)
    }

    @objc public func refreshNow() {
        hudController.viewModel.isRefreshing = true
        Task { @MainActor in
            let snap = await QuotaClient.shared.fetchSnapshot(forceRefresh: true)
            self.hudController.viewModel.isRefreshing = false
            self.latestSnapshot = snap
            self.hudController.viewModel.snapshot = snap
            self.hudController.updateVisibility()
            if let snap = snap {
                QuotaHistoryTracker.shared.record(snapshot: snap)
                QuotaNotificationManager.shared.processSnapshot(snap)
            }
            let gBurn = QuotaHistoryTracker.shared.burnRate(for: .gemini)
            let cBurn = QuotaHistoryTracker.shared.burnRate(for: .claude)
            self.hudController.viewModel.geminiBurnRate = gBurn
            self.hudController.viewModel.claudeBurnRate = cBurn
            self.hudController.viewModel.sparklineData = QuotaHistoryTracker.shared.sparklineData()

            self.updateStatusButtonTitle()
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
        refreshItem.keyEquivalentModifierMask = [.option, .shift]
        refreshItem.target = self
        menu.addItem(refreshItem)

        menu.addItem(.separator())

        let toggleHUDItem = NSMenuItem(title: Localization.showHUD, action: #selector(toggleHUD), keyEquivalent: "q")
        toggleHUDItem.keyEquivalentModifierMask = [.option, .shift]
        toggleHUDItem.target = self
        toggleHUDItem.state = hudController.isHUDEnabled ? .on : .off
        menu.addItem(toggleHUDItem)

        let toggleCompactItem = NSMenuItem(title: Localization.compactHUDMode, action: #selector(toggleCompactHUD), keyEquivalent: "m")
        toggleCompactItem.keyEquivalentModifierMask = [.option, .shift]
        toggleCompactItem.target = self
        toggleCompactItem.state = hudController.viewModel.isCompact ? .on : .off
        menu.addItem(toggleCompactItem)

        let toggleAutoHideItem = NSMenuItem(title: Localization.hudOnlyInAntigravity, action: #selector(toggleAutoHideHUD), keyEquivalent: "")
        toggleAutoHideItem.target = self
        toggleAutoHideItem.state = hudController.onlyWhenAntigravityActive ? .on : .off
        menu.addItem(toggleAutoHideItem)

        let hudSettingsItem = NSMenuItem(title: Localization.hudSettingsSubmenu, action: nil, keyEquivalent: "")
        let hudSettingsMenu = NSMenu()

        let clickThroughItem = NSMenuItem(title: Localization.clickThroughMenuItem, action: #selector(toggleClickThrough), keyEquivalent: "")
        clickThroughItem.target = self
        clickThroughItem.state = hudController.isClickThroughEnabled ? .on : .off
        hudSettingsMenu.addItem(clickThroughItem)

        hudSettingsMenu.addItem(.separator())

        let opacityTitleItem = NSMenuItem(title: Localization.hudOpacityTitle, action: nil, keyEquivalent: "")
        opacityTitleItem.isEnabled = false
        hudSettingsMenu.addItem(opacityTitleItem)

        let opacity100Item = NSMenuItem(title: Localization.opacity100, action: #selector(setHUDOpacity100), keyEquivalent: "")
        opacity100Item.target = self
        opacity100Item.state = abs(hudController.hudOpacity - 1.0) < 0.05 ? .on : .off
        hudSettingsMenu.addItem(opacity100Item)

        let opacity75Item = NSMenuItem(title: Localization.opacity75, action: #selector(setHUDOpacity75), keyEquivalent: "")
        opacity75Item.target = self
        opacity75Item.state = abs(hudController.hudOpacity - 0.75) < 0.05 ? .on : .off
        hudSettingsMenu.addItem(opacity75Item)

        let opacity50Item = NSMenuItem(title: Localization.opacity50, action: #selector(setHUDOpacity50), keyEquivalent: "")
        opacity50Item.target = self
        opacity50Item.state = abs(hudController.hudOpacity - 0.50) < 0.05 ? .on : .off
        hudSettingsMenu.addItem(opacity50Item)

        hudSettingsItem.submenu = hudSettingsMenu
        menu.addItem(hudSettingsItem)

        let launchAtLoginItem = NSMenuItem(title: Localization.launchAtLogin, action: #selector(toggleLaunchAtLogin), keyEquivalent: "")
        launchAtLoginItem.target = self
        launchAtLoginItem.state = LaunchAgentManager.isEnabled ? .on : .off
        menu.addItem(launchAtLoginItem)

        let toggleNotificationsItem = NSMenuItem(title: Localization.notificationsMenuItem, action: #selector(toggleNotifications), keyEquivalent: "")
        toggleNotificationsItem.target = self
        toggleNotificationsItem.state = QuotaNotificationManager.shared.isNotificationsEnabled ? .on : .off
        menu.addItem(toggleNotificationsItem)

        let toggleSoundAlertsItem = NSMenuItem(title: Localization.soundAlertsMenuItem, action: #selector(toggleSoundAlerts), keyEquivalent: "")
        toggleSoundAlertsItem.target = self
        toggleSoundAlertsItem.state = QuotaNotificationManager.shared.isSoundAlertsEnabled ? .on : .off
        menu.addItem(toggleSoundAlertsItem)

        let toggleTrendItem = NSMenuItem(title: Localization.showTrendInMenuBar, action: #selector(toggleShowTrendInMenuBar), keyEquivalent: "")
        toggleTrendItem.target = self
        toggleTrendItem.state = isTrendInMenuBarEnabled ? .on : .off
        menu.addItem(toggleTrendItem)

        let toggleGlobalHotkeysItem = NSMenuItem(title: Localization.globalHotkeysMenuItem, action: #selector(toggleGlobalHotkeys), keyEquivalent: "")
        toggleGlobalHotkeysItem.target = self
        toggleGlobalHotkeysItem.state = GlobalHotkeyManager.shared.isEnabled ? .on : .off
        menu.addItem(toggleGlobalHotkeysItem)

        menu.addItem(.separator())

        let checkForUpdatesItem = NSMenuItem(title: Localization.checkForUpdates, action: #selector(checkForUpdatesManually), keyEquivalent: "")
        checkForUpdatesItem.target = self
        menu.addItem(checkForUpdatesItem)

        let aboutItem = NSMenuItem(title: Localization.aboutMenuItem, action: #selector(showAboutWindow), keyEquivalent: "")
        aboutItem.target = self
        menu.addItem(aboutItem)

        let quitItem = NSMenuItem(title: Localization.quitApp, action: #selector(quitApp), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        self.statusItem.menu = menu
    }

    private func checkUpdatesOnStartup() {
        Task { @MainActor in
            try? await Task.sleep(nanoseconds: 3_000_000_000)
            let client = GitHubUpdateClient(repo: AntigravityQuotaGitHubRepo, currentVersion: AntigravityQuotaCurrentVersion)
            let result = await client.checkForUpdates(platform: .macOS, force: false)
            self.handleUpdateResult(result, isManual: false)
        }
    }

    @objc public func checkForUpdatesManually() {
        Task { @MainActor in
            let client = GitHubUpdateClient(repo: AntigravityQuotaGitHubRepo, currentVersion: AntigravityQuotaCurrentVersion)
            let result = await client.checkForUpdates(platform: .macOS, force: true)
            self.handleUpdateResult(result, isManual: true)
        }
    }

    private func handleUpdateResult(_ result: UpdateCheckResult, isManual: Bool) {
        switch result {
        case .updateAvailable(let newVersion, let release, let assetUrl):
            let alert = NSAlert()
            alert.messageText = Localization.updateAvailableTitle
            alert.informativeText = Localization.updateAvailableMessage(newVersion: "v\(newVersion)")
            alert.addButton(withTitle: Localization.downloadButton)
            alert.addButton(withTitle: Localization.laterButton)
            alert.alertStyle = .informational

            if alert.runModal() == .alertFirstButtonReturn {
                if let downloadUrl = assetUrl {
                    NSWorkspace.shared.open(downloadUrl)
                } else {
                    NSWorkspace.shared.open(release.htmlUrl)
                }
            }
        case .upToDate(let version):
            if isManual {
                let alert = NSAlert()
                alert.messageText = Localization.upToDateTitle
                alert.informativeText = Localization.upToDateMessage(version: version)
                alert.alertStyle = .informational
                alert.runModal()
            }
        case .throttled:
            if isManual {
                let alert = NSAlert()
                alert.messageText = Localization.upToDateTitle
                alert.informativeText = Localization.upToDateMessage(version: AntigravityQuotaCurrentVersion)
                alert.alertStyle = .informational
                alert.runModal()
            }
        case .failed(let reason):
            if isManual {
                let alert = NSAlert()
                alert.messageText = Localization.updateErrorTitle
                alert.informativeText = reason
                alert.alertStyle = .warning
                alert.runModal()
            }
        }
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

        let pool: QuotaPool = group.displayName.localizedCaseInsensitiveContains("gemini") ? .gemini : .claude
        let burn = QuotaHistoryTracker.shared.burnRate(for: pool)
        let burnStr = burn.formatted(isRussian: Localization.isRussian)
        let burnLine = String(format: "  • %@: %@ %@", Localization.isRussian ? "Динамика" : "Trend", burn.trend.rawValue, burnStr)
        let burnItem = NSMenuItem(title: burnLine, action: nil, keyEquivalent: "")
        burnItem.isEnabled = false
        menu.addItem(burnItem)

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

    @objc private func toggleClickThrough() {
        hudController.isClickThroughEnabled.toggle()
        rebuildMenu()
    }

    @objc private func setHUDOpacity100() {
        hudController.hudOpacity = 1.0
        rebuildMenu()
    }

    @objc private func setHUDOpacity75() {
        hudController.hudOpacity = 0.75
        rebuildMenu()
    }

    @objc private func setHUDOpacity50() {
        hudController.hudOpacity = 0.50
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

    @objc private func toggleSoundAlerts() {
        QuotaNotificationManager.shared.isSoundAlertsEnabled.toggle()
        rebuildMenu()
    }

    @objc private func toggleShowTrendInMenuBar() {
        isTrendInMenuBarEnabled.toggle()
        updateStatusButtonTitle()
        rebuildMenu()
    }

    private func setupGlobalHotkeys() {
        GlobalHotkeyManager.shared.onToggleHUD = { [weak self] in
            self?.toggleHUD()
        }
        GlobalHotkeyManager.shared.onTogglePillMode = { [weak self] in
            self?.toggleCompactHUD()
        }
        GlobalHotkeyManager.shared.onRefreshQuotas = { [weak self] in
            self?.refreshNow()
        }
        if GlobalHotkeyManager.shared.isEnabled {
            GlobalHotkeyManager.shared.registerHotkeys()
        }
    }

    @objc private func toggleGlobalHotkeys() {
        GlobalHotkeyManager.shared.isEnabled.toggle()
        rebuildMenu()
    }

    @objc private func quitApp() {
        NSApplication.shared.terminate(nil)
    }
}
