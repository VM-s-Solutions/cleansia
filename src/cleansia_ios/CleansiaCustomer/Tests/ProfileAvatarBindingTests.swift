import XCTest
@testable import CleansiaCustomer

/// The customer's avatar disc is guarded for contrast in Core's `AvatarDiscBindingTests`, which reads
/// `ProfileAvatar.swift`. That guard only covers a surface that actually renders the shared disc, so
/// pin both consumers here: an inline copy of the disc on either screen would leave the Core guard
/// green while its own initials drifted back to an adaptive ink on the fixed-white circle.
final class ProfileAvatarBindingTests: XCTestCase {
    private static let consumers = [
        "CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift",
        "CleansiaCustomer/Sources/Features/Profile/EditProfileView.swift"
    ]

    func testBothAvatarSurfacesRenderTheSharedDisc() throws {
        for consumer in Self.consumers {
            let source = try read(consumer)
            XCTAssertTrue(source.contains("ProfileAvatar("), "\(consumer) no longer renders the shared disc")
        }
    }

    func testNeitherSurfaceDrawsItsOwnInitialsDisc() throws {
        for consumer in Self.consumers {
            let source = try read(consumer)
            XCTAssertFalse(
                source.contains("Text(initials"),
                "\(consumer) draws its own initials — the Core contrast guard cannot see it"
            )
        }
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
