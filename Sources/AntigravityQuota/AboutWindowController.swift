import Cocoa
import SwiftUI
import AntigravityQuotaCore

@MainActor
public final class AboutWindowController: NSObject, NSWindowDelegate {
    public static let shared = AboutWindowController()

    private var window: NSWindow?
    private let viewModel = AboutViewModel()

    private override init() {
        super.init()
    }

    public func show(snapshot: QuotaSnapshot?) {
        viewModel.snapshot = snapshot
        viewModel.endpoint = QuotaClient.shared.currentEndpoint ?? ServerDiscovery.discoverActiveServer()
        viewModel.isNotificationsEnabled = QuotaNotificationManager.shared.isNotificationsEnabled
        viewModel.isSoundAlertsEnabled = QuotaNotificationManager.shared.isSoundAlertsEnabled
        viewModel.isGlobalHotkeysEnabled = GlobalHotkeyManager.shared.isEnabled
        viewModel.geminiBurnRate = QuotaHistoryTracker.shared.burnRate(for: .gemini)
        viewModel.claudeBurnRate = QuotaHistoryTracker.shared.burnRate(for: .claude)

        if let existing = window {
            existing.center()
            existing.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }

        let contentView = AboutView(viewModel: viewModel)
        let hostingView = NSHostingView(rootView: contentView)

        let win = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 440, height: 530),
            styleMask: [.titled, .closable, .miniaturizable],
            backing: .buffered,
            defer: false
        )
        win.title = Localization.aboutTitle
        win.titlebarAppearsTransparent = true
        win.isReleasedWhenClosed = false
        win.contentView = hostingView
        win.center()
        win.delegate = self

        self.window = win
        win.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }

    public func windowWillClose(_ notification: Notification) {
        // Kept for re-opening
    }
}

@MainActor
final class AboutViewModel: ObservableObject {
    @Published var snapshot: QuotaSnapshot?
    @Published var endpoint: ServerEndpoint?
    @Published var geminiBurnRate: QuotaBurnRate?
    @Published var claudeBurnRate: QuotaBurnRate?
    @Published var copiedFeedback: Bool = false
    @Published var isNotificationsEnabled: Bool = QuotaNotificationManager.shared.isNotificationsEnabled {
        didSet {
            QuotaNotificationManager.shared.isNotificationsEnabled = isNotificationsEnabled
        }
    }
    @Published var isSoundAlertsEnabled: Bool = QuotaNotificationManager.shared.isSoundAlertsEnabled {
        didSet {
            QuotaNotificationManager.shared.isSoundAlertsEnabled = isSoundAlertsEnabled
        }
    }
    @Published var isGlobalHotkeysEnabled: Bool = GlobalHotkeyManager.shared.isEnabled {
        didSet {
            GlobalHotkeyManager.shared.isEnabled = isGlobalHotkeysEnabled
        }
    }

    func copyDiagnostics() {
        let text = QuotaClient.buildDiagnosticReport(endpoint: endpoint, snapshot: snapshot)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)

        copiedFeedback = true
        Task { @MainActor in
            try? await Task.sleep(nanoseconds: 2_000_000_000)
            self.copiedFeedback = false
        }
    }

    func openGitHub() {
        if let url = URL(string: "https://github.com/Fuheshka/AntigravityQuota") {
            NSWorkspace.shared.open(url)
        }
    }

    func openReleases() {
        if let url = URL(string: "https://github.com/Fuheshka/AntigravityQuota/releases") {
            NSWorkspace.shared.open(url)
        }
    }

    @Published var isCheckingForUpdates: Bool = false
    @Published var updateStatusMessage: String? = nil

    func checkForUpdates() {
        guard !isCheckingForUpdates else { return }
        isCheckingForUpdates = true
        updateStatusMessage = Localization.checkingForUpdates

        Task { @MainActor in
            let client = GitHubUpdateClient(repo: AntigravityQuotaGitHubRepo, currentVersion: AntigravityQuotaCurrentVersion)
            let result = await client.checkForUpdates(platform: .macOS, force: true)
            self.isCheckingForUpdates = false

            switch result {
            case .updateAvailable(let newVersion, let release, let assetUrl):
                self.updateStatusMessage = "\(Localization.updateAvailableTitle): v\(newVersion)"
                self.showUpdateAlert(release: release, newVersion: newVersion, assetUrl: assetUrl)
            case .upToDate(let version):
                self.updateStatusMessage = Localization.upToDateMessage(version: version)
            case .throttled:
                self.updateStatusMessage = Localization.upToDateMessage(version: AntigravityQuotaCurrentVersion)
            case .failed(let reason):
                self.updateStatusMessage = "\(Localization.updateErrorTitle): \(reason)"
            }
        }
    }

    private func showUpdateAlert(release: GitHubReleaseInfo, newVersion: String, assetUrl: URL?) {
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
    }
}

struct AboutView: View {
    @ObservedObject var viewModel: AboutViewModel

