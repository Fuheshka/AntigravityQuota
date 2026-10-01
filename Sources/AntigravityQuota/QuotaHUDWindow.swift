import Cocoa
import SwiftUI
import AntigravityQuotaCore

@MainActor
public final class QuotaViewModel: ObservableObject {
    @Published public var snapshot: QuotaSnapshot? {
        didSet { onLayoutChange?() }
    }
    @Published public var geminiBurnRate: QuotaBurnRate? {
        didSet { onLayoutChange?() }
    }
    @Published public var claudeBurnRate: QuotaBurnRate? {
        didSet { onLayoutChange?() }
    }
    @Published public var sparklineData: QuotaSparklineData = QuotaHistoryTracker.shared.sparklineData() {
        didSet { onLayoutChange?() }
    }
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

struct QuotaSparklineView: View {
    let data: QuotaSparklineData

    private func formatDuration(_ duration: TimeInterval) -> String {
        let hours = max(1, Int((duration / 3600.0).rounded()))
        return Localization.isRussian ? "~\(hours)ч" : "~\(hours)h"
    }

    private func curvePaths(for points: [CGPoint], height: CGFloat) -> (line: Path, area: Path) {
        var line = Path()
        guard let first = points.first else { return (line, line) }
        line.move(to: first)

        if points.count == 2 {
            line.addLine(to: points[1])
        } else {
            for i in 0..<(points.count - 1) {
                let p0 = points[i]
                let p1 = points[i + 1]
                let dx = p1.x - p0.x
                let cp1 = CGPoint(x: p0.x + dx * 0.5, y: p0.y)
                let cp2 = CGPoint(x: p0.x + dx * 0.5, y: p1.y)
                line.addCurve(to: p1, control1: cp1, control2: cp2)
            }
        }

        var area = line
        if let last = points.last {
            area.addLine(to: CGPoint(x: last.x, y: height))
            area.addLine(to: CGPoint(x: first.x, y: height))
            area.closeSubpath()
        }

        return (line, area)
    }

