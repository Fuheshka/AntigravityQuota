import Cocoa
import SwiftUI
import AntigravityQuotaCore

@MainActor
public final class QuotaViewModel: ObservableObject {
    @Published public var snapshot: QuotaSnapshot?
    @Published public var isRefreshing: Bool = false
    @Published public var isCompact: Bool {
        didSet {
            UserDefaults.standard.set(isCompact, forKey: "HUDCompactMode")
            onLayoutChange?()
        }
    }

    public var onRefreshRequested: (() -> Void)?
    public var onLayoutChange: (() -> Void)?

    public init() {
        self.isCompact = UserDefaults.standard.bool(forKey: "HUDCompactMode")
    }
}

struct VisualEffectBackground: NSViewRepresentable {
    let material: NSVisualEffectView.Material
    let blendingMode: NSVisualEffectView.BlendingMode

    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = blendingMode
        view.state = .active
        return view
    }

    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {
        nsView.material = material
        nsView.blendingMode = blendingMode
    }
}

struct QuotaHUDView: View {
    @ObservedObject var viewModel: QuotaViewModel

    private func barColor(for percentage: Double) -> Color {
        if percentage >= 50 {
            return Color(red: 0.20, green: 0.83, blue: 0.60) // Emerald
        } else if percentage >= 20 {
            return Color(red: 0.96, green: 0.65, blue: 0.14) // Amber
        } else {
            return Color(red: 0.95, green: 0.33, blue: 0.33) // Coral
        }
    }

    var body: some View {
        Group {
            if viewModel.isCompact {
                compactPillView
            } else {
                expandedCardView
            }
        }
        .background(
            VisualEffectBackground(material: .hudWindow, blendingMode: .behindWindow)
        )
        .clipShape(RoundedRectangle(cornerRadius: viewModel.isCompact ? 16 : 13, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: viewModel.isCompact ? 16 : 13, style: .continuous)
                .strokeBorder(Color.white.opacity(0.16), lineWidth: 1)
        )
    }

    private var compactPillView: some View {
        let g5h = viewModel.snapshot?.geminiGroup?.fiveHourBucket
        let c5h = viewModel.snapshot?.claudeGroup?.fiveHourBucket

        return HStack(spacing: 8) {
            Circle()
                .fill(barColor(for: g5h?.percentage ?? 100))
                .frame(width: 7, height: 7)

            Text(String(format: "G %.1f%%", g5h?.percentage ?? 0))
                .font(.system(size: 11.5, weight: .semibold, design: .monospaced))
                .foregroundColor(.white)

            if let reset = g5h?.resetDate {
                Text(QuotaFormatter.formatCountdown(to: reset))
                    .font(.system(size: 10.5, weight: .regular, design: .monospaced))
                    .foregroundColor(.white.opacity(0.65))
            }

            Text("·")
                .foregroundColor(.white.opacity(0.35))

            Circle()
                .fill(barColor(for: c5h?.percentage ?? 100))
                .frame(width: 7, height: 7)

            Text(String(format: "C %.0f%%", c5h?.percentage ?? 0))
                .font(.system(size: 11.5, weight: .semibold, design: .monospaced))
                .foregroundColor(.white)

            Button(action: { viewModel.isCompact = false }) {
                Image(systemName: "arrow.up.left.and.arrow.down.right")
                    .font(.system(size: 9.5, weight: .bold))
                    .foregroundColor(.white.opacity(0.9))
                    .frame(width: 18, height: 18)
                    .background(Circle().fill(Color.white.opacity(0.16)))
            }
            .buttonStyle(.plain)
            .help(Localization.isRussian ? "Развернуть карточку лимитов" : "Expand Quota Card")
        }
        .padding(.horizontal, 11)
        .padding(.vertical, 6)
        .contentShape(Rectangle())
        .onTapGesture {
            viewModel.isCompact = false
        }
    }

