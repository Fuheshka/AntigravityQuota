import Foundation

/// Semantic versioning parser and comparator.
public enum SemVer {
    public static func clean(_ version: String) -> String {
        var str = version.trimmingCharacters(in: .whitespacesAndNewlines)
        if str.hasPrefix("v") || str.hasPrefix("V") {
            str.removeFirst()
        }
        return str
    }

    public static func parseComponents(_ version: String) -> [Int] {
        let cleaned = clean(version)
        // Split by '.' and extract leading digits from each segment
        return cleaned.split(separator: ".").compactMap { segment in
            let digits = segment.prefix(while: { $0.isNumber })
            return Int(digits)
        }
    }

    /// Returns true if candidateVersion is strictly newer than currentVersion.
    public static func isCandidate(_ candidate: String, newerThan current: String) -> Bool {
        let v1 = parseComponents(candidate)
        let v2 = parseComponents(current)

        let maxCount = max(v1.count, v2.count)
        guard maxCount > 0 else { return false }

        for i in 0..<maxCount {
            let part1 = i < v1.count ? v1[i] : 0
            let part2 = i < v2.count ? v2[i] : 0

            if part1 > part2 {
                return true
            } else if part1 < part2 {
                return false
            }
        }
        return false
    }
}

/// Target platforms supported by the asset selector.
public enum SupportedPlatform: String, Sendable, CaseIterable {
    case macOS
    case windows
    case linux
    case android

    public var preferredExtensions: [String] {
        switch self {
        case .macOS:
            return [".dmg", ".zip", ".tar.gz"]
        case .windows:
            return [".exe", ".msi", ".zip"]
        case .linux:
            return [".appimage", ".deb", ".rpm", ".tar.gz"]
        case .android:
            return [".apk"]
        }
    }
}

/// A release asset published on GitHub.
public struct GitHubAsset: Decodable, Equatable, Sendable {
    public let name: String
    public let browserDownloadUrl: URL

    public init(name: String, browserDownloadUrl: URL) {
        self.name = name
        self.browserDownloadUrl = browserDownloadUrl
    }

    enum CodingKeys: String, CodingKey {
        case name
        case browserDownloadUrl = "browser_download_url"
    }
}

/// GitHub release metadata payload.
public struct GitHubReleaseInfo: Decodable, Equatable, Sendable {
    public let tagName: String
    public let name: String?
    public let htmlUrl: URL
    public let body: String?
    public let publishedAt: String?
    public let assets: [GitHubAsset]

    public var cleanVersion: String {
        SemVer.clean(tagName)
    }

    public init(
        tagName: String,
        name: String?,
        htmlUrl: URL,
        body: String?,
        publishedAt: String?,
        assets: [GitHubAsset]
    ) {
        self.tagName = tagName
        self.name = name
        self.htmlUrl = htmlUrl
        self.body = body
        self.publishedAt = publishedAt
        self.assets = assets
    }

    enum CodingKeys: String, CodingKey {
        case tagName = "tag_name"
        case name
        case htmlUrl = "html_url"
        case body
        case publishedAt = "published_at"
        case assets
    }
}

/// Result of an update check attempt.
public enum UpdateCheckResult: Equatable, Sendable {
    case updateAvailable(newVersion: String, release: GitHubReleaseInfo, assetUrl: URL?)
    case upToDate(currentVersion: String)
    case throttled(lastCheck: Date)
    case failed(reason: String)
}

/// Universal GitHub Release Update Checker.
public struct UpdateChecker {