    private var appIconImage: NSImage? {
        if let icon = NSImage(named: "AppIcon") {
            return icon
        }
        if let bundleIcon = Bundle.main.image(forResource: "AppIcon") {
            return bundleIcon
        }
        return nil
    }

    var body: some View {
        VStack(spacing: 16) {
            // Header
            HStack(spacing: 14) {
                if let icon = appIconImage {
                    Image(nsImage: icon)
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: 58, height: 58)
                        .clipShape(RoundedRectangle(cornerRadius: 13, style: .continuous))
                        .shadow(color: .black.opacity(0.2), radius: 4, x: 0, y: 2)
                } else {
                    ZStack {
                        RoundedRectangle(cornerRadius: 13, style: .continuous)
                            .fill(LinearGradient(
                                colors: [Color(red: 0.25, green: 0.28, blue: 0.8), Color(red: 0.12, green: 0.13, blue: 0.35)],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing
                            ))
                            .frame(width: 58, height: 58)

                        Image(systemName: "gauge.with.dots.needle.67percent")
                            .font(.system(size: 28, weight: .semibold))
                            .foregroundColor(.white)
                    }
                }

                VStack(alignment: .leading, spacing: 3) {
                    HStack(alignment: .firstTextBaseline, spacing: 8) {
                        Text("AntigravityQuota")
                            .font(.system(size: 19, weight: .bold))

                        Button(action: { viewModel.checkForUpdates() }) {
                            HStack(spacing: 4) {
                                Text("v\(AntigravityQuotaCurrentVersion)")
                                    .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                if viewModel.isCheckingForUpdates {
                                    ProgressView()
                                        .controlSize(.mini)
                                } else {
                                    Image(systemName: "arrow.triangle.2.circlepath")
                                        .font(.system(size: 9))
                                }
                            }
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .background(Color.accentColor.opacity(0.15))
                            .foregroundColor(.accentColor)
                            .clipShape(Capsule())
                        }
                        .buttonStyle(.plain)
                        .help(Localization.checkForUpdates)
                    }

                    if let status = viewModel.updateStatusMessage {
                        Text(status)
                            .font(.system(size: 10.5, weight: .medium))
                            .foregroundColor(.accentColor)
                            .transition(.opacity)
                    }

                    Text(Localization.aboutSubtitle)
                        .font(.system(size: 11.5))
                        .foregroundColor(.secondary)
                        .fixedSize(horizontal: false, vertical: true)

                    Link(Localization.aboutAuthor, destination: URL(string: "https://github.com/Fuheshka")!)
                        .font(.system(size: 11, weight: .medium))
                        .foregroundColor(.accentColor)
                }
                Spacer()
            }
            .padding(.horizontal, 18)
            .padding(.top, 16)

            Divider()
                .padding(.horizontal, 16)

            // Content ScrollView
            ScrollView(.vertical, showsIndicators: true) {
                VStack(spacing: 12) {
                    statusSection
                    settingsSection
                    shortcutsSection
                    tipsSection
                }
                .padding(.horizontal, 18)
                .padding(.vertical, 4)
            }

            Divider()
                .padding(.horizontal, 16)

            // Footer
            HStack(spacing: 10) {
                Button(action: { viewModel.copyDiagnostics() }) {
                    HStack(spacing: 5) {
                        Image(systemName: viewModel.copiedFeedback ? "checkmark" : "doc.on.doc")
                            .font(.system(size: 11, weight: .medium))
                        Text(viewModel.copiedFeedback ? Localization.aboutCopiedNotice : Localization.aboutCopyDiagnosticsButton)
                            .font(.system(size: 11.5))
                    }
                }
                .buttonStyle(.bordered)
                .tint(viewModel.copiedFeedback ? .green : .primary)

                Spacer()

                Button(action: { viewModel.openGitHub() }) {
                    Text(Localization.aboutGitHubButton)
                        .font(.system(size: 11.5))
                }
                .buttonStyle(.bordered)

                Button(action: { viewModel.openReleases() }) {
                    Text(Localization.aboutReleasesButton)
                        .font(.system(size: 11.5))
                }
                .buttonStyle(.borderedProminent)
            }
            .padding(.horizontal, 18)
            .padding(.bottom, 16)
        }
        .frame(width: 440, height: 530)
    }

    private var statusSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 6) {
                Circle()
                    .fill(viewModel.endpoint != nil ? Color.green : Color.orange)
                    .frame(width: 8, height: 8)
                Text(Localization.aboutServerStatusTitle)
                    .font(.system(size: 12, weight: .bold))
                Spacer()
                if let snap = viewModel.snapshot {
                    Text("\(Localization.updatedPrefix) \(QuotaFormatter.formatClockTime(snap.updatedAt))")
                        .font(.system(size: 10.5, design: .monospaced))
                        .foregroundColor(.secondary)
                }
            }

            VStack(alignment: .leading, spacing: 4) {
                if let ep = viewModel.endpoint {
                    Text("\(Localization.aboutServerConnected) • PID: \(ep.pid) • \(Localization.aboutPort): \(ep.ports.map(String.init).joined(separator: ", "))")
                        .font(.system(size: 11, design: .monospaced))
                        .foregroundColor(.secondary)

                    if let snap = viewModel.snapshot {
                        VStack(alignment: .leading, spacing: 5) {
                            if let g = snap.geminiGroup?.fiveHourBucket {
                                let cd = QuotaFormatter.formatCountdown(to: g.resetDate)
                                let burn = viewModel.geminiBurnRate
                                let burnStr = burn?.formatted(isRussian: Localization.isRussian) ?? "~0%/ч"
                                let trend = burn?.trend.rawValue ?? "→"
                                HStack(spacing: 6) {
                                    Text("Gemini:")
                                        .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                        .foregroundColor(.green)
                                    Text("\(String(format: "%.0f%%", g.percentage)) (\(cd))")
                                        .font(.system(size: 11, design: .monospaced))
                                        .foregroundColor(.primary.opacity(0.85))
                                    Text("•")
                                        .foregroundColor(.secondary)
                                    Text("\(trend) \(burnStr)")
                                        .font(.system(size: 11, weight: .medium, design: .monospaced))
                                        .foregroundColor(.secondary)
                                }
                            }
                            if let c = snap.claudeGroup?.fiveHourBucket {
                                let cd = QuotaFormatter.formatCountdown(to: c.resetDate)
                                let burn = viewModel.claudeBurnRate
                                let burnStr = burn?.formatted(isRussian: Localization.isRussian) ?? "~0%/ч"
                                let trend = burn?.trend.rawValue ?? "→"
                                HStack(spacing: 6) {
                                    Text("Claude:")
                                        .font(.system(size: 11, weight: .semibold, design: .monospaced))
                                        .foregroundColor(.cyan)
                                    Text("\(String(format: "%.0f%%", c.percentage)) (\(cd))")
                                        .font(.system(size: 11, design: .monospaced))
                                        .foregroundColor(.primary.opacity(0.85))
                                    Text("•")
                                        .foregroundColor(.secondary)
                                    Text("\(trend) \(burnStr)")
                                        .font(.system(size: 11, weight: .medium, design: .monospaced))
                                        .foregroundColor(.secondary)
                                }
                            }
                        }
                    }
                } else {
                    Text(Localization.aboutServerDisconnected)
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                }
            }
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.primary.opacity(0.04))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    private var settingsSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(Localization.notificationsMenuItem)
                .font(.system(size: 12, weight: .bold))

            Toggle(isOn: $viewModel.isNotificationsEnabled) {
                Text(Localization.aboutSettingNotifications)
                    .font(.system(size: 11))
            }
            .toggleStyle(.switch)
            .controlSize(.small)

            Toggle(isOn: $viewModel.isSoundAlertsEnabled) {
                Text(Localization.aboutSettingSoundAlerts)
                    .font(.system(size: 11))
            }
            .toggleStyle(.switch)
            .controlSize(.small)

            Divider()

            Text(Localization.globalHotkeysTitle)
                .font(.system(size: 12, weight: .bold))

            Toggle(isOn: $viewModel.isGlobalHotkeysEnabled) {
                Text(Localization.globalHotkeysSetting)
                    .font(.system(size: 11))
            }
            .toggleStyle(.switch)
            .controlSize(.small)
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.primary.opacity(0.04))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    private var shortcutsSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(Localization.globalHotkeysTitle)
                .font(.system(size: 12, weight: .bold))

            shortcutRow(key: "⌥ ⇧ Q", desc: Localization.globalHotkeyToggleHUD)
            shortcutRow(key: "⌥ ⇧ M", desc: Localization.globalHotkeyTogglePill)
            shortcutRow(key: "⌥ ⇧ R", desc: Localization.globalHotkeyRefresh)

            Divider()
                .padding(.vertical, 2)

            Text(Localization.aboutShortcutsTitle)
                .font(.system(size: 12, weight: .bold))

            shortcutRow(key: "⌘ Q", desc: Localization.aboutShortcutQuit)
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.primary.opacity(0.04))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    private func shortcutRow(key: String, desc: String) -> some View {
        HStack(spacing: 8) {
            Text(key)
                .font(.system(size: 10.5, weight: .semibold, design: .monospaced))
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Color.primary.opacity(0.08))
                .clipShape(RoundedRectangle(cornerRadius: 4, style: .continuous))

            Text(desc)
                .font(.system(size: 11))
                .foregroundColor(.secondary)
            Spacer()
        }
    }

    private var tipsSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(Localization.aboutTipsTitle)
                .font(.system(size: 12, weight: .bold))

            tipBullet(Localization.aboutTipHUDDrag)
            tipBullet(Localization.aboutTipPillMode)
            tipBullet(Localization.aboutTipAutoHide)
            tipBullet(Localization.aboutTipAllModels)
        }
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.primary.opacity(0.04))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    private func tipBullet(_ text: String) -> some View {
        HStack(alignment: .top, spacing: 6) {
            Text("•")
                .font(.system(size: 11, weight: .bold))
                .foregroundColor(.secondary)
            Text(text)
                .font(.system(size: 11))
                .foregroundColor(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}