    private var expandedCardView: some View {
        VStack(alignment: .leading, spacing: 9) {
            // Header
            HStack(spacing: 6) {
                Image(systemName: "bolt.horizontal.circle.fill")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundColor(Color(red: 0.45, green: 0.72, blue: 1.0))

                Text("Antigravity Quota")
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundColor(.white.opacity(0.92))

                Spacer()

                Button(action: { viewModel.onRefreshRequested?() }) {
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 10.5, weight: .bold))
                        .foregroundColor(viewModel.isRefreshing ? .yellow : .white.opacity(0.75))
                }
                .buttonStyle(.plain)
                .help(Localization.refreshNow)

                Button(action: { viewModel.isCompact = true }) {
                    Image(systemName: "minus")
                        .font(.system(size: 10.5, weight: .bold))
                        .foregroundColor(.white.opacity(0.75))
                        .frame(width: 16, height: 16)
                }
                .buttonStyle(.plain)
                .help(Localization.compactHUDMode)
            }

            if let snap = viewModel.snapshot {
                if let gemini = snap.geminiGroup {
                    poolRow(
                        title: "Gemini",
                        fiveHour: gemini.fiveHourBucket,
                        weekly: gemini.weeklyBucket
                    )
                }
                if let claude = snap.claudeGroup {
                    poolRow(
                        title: "Claude / GPT",
                        fiveHour: claude.fiveHourBucket,
                        weekly: claude.weeklyBucket
                    )
                }
            } else {
                Text(Localization.serverOffline)
                    .font(.system(size: 11))
                    .foregroundColor(.white.opacity(0.65))
                    .padding(.vertical, 6)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .frame(width: 248)
    }

    private func poolRow(title: String, fiveHour: QuotaBucket?, weekly: QuotaBucket?) -> some View {
        let pct5h = fiveHour?.percentage ?? 0
        let pctWeekly = weekly?.percentage ?? 0

        return VStack(alignment: .leading, spacing: 4) {
            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text(title)
                    .font(.system(size: 11, weight: .medium))
                    .foregroundColor(.white.opacity(0.9))

                Spacer()

                Text(String(format: "%.1f%%", pct5h))
                    .font(.system(size: 12, weight: .bold, design: .monospaced))
                    .foregroundColor(barColor(for: pct5h))

                if let reset = fiveHour?.resetDate {
                    Text("(\(QuotaFormatter.formatCountdown(to: reset)))")
                        .font(.system(size: 10, weight: .regular, design: .monospaced))
                        .foregroundColor(.white.opacity(0.62))
                }
            }

            GeometryReader { geo in
                ZStack(alignment: .leading) {
                    Capsule()
                        .fill(Color.white.opacity(0.14))
                        .frame(height: 5)

                    Capsule()
                        .fill(barColor(for: pct5h))
                        .frame(width: max(4, geo.size.width * CGFloat(pct5h / 100.0)), height: 5)
                }
            }
            .frame(height: 5)

            if weekly != nil {
                HStack {
                    Text("\(Localization.weeklyWindow): \(String(format: "%.1f%%", pctWeekly))")
                        .font(.system(size: 9.5, weight: .regular, design: .monospaced))
                        .foregroundColor(.white.opacity(0.52))
                    Spacer()
                    if let wReset = weekly?.resetDate {
                        Text("↻ \(QuotaFormatter.formatCountdown(to: wReset))")
                            .font(.system(size: 9.5, weight: .regular, design: .monospaced))
                            .foregroundColor(.white.opacity(0.48))
                    }
                }
            }
        }
    }
}

@MainActor
public final class QuotaHUDWindowController: NSObject {
    public let viewModel = QuotaViewModel()
    private var panel: NSPanel?

    public var isHUDEnabled: Bool {
        didSet {
            UserDefaults.standard.set(isHUDEnabled, forKey: "HUDEnabled")
            updateVisibility()
        }
    }

    public var onlyWhenAntigravityActive: Bool {
        didSet {
            UserDefaults.standard.set(onlyWhenAntigravityActive, forKey: "HUDOnlyInAntigravity")
            updateVisibility()
        }
    }