    /// Finds the best matching asset for the target platform based on extension priorities and platform filters.
    /// Excludes foreign OS assets to prevent cross-platform download collisions.
    public static func findBestAsset(in assets: [GitHubAsset], for platform: SupportedPlatform) -> GitHubAsset? {
        let valid = assets.filter { !$0.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

        func isForeign(_ name: String, target: SupportedPlatform) -> Bool {
            let lower = name.lowercased()
            switch target {
            case .macOS:
                if lower.hasSuffix(".exe") || lower.hasSuffix(".msi") ||
                   lower.hasSuffix(".appimage") || lower.hasSuffix(".deb") ||
                   lower.hasSuffix(".rpm") || lower.hasSuffix(".apk") || lower.hasSuffix(".aab") {
                    return true
                }
                if lower.contains("windows") || lower.contains("win32") || lower.contains("win64") ||
                   lower.contains("linux") || lower.contains("ubuntu") || lower.contains("debian") ||
                   lower.contains("android") {
                    return true
                }
                if lower.contains("-win.") || lower.contains("-win-") ||
                   lower.contains("_win.") || lower.contains("_win_") ||
                   lower.contains(".win.") {
                    return true
                }
                return false

            case .windows:
                if lower.hasSuffix(".dmg") || lower.hasSuffix(".pkg") ||
                   lower.hasSuffix(".appimage") || lower.hasSuffix(".deb") ||
                   lower.hasSuffix(".rpm") || lower.hasSuffix(".apk") || lower.hasSuffix(".aab") {
                    return true
                }
                if lower.contains("macos") || lower.contains("darwin") || lower.contains("osx") ||
                   lower.contains("apple") || lower.contains("linux") || lower.contains("ubuntu") ||
                   lower.contains("debian") || lower.contains("android") {
                    return true
                }
                if lower.contains("-mac.") || lower.contains("-mac-") ||
                   lower.contains("_mac.") || lower.contains("_mac_") ||
                   lower.contains(".mac.") {
                    return true
                }
                return false

            case .linux:
                if lower.hasSuffix(".dmg") || lower.hasSuffix(".pkg") ||
                   lower.hasSuffix(".exe") || lower.hasSuffix(".msi") ||
                   lower.hasSuffix(".apk") || lower.hasSuffix(".aab") {
                    return true
                }
                if lower.contains("macos") || lower.contains("darwin") || lower.contains("osx") ||
                   lower.contains("windows") || lower.contains("android") {
                    return true
                }
                return false

            case .android:
                if !lower.hasSuffix(".apk") && !lower.hasSuffix(".aab") {
                    return true
                }
                return false
            }
        }

        func hasPlatformAffinity(_ name: String, target: SupportedPlatform) -> Bool {
            let lower = name.lowercased()
            switch target {
            case .macOS:
                return lower.hasSuffix(".dmg") || lower.hasSuffix(".pkg") ||
                       lower.contains("macos") || lower.contains("darwin") || lower.contains("osx") ||
                       lower.contains("-mac.") || lower.contains("-mac-") || lower.contains("_mac.")
            case .windows:
                return lower.hasSuffix(".exe") || lower.hasSuffix(".msi") ||
                       lower.contains("windows") || lower.contains("win32") || lower.contains("win64") ||
                       lower.contains("win-x64") || lower.contains("win-arm64") ||
                       lower.contains("-win.") || lower.contains("-win-") || lower.contains("_win.")
            case .linux:
                return lower.hasSuffix(".appimage") || lower.hasSuffix(".deb") || lower.hasSuffix(".rpm") ||
                       lower.contains("linux") || lower.contains("ubuntu") || lower.contains("debian")
            case .android:
                return lower.hasSuffix(".apk") || lower.hasSuffix(".aab") || lower.contains("android")
            }
        }

        let nonForeign = valid.filter { !isForeign($0.name, target: platform) }

        // 1. Preferred pass: assets with explicit platform affinity matching preferred extensions
        let priorities = platform.preferredExtensions
        for ext in priorities {
            if let match = nonForeign.first(where: {
                $0.name.lowercased().hasSuffix(ext) && hasPlatformAffinity($0.name, target: platform)
            }) {
                return match
            }
        }

        // 2. Generic pass: fallback to any non-foreign asset matching preferred extensions
        for ext in priorities {
            if let match = nonForeign.first(where: { $0.name.lowercased().hasSuffix(ext) }) {
                return match
            }
        }

        return nil
    }

    /// Evaluates if an automated check should be throttled to prevent hitting GitHub's 60 req/hour rate limit.
    public static func shouldThrottle(
        lastCheck: Date?,
        now: Date = Date(),
        cooldown: TimeInterval = 86400,
        force: Bool = false
    ) -> Bool {
        if force { return false }
        guard let last = lastCheck else { return false }
        return now.timeIntervalSince(last) < cooldown
    }
}

/// Actor managing the asynchronous update check lifecycle, caching, and rate limiting.
public actor GitHubUpdateClient {
    public let repo: String
    public let currentVersion: String
    public let cooldown: TimeInterval
    private let lastCheckKey: String

    public init(
        repo: String,
        currentVersion: String,
        cooldown: TimeInterval = 86400,
        cacheKey: String = "AntigravityQuota_LastUpdateCheckTimestamp"
    ) {
        self.repo = repo
        self.currentVersion = currentVersion
        self.cooldown = cooldown
        self.lastCheckKey = cacheKey
    }

    public var lastCheckDate: Date? {
        let ts = UserDefaults.standard.double(forKey: lastCheckKey)
        guard ts > 0 else { return nil }
        return Date(timeIntervalSince1970: ts)
    }

    private func recordCheckTimestamp(_ date: Date) {
        UserDefaults.standard.set(date.timeIntervalSince1970, forKey: lastCheckKey)
    }

    /// Performs the update check against GitHub Releases API.
    public func checkForUpdates(
        platform: SupportedPlatform = .macOS,
        force: Bool = false,
        session: URLSession = .shared
    ) async -> UpdateCheckResult {
        let now = Date()

        if UpdateChecker.shouldThrottle(lastCheck: lastCheckDate, now: now, cooldown: cooldown, force: force) {
            return .throttled(lastCheck: lastCheckDate ?? now)
        }

        guard let url = URL(string: "https://api.github.com/repos/\(repo)/releases/latest") else {
            return .failed(reason: "Invalid GitHub repository URL")
        }

        var request = URLRequest(url: url)
        request.timeoutInterval = 4.0
        request.setValue("application/vnd.github.v3+json", forHTTPHeaderField: "Accept")
        request.setValue("AntigravityQuota/\(currentVersion)", forHTTPHeaderField: "User-Agent")

        do {
            let (data, response) = try await session.data(for: request)
            recordCheckTimestamp(now)

            guard let httpResponse = response as? HTTPURLResponse else {
                return .failed(reason: "Invalid server response")
            }

            guard httpResponse.statusCode == 200 else {
                return .failed(reason: "HTTP error: status \(httpResponse.statusCode)")
            }

            let release = try JSONDecoder().decode(GitHubReleaseInfo.self, from: data)
            let latestVersion = release.cleanVersion

            if SemVer.isCandidate(latestVersion, newerThan: currentVersion) {
                let bestAsset = UpdateChecker.findBestAsset(in: release.assets, for: platform)
                return .updateAvailable(
                    newVersion: latestVersion,
                    release: release,
                    assetUrl: bestAsset?.browserDownloadUrl ?? release.htmlUrl
                )
            } else {
                return .upToDate(currentVersion: currentVersion)
            }
        } catch {
            return .failed(reason: error.localizedDescription)
        }
    }
}