    var body: some View {
        VStack(spacing: 5) {
            // Legend
            HStack(spacing: 8) {
                HStack(spacing: 4) {
                    Circle()
                        .fill(Color(red: 0.28, green: 0.65, blue: 1.0))
                        .frame(width: 5, height: 5)
                    Text("Gemini")
                        .font(.system(size: 9.5, weight: .medium))
                        .foregroundColor(.white.opacity(0.72))
                }

                HStack(spacing: 4) {
                    Circle()
                        .fill(Color(red: 0.78, green: 0.48, blue: 1.0))
                        .frame(width: 5, height: 5)
                    Text("Claude")
                        .font(.system(size: 9.5, weight: .medium))
                        .foregroundColor(.white.opacity(0.72))
                }

                Spacer()

                if data.hasSufficientData {
                    Text(formatDuration(data.windowDuration))
                        .font(.system(size: 9, weight: .regular, design: .monospaced))
                        .foregroundColor(.white.opacity(0.45))
                }
            }

            // Chart area
            ZStack {
                RoundedRectangle(cornerRadius: 5, style: .continuous)
                    .fill(Color.white.opacity(0.04))

                if !data.hasSufficientData {
                    GeometryReader { geo in
                        ZStack {
                            Path { p in
                                p.move(to: CGPoint(x: 4, y: geo.size.height / 2))
                                p.addLine(to: CGPoint(x: geo.size.width - 4, y: geo.size.height / 2))
                            }
                            .stroke(style: StrokeStyle(lineWidth: 1, dash: [4, 4]))
                            .foregroundColor(Color.white.opacity(0.18))

                            HStack(spacing: 4) {
                                Image(systemName: "chart.xyaxis.line")
                                    .font(.system(size: 8.5))
                                Text(Localization.sparklineCollectingData)
                                    .font(.system(size: 9, weight: .medium))
                            }
                            .foregroundColor(Color.white.opacity(0.55))
                            .padding(.horizontal, 7)
                            .padding(.vertical, 2)
                            .background(
                                Capsule()
                                    .fill(Color.black.opacity(0.55))
                                    .overlay(Capsule().strokeBorder(Color.white.opacity(0.12), lineWidth: 0.5))
                            )
                        }
                    }
                } else {
                    GeometryReader { geo in
                        let size = geo.size
                        let windowStart = Date().addingTimeInterval(-data.windowDuration)

                        let gCoords = QuotaSparklineBuilder.normalizedPoints(
                            for: data.geminiPoints,
                            in: size,
                            windowStart: windowStart,
                            windowDuration: data.windowDuration,
                            topInset: 3.0,
                            bottomInset: 3.0
                        )

                        let cCoords = QuotaSparklineBuilder.normalizedPoints(
                            for: data.claudePoints,
                            in: size,
                            windowStart: windowStart,
                            windowDuration: data.windowDuration,
                            topInset: 3.0,
                            bottomInset: 3.0
                        )

                        ZStack {
                            // Midline reference (50%)
                            Path { p in
                                p.move(to: CGPoint(x: 0, y: size.height / 2))
                                p.addLine(to: CGPoint(x: size.width, y: size.height / 2))
                            }
                            .stroke(style: StrokeStyle(lineWidth: 0.5, dash: [3, 3]))
                            .foregroundColor(Color.white.opacity(0.08))

                            // Claude (Purple)
                            if cCoords.count >= 2 {
                                let (linePath, areaPath) = curvePaths(for: cCoords, height: size.height)
                                areaPath.fill(
                                    LinearGradient(
                                        colors: [
                                            Color(red: 0.78, green: 0.48, blue: 1.0).opacity(0.20),
                                            Color(red: 0.78, green: 0.48, blue: 1.0).opacity(0.01)
                                        ],
                                        startPoint: .top,
                                        endPoint: .bottom
                                    )
                                )
                                linePath.stroke(
                                    Color(red: 0.78, green: 0.48, blue: 1.0),
                                    style: StrokeStyle(lineWidth: 1.5, lineCap: .round, lineJoin: .round)
                                )
                                if let last = cCoords.last {
                                    Circle()
                                        .fill(Color(red: 0.78, green: 0.48, blue: 1.0))
                                        .frame(width: 3.5, height: 3.5)
                                        .position(last)
                                }
                            }

                            // Gemini (Cyan/Blue)
                            if gCoords.count >= 2 {
                                let (linePath, areaPath) = curvePaths(for: gCoords, height: size.height)
                                areaPath.fill(
                                    LinearGradient(
                                        colors: [
                                            Color(red: 0.28, green: 0.65, blue: 1.0).opacity(0.24),
                                            Color(red: 0.28, green: 0.65, blue: 1.0).opacity(0.01)
                                        ],
                                        startPoint: .top,
                                        endPoint: .bottom
                                    )
                                )
                                linePath.stroke(
                                    Color(red: 0.28, green: 0.65, blue: 1.0),
                                    style: StrokeStyle(lineWidth: 1.5, lineCap: .round, lineJoin: .round)
                                )
                                if let last = gCoords.last {
                                    Circle()
                                        .fill(Color(red: 0.28, green: 0.65, blue: 1.0))
                                        .frame(width: 3.5, height: 3.5)
                                        .position(last)
                                }
                            }
                        }
                    }
                }
            }
            .frame(height: 38)
            .clipped()
        }
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

    private func trendColor(for trend: QuotaTrend) -> Color {
        switch trend {
        case .burning:
            return Color(red: 0.96, green: 0.65, blue: 0.14) // Amber
        case .recovering:
            return Color(red: 0.20, green: 0.83, blue: 0.60) // Emerald
        case .stable:
            return Color.white.opacity(0.5)
        }
    }

    private var compactPillView: some View {
        let g5h = viewModel.snapshot?.geminiGroup?.fiveHourBucket
        let c5h = viewModel.snapshot?.claudeGroup?.fiveHourBucket

        let gTrend = viewModel.geminiBurnRate?.trend.rawValue ?? ""
        let cTrend = viewModel.claudeBurnRate?.trend.rawValue ?? ""

        return HStack(spacing: 8) {
            Circle()
                .fill(barColor(for: g5h?.percentage ?? 100))
                .frame(width: 7, height: 7)

            Text(String(format: "G %.0f%%%@", g5h?.percentage ?? 0, gTrend))
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

            Text(String(format: "C %.0f%%%@", c5h?.percentage ?? 0, cTrend))
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
                        weekly: gemini.weeklyBucket,
                        burnRate: viewModel.geminiBurnRate
                    )
                }
                if let claude = snap.claudeGroup {
                    poolRow(
                        title: "Claude / GPT",
                        fiveHour: claude.fiveHourBucket,
                        weekly: claude.weeklyBucket,
                        burnRate: viewModel.claudeBurnRate
                    )
                }

                Divider()
                    .overlay(Color.white.opacity(0.12))

                QuotaSparklineView(data: viewModel.sparklineData)

                // Bottom control buttons
                HStack(spacing: 6) {
                    Text("\(Localization.updatedPrefix) \(QuotaFormatter.formatClockTime(snap.updatedAt))")
                        .font(.system(size: 9, weight: .regular, design: .monospaced))
                        .foregroundColor(.white.opacity(0.45))

                    Spacer()

                    Button(action: { viewModel.onRefreshRequested?() }) {
                        HStack(spacing: 3.5) {
                            Image(systemName: "arrow.clockwise")
                                .font(.system(size: 9, weight: .bold))
                                .foregroundColor(viewModel.isRefreshing ? .yellow : .white.opacity(0.8))
                            Text(Localization.refreshShort)
                                .font(.system(size: 9.5, weight: .medium))
                                .foregroundColor(.white.opacity(0.8))
                        }
                        .padding(.horizontal, 6)
                        .padding(.vertical, 3)
                        .background(
                            RoundedRectangle(cornerRadius: 4, style: .continuous)
                                .fill(Color.white.opacity(0.08))
                        )
                    }
                    .buttonStyle(.plain)
                    .help(Localization.refreshNow)
                }
            } else {
                Text(Localization.serverOffline)
                    .font(.system(size: 11))
                    .foregroundColor(.white.opacity(0.65))
                    .padding(.vertical, 6)

                Divider()
                    .overlay(Color.white.opacity(0.12))

                QuotaSparklineView(data: viewModel.sparklineData)
            }
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 10)
        .frame(width: 256)
    }

    private func poolRow(
        title: String,
        fiveHour: QuotaBucket?,
        weekly: QuotaBucket?,
        burnRate: QuotaBurnRate?
    ) -> some View {
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

            if let burn = burnRate {
                HStack(spacing: 3) {
                    Text(burn.trend.rawValue)
                        .font(.system(size: 9.5, weight: .bold))
                        .foregroundColor(trendColor(for: burn.trend))
                    Text(burn.formatted(isRussian: Localization.isRussian))
                        .font(.system(size: 9.5, weight: .medium, design: .monospaced))
                        .foregroundColor(.white.opacity(0.72))
                    Spacer()
                }
            }

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

final class QuotaHUDPanel: NSPanel {
    var isSnappingEnabled: Bool = true
    var snapThreshold: CGFloat = 16.0

    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }

    override func setFrameOrigin(_ newOrigin: NSPoint) {
        guard isSnappingEnabled, let screenBounds = (screen ?? NSScreen.main)?.visibleFrame else {
            super.setFrameOrigin(newOrigin)
            return
        }
        let snapped = QuotaFormatter.snapOriginToScreenEdges(
            origin: newOrigin,
            size: frame.size,
            screenBounds: screenBounds,
            threshold: snapThreshold
        )
        super.setFrameOrigin(snapped)
    }
}

