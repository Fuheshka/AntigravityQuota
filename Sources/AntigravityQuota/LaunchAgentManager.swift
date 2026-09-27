import Foundation

public enum LaunchAgentManager {
    private static let label = "com.fuheshka.antigravity-quota"

    private static var plistURL: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/LaunchAgents/\(label).plist")
    }

    public static var isEnabled: Bool {
        FileManager.default.fileExists(atPath: plistURL.path)
    }

    public static func setEnabled(_ enabled: Bool) {
        let fm = FileManager.default
        if enabled {
            let executablePath = Bundle.main.executablePath ?? ProcessInfo.processInfo.arguments[0]
            let dict: [String: Any] = [
                "Label": label,
                "ProgramArguments": [executablePath],
                "RunAtLoad": true,
                "KeepAlive": false
            ]
            try? fm.createDirectory(
                at: plistURL.deletingLastPathComponent(),
                withIntermediateDirectories: true
            )
            if let data = try? PropertyListSerialization.data(
                fromPropertyList: dict,
                format: .xml,
                options: 0
            ) {
                try? data.write(to: plistURL, options: .atomic)
            }
        } else {
            try? fm.removeItem(at: plistURL)
        }
    }
}
