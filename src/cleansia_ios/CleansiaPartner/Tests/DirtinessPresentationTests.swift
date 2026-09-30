import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The level is an integer on the wire, so these decode it the way the client does rather than
/// constructing the enum by hand — the board card and the detail read what the server sends.
final class DirtinessPresentationTests: XCTestCase {
    private static let levels: [DirtinessLevel] = [._0, ._1, ._2]

    func testTheDetailNamesEveryLevelInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in ["en", "cs", "sk", "uk", "ru"] {
            L10n.bundle = try localeBundle(language)
            let names = try Self.levels.map { try XCTUnwrap(L10n.Orders.dirtinessLevel($0), "\($0) in \(language)") }
            XCTAssertEqual(Set(names).count, Self.levels.count, "two levels share a name in \(language): \(names)")
            for name in names + [L10n.PeriodPay.dirtiness] {
                XCTAssertFalse(name.isEmpty, "empty dirtiness copy in \(language)")
                XCTAssertFalse(name.hasPrefix("dirtiness_level_"), "\(name) is unresolved in \(language)")
                XCTAssertFalse(name.hasPrefix("period_pay_"), "\(name) is unresolved in \(language)")
            }
        }
    }

    func testTheBoardCardFlagsOnlyADirtierThanNormalHome() {
        XCTAssertNil(OrdersFormat.dirtinessChip(._0))
        XCTAssertEqual(OrdersFormat.dirtinessChip(._1), L10n.Orders.dirtinessLevel(._1))
        XCTAssertEqual(OrdersFormat.dirtinessChip(._2), L10n.Orders.dirtinessLevel(._2))
        XCTAssertNotNil(OrdersFormat.dirtinessChip(._2))
    }

    func testAServerThatSendsNoLevelShowsNone() throws {
        XCTAssertNil(L10n.Orders.dirtinessLevel(nil))
        XCTAssertNil(OrdersFormat.dirtinessChip(nil))

        let card = try decode(OrderListItem.self, #"{ "id": "ord-1" }"#)
        XCTAssertNil(OrdersFormat.dirtinessChip(card.dirtinessLevel))
        XCTAssertNil(try OrderDetail(.wireComplete()).dirtinessLevel)
    }

    func testAHeavyJobOnTheBoardDecodesToTheHeavyChip() throws {
        let card = try decode(
            OrderListItem.self,
            #"{ "id": "ord-1", "dirtinessLevel": 2, "dirtinessSurchargeAmount": 720.0 }"#
        )

        XCTAssertEqual(card.dirtinessLevel, ._2)
        XCTAssertEqual(OrdersFormat.dirtinessChip(card.dirtinessLevel), L10n.Orders.dirtinessLevel(._2))
    }

    func testAnIncreasedJobDetailCarriesItsLevel() throws {
        let detail = try OrderDetail(decode(OrderItem.self, Self.detailJson(level: 1)))

        XCTAssertEqual(detail.dirtinessLevel, ._1)
        XCTAssertEqual(L10n.Orders.dirtinessLevel(detail.dirtinessLevel), L10n.Orders.dirtinessLevel(._1))
    }

    func testANormalJobDetailStillNamesItsLevel() throws {
        let detail = try OrderDetail(decode(OrderItem.self, Self.detailJson(level: 0)))

        XCTAssertEqual(detail.dirtinessLevel, ._0)
        XCTAssertNotNil(L10n.Orders.dirtinessLevel(detail.dirtinessLevel))
    }

    // MARK: - Where the level and the pay term are drawn

    func testTheBoardCardLeadsItsChipsWithTheLevel() throws {
        let source = try read("CleansiaPartner/Sources/Features/Orders/OrdersListComponents.swift")
        XCTAssertTrue(
            source.contains("let dirtiness = OrdersFormat.dirtinessChip(order.dirtinessLevel)"),
            "the board card no longer reads the order's level"
        )
        XCTAssertTrue(
            source.contains("if let dirtiness { ScopeChip(text: dirtiness) }"),
            "the board card no longer draws the level chip"
        )
    }

    func testTheScopeCardNamesTheLevel() throws {
        XCTAssertTrue(
            try read("CleansiaPartner/Sources/Features/Orders/OrderDetailCards.swift")
                .contains("if let dirtiness = L10n.Orders.dirtinessLevel(order.dirtinessLevel)"),
            "the job detail no longer names the level"
        )
    }

    func testThePayBreakdownShowsTheDirtinessTerm() throws {
        XCTAssertTrue(
            try read("CleansiaPartner/Sources/Features/Earnings/PeriodPayContent.swift")
                .contains("label: L10n.PeriodPay.dirtiness, amount: summary.totalDirtinessPay"),
            "the pay breakdown drops the dirtiness term"
        )
    }

    // MARK: - Helpers

    private static func detailJson(level: Int) -> String {
        """
        {
          "id": "ord-1",
          "displayOrderNumber": "ORD-1",
          "rooms": 3,
          "bathrooms": 1,
          "isAssignedToCurrentUser": false,
          "hasAfterPhotos": false,
          "dirtinessLevel": \(level),
          "dirtinessSurchargeAmount": 360.0
        }
        """
    }

    private func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        try JSONDecoder().decode(type, from: Data(json.utf8))
    }

    private func read(_ relativePath: String) throws -> String {
        let root = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        return try String(contentsOf: root.appendingPathComponent(relativePath), encoding: .utf8)
    }

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
