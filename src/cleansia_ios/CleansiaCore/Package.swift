// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "CleansiaCore",
    defaultLocalization: "en",
    platforms: [.iOS(.v16)],
    products: [
        .library(name: "CleansiaCore", targets: ["CleansiaCore"])
    ],
    dependencies: [
        .package(url: "https://github.com/PhoneNumberKit/PhoneNumberKit", exact: "5.0.5")
    ],
    targets: [
        .target(
            name: "CleansiaCore",
            dependencies: [
                .product(name: "PhoneNumberKit", package: "PhoneNumberKit")
            ],
            path: "Sources/CleansiaCore",
            resources: [.process("Resources")]
        ),
        .testTarget(
            name: "CleansiaCoreTests",
            dependencies: ["CleansiaCore"],
            path: "Tests/CleansiaCoreTests"
        )
    ]
)
