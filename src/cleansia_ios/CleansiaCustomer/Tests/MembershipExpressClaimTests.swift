import XCTest
@testable import CleansiaCustomer

/// The management card dropped its express row one release before the subscribe and success screens
/// dropped theirs, and the claim went on shipping on both of them plus every locale in the catalog.
/// So the absence is pinned across all three surfaces and the string catalog at once, not card by card.
final class MembershipExpressClaimTests: XCTestCase {
    private static let screens = [
        "CleansiaCustomer/Sources/Features/Membership/MembershipManagementCard.swift",
        "CleansiaCustomer/Sources/Features/Membership/SubscribePlusScreen.swift",
        "CleansiaCustomer/Sources/Features/Membership/MembershipSuccessScreen.swift"
    ]

    private static let catalog = "CleansiaCustomer/Resources/Localizable.xcstrings"

    /// Every stem the perk was worded with across the five locales.
    private static let expressStems = ["express", "expres", "експрес", "экспресс"]

    func testNoMembershipScreenRendersAnExpressPerk() throws {
        for screen in Self.screens {
            let source = try read(screen)
            XCTAssertFalse(
                source.contains("perkExpress"),
                "\(screen) still renders the express perk — no pricing code waives the surcharge"
            )
        }
    }

    func testTheL10nAccessorsAreGone() throws {
        let source = try read("CleansiaCustomer/Sources/L10n+Membership.swift")
        XCTAssertFalse(source.contains("membership_perk_express"), "a retired key still has an accessor")
    }

    func testNoMembershipStringInAnyLocaleNamesTheExpressPerk() throws {
        let data = try Data(contentsOf: url(for: Self.catalog))
        let root = try XCTUnwrap(try JSONSerialization.jsonObject(with: data) as? [String: Any])
        let strings = try XCTUnwrap(root["strings"] as? [String: Any])

        var offending: [String] = []
        for (key, entry) in strings where key.hasPrefix("membership_") {
            guard
                let entry = entry as? [String: Any],
                let localizations = entry["localizations"] as? [String: Any]
            else { continue }
            for (locale, unit) in localizations {
                guard
                    let unit = unit as? [String: Any],
                    let stringUnit = unit["stringUnit"] as? [String: Any],
                    let value = stringUnit["value"] as? String
                else { continue }
                if Self.expressStems.contains(where: { value.range(of: $0, options: .caseInsensitive) != nil }) {
                    offending.append("\(locale)/\(key) = \(value)")
                }
            }
        }
        XCTAssertEqual(offending.sorted(), [], "the catalog advertises the express perk")
    }

    private func url(for relativePath: String) -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent(relativePath)
    }

    private func read(_ relativePath: String) throws -> String {
        try String(contentsOf: url(for: relativePath), encoding: .utf8)
    }
}