@MainActor
public final class QuotaHUDWindowController: NSObject {
    public let viewModel = QuotaViewModel()
    private var panel: NSPanel?

    private var flagsChangedGlobalMonitor: Any?
    private var flagsChangedLocalMonitor: Any?
    private var mouseUpGlobalMonitor: Any?
    private var mouseUpLocalMonitor: Any?

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

    public var isClickThroughEnabled: Bool {
        didSet {
            UserDefaults.standard.set(isClickThroughEnabled, forKey: "HUDClickThroughEnabled")
            updateModifierMonitors()
        }
    }

    public var hudOpacity: Double {
        didSet {
            let clamped = min(max(hudOpacity, 0.4), 1.0)
            UserDefaults.standard.set(clamped, forKey: "HUDOpacity")
            panel?.alphaValue = CGFloat(clamped)
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
        if defaults.object(forKey: "HUDClickThroughEnabled") == nil {
            defaults.set(false, forKey: "HUDClickThroughEnabled")
        }
        if defaults.object(forKey: "HUDOpacity") == nil {
            defaults.set(1.0, forKey: "HUDOpacity")
        }

        self.isHUDEnabled = defaults.bool(forKey: "HUDEnabled")
        self.onlyWhenAntigravityActive = defaults.bool(forKey: "HUDOnlyInAntigravity")
        self.isClickThroughEnabled = defaults.bool(forKey: "HUDClickThroughEnabled")
        let storedOpacity = defaults.double(forKey: "HUDOpacity")
        self.hudOpacity = (storedOpacity >= 0.4 && storedOpacity <= 1.0) ? storedOpacity : 1.0

        super.init()

        setupPanel()
        setupWorkspaceObserver()
        updateModifierMonitors()

        viewModel.onLayoutChange = { [weak self] in
            DispatchQueue.main.async {
                self?.resizePanelToFit()
            }
        }
        updateVisibility()
    }

    deinit {
        if let m = flagsChangedGlobalMonitor { NSEvent.removeMonitor(m) }
        if let m = flagsChangedLocalMonitor { NSEvent.removeMonitor(m) }
        if let m = mouseUpGlobalMonitor { NSEvent.removeMonitor(m) }
        if let m = mouseUpLocalMonitor { NSEvent.removeMonitor(m) }
    }

    private func setupPanel() {
        let hostingView = NSHostingView(rootView: QuotaHUDView(viewModel: viewModel))
        let fitting = hostingView.fittingSize

        let p = QuotaHUDPanel(
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
        p.alphaValue = CGFloat(hudOpacity)
        p.ignoresMouseEvents = isClickThroughEnabled
        p.contentView = hostingView

        restoreOrPlaceDefaultPosition(for: p, size: fitting)

        NotificationCenter.default.addObserver(
            forName: NSWindow.didMoveNotification,
            object: p,
            queue: .main
        ) { [weak p] _ in
            guard let p = p else { return }
            if let screenBounds = (p.screen ?? NSScreen.main)?.visibleFrame {
                let snapped = QuotaFormatter.snapOriginToScreenEdges(
                    origin: p.frame.origin,
                    size: p.frame.size,
                    screenBounds: screenBounds,
                    threshold: 16.0
                )
                if snapped != p.frame.origin {
                    p.setFrameOrigin(snapped)
                }
            }
            UserDefaults.standard.set(Double(p.frame.origin.x), forKey: "HUDOriginX")
            UserDefaults.standard.set(Double(p.frame.origin.y), forKey: "HUDOriginY")
        }

        self.panel = p
    }

    private func restoreOrPlaceDefaultPosition(for window: NSWindow, size: NSSize) {
        let defaults = UserDefaults.standard
        if defaults.object(forKey: "HUDOriginX") != nil,
           defaults.object(forKey: "HUDOriginY") != nil {
            let x = defaults.double(forKey: "HUDOriginX")
            let y = defaults.double(forKey: "HUDOriginY")
            let restored = NSPoint(x: x, y: y)
            let screenBounds = (window.screen ?? NSScreen.main)?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1728, height: 1080)
            let snapped = QuotaFormatter.snapOriginToScreenEdges(
                origin: restored,
                size: size,
                screenBounds: screenBounds,
                threshold: 16.0
            )
            window.setFrameOrigin(snapped)
        } else if let screen = NSScreen.main?.visibleFrame {
            // Bottom-right corner with clean padding snapped
            let x = screen.maxX - size.width - 20
            let y = screen.minY + 24
            let initial = NSPoint(x: x, y: y)
            let snapped = QuotaFormatter.snapOriginToScreenEdges(
                origin: initial,
                size: size,
                screenBounds: screen,
                threshold: 16.0
            )
            window.setFrameOrigin(snapped)
        }
    }

    private func updateMouseEventsState(forceOptionActive: Bool? = nil) {
        guard let panel = panel else { return }
        if !isClickThroughEnabled {
            panel.ignoresMouseEvents = false
            return
        }
        let isOptionDown = forceOptionActive ?? NSEvent.modifierFlags.contains(.option)
        if isOptionDown {
            panel.ignoresMouseEvents = false
        } else {
            if NSEvent.pressedMouseButtons == 0 {
                panel.ignoresMouseEvents = true
            }
        }
    }

    private func updateModifierMonitors() {
        if isClickThroughEnabled {
            startModifierMonitors()
            updateMouseEventsState()
        } else {
            stopModifierMonitors()
            panel?.ignoresMouseEvents = false
        }
    }

    private func startModifierMonitors() {
        guard flagsChangedGlobalMonitor == nil else { return }

        flagsChangedGlobalMonitor = NSEvent.addGlobalMonitorForEvents(matching: .flagsChanged) { [weak self] event in
            Task { @MainActor in
                self?.handleFlagsChanged(event)
            }
        }

        flagsChangedLocalMonitor = NSEvent.addLocalMonitorForEvents(matching: .flagsChanged) { [weak self] event in
            Task { @MainActor in
                self?.handleFlagsChanged(event)
            }
            return event
        }

        mouseUpGlobalMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseUp, .rightMouseUp, .otherMouseUp]) { [weak self] _ in
            Task { @MainActor in
                self?.handleMouseUp()
            }
        }

