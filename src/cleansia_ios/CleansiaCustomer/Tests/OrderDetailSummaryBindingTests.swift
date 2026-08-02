import XCTest
@testable import CleansiaCustomer

/// `OrderDetailSummaryTests` proves the resolvers; a resolver nothing calls is
/// exactly the shipped defect — the per-source discount amounts rode the wire
/// for months while no view read them. These pin the call sites.
final class OrderDetailSummaryBindingTests: XCTestCase {
    private static let content = "CleansiaCustomer/Sources/Features/Orders/OrderDetailContent.swift"
    private static let summary = "CleansiaCustomer/Sources/Features/Orders/OrderDetailSummary.swift"
    private static let hero = "CleansiaCustomer/Sources/Features/Orders/OrderDetailHeroAndAddress.swift"

    func testTheDetailScreenRendersThePriceBreakdown() throws {
        let source = try read(Self.content)
        XCTAssertTrue(source.contains("OrderPriceBreakdownCard(order: order)"), "the price breakdown lost its slot")
    }

    /// The live hero carries status and nothing else, so without the strip the
    /// confirmation code the cleaner asks for at the door is unreachable for the
    /// three statuses in which a cleaner is actually coming.
    func testTheLiveHeroBranchStillCarriesTheFactsStrip() throws {
        let source = try read(Self.content)
        XCTAssertTrue(source.contains("LiveProgressHero(order: order)"))
        XCTAssertTrue(
            source.contains("OrderHeroFactsStrip(order: order)"),
            "the code is unreachable under the live hero"
        )
    }

    func testBothHeroesReadTheSameResolvedFacts() throws {
        XCTAssertTrue(try read(Self.hero).contains("OrderHeroFacts.resolve(order)"))
        XCTAssertTrue(try read(Self.summary).contains("OrderHeroFacts.resolve(order)"))
    }

    func testTheSummarySpellsNoLabelItself() throws {
        let source = try read(Self.summary)
        for hardcoded in ["Subtotal", "Total", "Payment", "Cash", "Card", "Paid", "Refunded"] {
            XCTAssertFalse(
                source.contains("\"\(hardcoded)"),
                "\(hardcoded) is a literal — cs/sk/uk/ru would read it in English"
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
