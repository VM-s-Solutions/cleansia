// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "CleansiaCore",
    defaultLocalization: "en",
    platforms: [.iOS(.v16)],
    products: [
        .library(name: "CleansiaCore", targets: ["CleansiaCore"]),
    ],
    targets: [
        .target(
            name: "CleansiaCore",
            path: "Sources/CleansiaCore",
            resources: [.process("Resources")]
        ),
        .testTarget(
            name: "CleansiaCoreTests",
            dependencies: ["CleansiaCore"],
            path: "Tests/CleansiaCoreTests"
        ),
    ]
)