        mouseUpLocalMonitor = NSEvent.addLocalMonitorForEvents(matching: [.leftMouseUp, .rightMouseUp, .otherMouseUp]) { [weak self] event in
            Task { @MainActor in
                self?.handleMouseUp()
            }
            return event
        }
    }

    private func stopModifierMonitors() {
        if let m = flagsChangedGlobalMonitor {
            NSEvent.removeMonitor(m)
            flagsChangedGlobalMonitor = nil
        }
        if let m = flagsChangedLocalMonitor {
            NSEvent.removeMonitor(m)
            flagsChangedLocalMonitor = nil
        }
        if let m = mouseUpGlobalMonitor {
            NSEvent.removeMonitor(m)
            mouseUpGlobalMonitor = nil
        }
        if let m = mouseUpLocalMonitor {
            NSEvent.removeMonitor(m)
            mouseUpLocalMonitor = nil
        }
    }

    private func handleFlagsChanged(_ event: NSEvent) {
        guard isClickThroughEnabled else { return }
        let isOptionDown = event.modifierFlags.contains(.option)
        updateMouseEventsState(forceOptionActive: isOptionDown)
    }

    private func handleMouseUp() {
        guard isClickThroughEnabled else { return }
        let isOptionDown = NSEvent.modifierFlags.contains(.option)
        if !isOptionDown {
            panel?.ignoresMouseEvents = true
        }
    }

    private func resizePanelToFit() {
        guard let panel = panel, let contentView = panel.contentView else { return }
        contentView.layoutSubtreeIfNeeded()
        let oldFrame = panel.frame
        let newSize = contentView.fittingSize
        let screenBounds = (panel.screen ?? NSScreen.main)?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1728, height: 1080)
        var targetFrame = QuotaFormatter.anchoredHUDFrame(
            oldFrame: oldFrame,
            newSize: newSize,
            screenBounds: screenBounds
        )
        let snappedOrigin = QuotaFormatter.snapOriginToScreenEdges(
            origin: targetFrame.origin,
            size: targetFrame.size,
            screenBounds: screenBounds,
            threshold: 16.0
        )
        targetFrame.origin = snappedOrigin
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
