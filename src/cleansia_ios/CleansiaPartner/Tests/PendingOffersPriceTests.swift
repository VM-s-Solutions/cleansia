import XCTest
@testable import CleansiaPartner

/// A reserved job's price carries only the ISO code, and the formatter prints whatever symbol it is handed, so the
/// code has to be resolved first: Android prints `Kč` there (`PendingOffersScreen.kt`), and iOS printed `CZK`.
final class PendingOffersPriceTests: XCTestCase {
    func testTheOfferPriceTakesTheCurrencysOwnSymbol() throws {
        let source = try read("CleansiaPartner/Sources/Features/Orders/PendingOffersView.swift")
        XCTAssertTrue(
            source.contains("symbol: EarningsFormat.currencySymbol(offer.currencyCode)"),
            "the offer price does not resolve its currency's symbol"
        )
        XCTAssertFalse(source.contains("symbol: offer.currencyCode"), "the offer price prints the ISO code")
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }
}
