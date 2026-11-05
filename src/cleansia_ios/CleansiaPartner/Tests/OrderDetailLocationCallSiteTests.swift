import XCTest
@testable import CleansiaPartner

/// A resolver test cannot see the call site, and the call site is where this defect lived: the wire
/// carried the coarse address and the seat block, and the views that own the detail's location and
/// crew simply never read them. `OrderDetail` carries no raw address or coordinate, so the head and the
/// run sheet cannot reach around `OrderLocation` and still compile — these pin what the compiler
/// cannot, that the views render *through* the resolvers at all. The detail has no map.
///
/// Evidence here is Swift source read off disk, which the simulator sandbox refuses on a
/// TCC-protected checkout — the same refusal every sibling binding suite hits locally.
final class OrderDetailLocationCallSiteTests: XCTestCase {
    private static let content = "CleansiaPartner/Sources/Features/Orders/OrderDetailContent.swift"
    private static let runSheet = "CleansiaPartner/Sources/Features/Orders/JobRunSheet.swift"
    private static let cards = "CleansiaPartner/Sources/Features/Orders/OrderDetailCards.swift"
    private static let frame = "CleansiaPartner/Sources/Features/Orders/JobDetailFrame.swift"

    func testTheHeadRendersWhicheverLocationArrived() throws {
        XCTAssertTrue(
            try read(Self.content).contains("order.location.line"),
            "the job head must render OrderLocation.line — the coarse zone is a location too"
        )
    }

    func testNavigateIsOfferedOnlyForAStreetAddress() throws {
        XCTAssertTrue(
            try read(Self.runSheet).contains("navigates: order.location.navigationTarget != nil"),
            "the run sheet's pills must gate Navigate on navigationTarget — a maps app opened on a city "
                + "name is worse than no button"
        )
        XCTAssertTrue(
            try read(Self.cards).contains("L10n.Orders.actionNavigate"),
            "the Navigate pill must still exist for the entitled reader"
        )
    }

    func testTheMapBehindThePanelOpensNoNavigateChoiceOnATap() throws {
        let source = try read(Self.frame)
        XCTAssertFalse(
            source.contains("navigateChoice"),
            "she pans and zooms the map behind the panel, so a tap on it is no ask to navigate: Navigate is the run "
                + "sheet's"
        )
        XCTAssertFalse(source.contains("onTapGesture"), "a tap on the map would fight her pan")
    }

    func testAPastJobSaysTheCustomerDetailsWereRemovedInsteadOfPromisingThem() throws {
        XCTAssertTrue(
            try read(Self.content).contains("order.customerDetailsClosedNote"),
            "the about panel must render customerDetailsClosedNote where the customer was — a past job "
                + "with no address otherwise reads as a job not yet taken"
        )
    }

    func testTheNotTakenScopeRendersTheSeatFacts() throws {
        let source = try read(Self.cards)
        XCTAssertTrue(
            source.contains("order.crew"),
            "the scope of a job not taken must read the resolved crew — TakeOrder's free-seat conjunct refuses "
                + "a take this screen otherwise never warned about"
        )
        XCTAssertTrue(
            source.contains("OrdersBoardRules.teamWords"),
            "the crew must be said in the board's own words, the one wording per fact"
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
