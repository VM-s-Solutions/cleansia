import XCTest
@testable import CleansiaCustomer

/// `PreferredOfferPresentation` decides three states and `PreferredOfferCard` writes them; a resolver
/// nothing calls is exactly the shipped defect this feature is fixing — `preferredOffer` has ridden the
/// customer wire since ADR-0045 landed and no client read it. This pins the call site.
///
/// Reads the source: the alternative is asserting over a rendered SwiftUI tree, which pins layout rather
/// than reachability. Sandboxed source reads fail locally under the Desktop TCC prompt and pass in CI,
/// like the sibling binding suites.
final class PreferredOfferBindingTests: XCTestCase {
    private static let content = "CleansiaCustomer/Sources/Features/Orders/OrderDetailContent.swift"

    func testTheOrderDetailRendersTheReservationItIsSent() throws {
        let source = try read(Self.content)

        XCTAssertTrue(
            source.contains("PreferredOfferPresentation.disclosure(for: order)"),
            "the order detail no longer asks what the reservation is"
        )
        XCTAssertTrue(
            source.contains("PreferredOfferCard(disclosure: disclosure)"),
            "the reservation is resolved and then thrown away"
        )
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
