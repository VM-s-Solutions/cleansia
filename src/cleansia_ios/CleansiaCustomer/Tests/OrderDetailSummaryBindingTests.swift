import XCTest
@testable import CleansiaCustomer

/// `OrderDetailSummaryTests` proves the resolvers; a resolver nothing calls is
/// exactly the shipped defect — the per-source discount amounts rode the wire
/// for months while no view read them. These pin the call sites.
final class OrderDetailSummaryBindingTests: XCTestCase {
    private static let content = "CleansiaCustomer/Sources/Features/Orders/OrderDetailContent.swift"
    private static let summary = "CleansiaCustomer/Sources/Features/Orders/OrderDetailSummary.swift"

    func testTheDetailScreenRendersThePriceBreakdown() throws {
        let source = try read(Self.content)
        XCTAssertTrue(source.contains("OrderPriceBreakdownCard(order: order)"), "the price breakdown lost its slot")
    }

    /// The hero carries status and nothing else, so without the strip the confirmation code the cleaner
    /// asks for at the door is unreachable.
    func testTheHeroIsFollowedByTheFactsStrip() throws {
        let source = try read(Self.content)
        XCTAssertTrue(source.contains("OrderStatusHero(order: order)"))
        XCTAssertTrue(
            source.contains("OrderHeroFactsStrip(order: order)"),
            "the code is unreachable under the hero"
        )
    }

    /// The strip used to render only under the live hero, so the confirmation code and the price were
    /// missing on a booked or a finished order. One hero now serves every status and the strip follows it
    /// unconditionally — a status gate reappearing here is that defect coming back.
    func testTheFactsStripIsNotGatedOnStatus() throws {
        let source = try read(Self.content)
        XCTAssertFalse(source.contains("usesLiveHero"), "the facts strip is behind a status gate again")
    }

    func testTheHeroFactsAreResolvedInOnePlace() throws {
        XCTAssertTrue(try read(Self.summary).contains("OrderHeroFacts.resolve(order)"))
    }

    /// The five-phase bar is the customer's only view of where the order sits, and it used to exist only
    /// for the three live statuses.
    func testTheTrackerRendersForEveryStatus() throws {
        let source = try read(Self.content)
        XCTAssertTrue(source.contains("CustomerOrderTrackerHero(status: status)"))
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