    public override init() {
        let defaults = UserDefaults.standard
        if defaults.object(forKey: "HUDEnabled") == nil {
            defaults.set(true, forKey: "HUDEnabled")
        }
        if defaults.object(forKey: "HUDOnlyInAntigravity") == nil {
            defaults.set(true, forKey: "HUDOnlyInAntigravity")
        }
        self.isHUDEnabled = defaults.bool(forKey: "HUDEnabled")
        self.onlyWhenAntigravityActive = defaults.bool(forKey: "HUDOnlyInAntigravity")
        super.init()

        setupPanel()
        setupWorkspaceObserver()
        viewModel.onLayoutChange = { [weak self] in
            DispatchQueue.main.async {
                self?.resizePanelToFit()
            }
        }
        updateVisibility()
    }

    private func setupPanel() {
        let hostingView = NSHostingView(rootView: QuotaHUDView(viewModel: viewModel))
        let fitting = hostingView.fittingSize

        let p = NSPanel(
            contentRect: NSRect(origin: .zero, size: fitting),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        p.level = .floating
        p.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        p.isOpaque = false
        p.backgroundColor = .clear
        p.hasShadow = true
        p.isMovableByWindowBackground = true
        p.hidesOnDeactivate = false
        p.contentView = hostingView

        restoreOrPlaceDefaultPosition(for: p, size: fitting)

        NotificationCenter.default.addObserver(
            forName: NSWindow.didMoveNotification,
            object: p,
            queue: .main
        ) { [weak p] _ in
            guard let origin = p?.frame.origin else { return }
            UserDefaults.standard.set(Double(origin.x), forKey: "HUDOriginX")
            UserDefaults.standard.set(Double(origin.y), forKey: "HUDOriginY")
        }

        self.panel = p
    }

    private func restoreOrPlaceDefaultPosition(for window: NSWindow, size: NSSize) {
        let defaults = UserDefaults.standard
        if defaults.object(forKey: "HUDOriginX") != nil,
           defaults.object(forKey: "HUDOriginY") != nil {
            let x = defaults.double(forKey: "HUDOriginX")
            let y = defaults.double(forKey: "HUDOriginY")
            window.setFrameOrigin(NSPoint(x: x, y: y))
        } else if let screen = NSScreen.main?.visibleFrame {
            // Bottom-right corner with clean padding
            let x = screen.maxX - size.width - 20
            let y = screen.minY + 24
            window.setFrameOrigin(NSPoint(x: x, y: y))
        }
    }

    private func resizePanelToFit() {
        guard let panel = panel, let contentView = panel.contentView else { return }
        contentView.layoutSubtreeIfNeeded()
        let oldFrame = panel.frame
        let newSize = contentView.fittingSize
        let screenBounds = (panel.screen ?? NSScreen.main)?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1728, height: 1080)
        let targetFrame = QuotaFormatter.anchoredHUDFrame(
            oldFrame: oldFrame,
            newSize: newSize,
            screenBounds: screenBounds
        )
        panel.setFrame(targetFrame, display: true, animate: false)
    }

    private func setupWorkspaceObserver() {
        let nc = NSWorkspace.shared.notificationCenter
        nc.addObserver(
            forName: NSWorkspace.didActivateApplicationNotification,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.updateVisibility()
            }
        }
    }

    public func isAntigravityFrontmost() -> Bool {
        guard let front = NSWorkspace.shared.frontmostApplication else { return false }
        let bundleId = front.bundleIdentifier?.lowercased() ?? ""
        let name = front.localizedName?.lowercased() ?? ""
        return bundleId.contains("antigravity") || name.contains("antigravity")
    }

    public func updateVisibility() {
        guard let panel = panel else { return }
        resizePanelToFit()
        if !isHUDEnabled {
            panel.orderOut(nil)
            return
        }
        if !onlyWhenAntigravityActive || isAntigravityFrontmost() {
            panel.orderFrontRegardless()
        } else {
            panel.orderOut(nil)
        }
    }
}
