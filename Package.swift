// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AntigravityQuota",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .library(name: "AntigravityQuotaCore", targets: ["AntigravityQuotaCore"]),
        .executable(name: "AntigravityQuota", targets: ["AntigravityQuota"])
    ],
    targets: [
        .target(
            name: "AntigravityQuotaCore",
            path: "Sources/AntigravityQuotaCore"
        ),
        .executableTarget(
            name: "AntigravityQuota",
            dependencies: ["AntigravityQuotaCore"],
            path: "Sources/AntigravityQuota"
        ),
        .testTarget(
            name: "AntigravityQuotaTests",
            dependencies: ["AntigravityQuotaCore"],
            path: "Tests/AntigravityQuotaTests"
        )
    ]
)
