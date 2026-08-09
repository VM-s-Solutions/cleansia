import XCTest
@testable import CleansiaPartner

/// A resolver test cannot see the call site, and the call site is where this defect lived: the wire
/// carried the coarse address and the seat block, and the views that own the detail's location and
/// crew simply never read them. `OrderDetail` no longer carries a raw address or coordinate, so the
/// customer card and the map backdrop cannot reach around `OrderLocation` and still compile — these
/// pin what the compiler cannot, that the views render *through* the resolvers at all.
///
/// Evidence here is Swift source read off disk, which the simulator sandbox refuses on a
/// TCC-protected checkout — the same refusal every sibling binding suite hits locally.
final class OrderDetailLocationCallSiteTests: XCTestCase {
    private static let cards = "CleansiaPartner/Sources/Features/Orders/OrderDetailCards.swift"
    private static let detailView = "CleansiaPartner/Sources/Features/Orders/OrderDetailView.swift"

    func testTheCustomerCardRendersWhicheverLocationArrived() throws {
        XCTAssertTrue(
            try read(Self.cards).contains("order.location.line"),
            "the customer card must render OrderLocation.line — the coarse zone is a location too"
        )
    }

    func testNavigateIsOfferedOnlyForAStreetAddress() throws {
        let source = try read(Self.cards)
        XCTAssertTrue(
            source.contains("order.location.navigationTarget"),
            "the customer card must gate Navigate on navigationTarget — a maps app opened on a city "
                + "name is worse than no button"
        )
        XCTAssertTrue(
            source.contains("L10n.Orders.actionNavigate"),
            "the Navigate chip must still exist for the entitled reader"
        )
    }

    func testTheMapBackdropIsResolvedNotReDerived() throws {
        XCTAssertTrue(
            try read(Self.detailView).contains("order.mapCoordinate"),
            "the detail view must take its map point from OrderDetail.mapCoordinate"
        )
    }

    func testTheScopeCardRendersTheSeatFacts() throws {
        let source = try read(Self.cards)
        XCTAssertTrue(
            source.contains("order.crew"),
            "the scope card must read the resolved crew — TakeOrder's free-seat conjunct refuses a "
                + "take this screen otherwise never warned about"
        )
        XCTAssertTrue(
            source.contains("OrdersFormat.crewLine"),
            "the scope card must render the crew through the shared \" · \" line the list card uses"
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
