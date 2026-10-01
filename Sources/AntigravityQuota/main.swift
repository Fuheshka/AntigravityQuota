import Cocoa
import AntigravityQuotaCore

let command = CLIArguments.parse(args: CommandLine.arguments)

if command != .gui {
    switch command {
    case .help:
        print(CLIFormatter.helpMessage())
        exit(0)
    case .unknown(let flag):
        FileHandle.standardError.write(Data("Unknown option: \(flag)\n\n\(CLIFormatter.helpMessage())\n".utf8))
        exit(1)
    case .status:
        let semaphore = DispatchSemaphore(value: 0)
        var fetchedSnapshot: QuotaSnapshot?
        Task {
            fetchedSnapshot = await QuotaClient.shared.fetchSnapshot()
            semaphore.signal()
        }
        semaphore.wait()

        guard let snapshot = fetchedSnapshot else {
            FileHandle.standardError.write(Data("Error: Antigravity language_server is not reachable\n".utf8))
            exit(1)
        }
        print(CLIFormatter.formatStatus(snapshot: snapshot))
        exit(0)
    case .json:
        let semaphore = DispatchSemaphore(value: 0)
        var fetchedSnapshot: QuotaSnapshot?
        Task {
            fetchedSnapshot = await QuotaClient.shared.fetchSnapshot()
            semaphore.signal()
        }
        semaphore.wait()

        guard let snapshot = fetchedSnapshot else {
            FileHandle.standardError.write(Data("Error: Antigravity language_server is not reachable\n".utf8))
            exit(1)
        }
        do {
            let json = try CLIFormatter.formatJSON(snapshot: snapshot)
            print(json)
            exit(0)
        } catch {
            FileHandle.standardError.write(Data("Error: Failed to serialize JSON: \(error)\n".utf8))
            exit(1)
        }
    case .history:
        let records = QuotaHistoryTracker.shared.loadHistoryRecords()
        print(CLIFormatter.formatHistoryTable(records: records))
        exit(0)
    case .exportHistory:
        let records = QuotaHistoryTracker.shared.loadHistoryRecords()
        do {
            let json = try CLIFormatter.formatHistoryJSON(records: records)
            print(json)
            exit(0)
        } catch {
            FileHandle.standardError.write(Data("Error: Failed to serialize history JSON: \(error)\n".utf8))
            exit(1)
        }
    case .gui:
        break
    }
}

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

