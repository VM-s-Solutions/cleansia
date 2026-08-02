import XCTest
@testable import CleansiaCustomer

/// `MembershipPerksTests` proves the resolver; it says nothing about whether the card still renders it.
/// The active card shipped with no perk row at all for a full release — the resolver would have been
/// green the whole time — so pin the call site too.
final class MembershipPerkRowBindingTests: XCTestCase {
    private static let card = "CleansiaCustomer/Sources/Features/Membership/MembershipManagementCard.swift"

    func testTheActiveCardRendersTheResolvedPerks() throws {
        let source = try read(Self.card)
        XCTAssertTrue(source.contains("MembershipPerks.resolve(membership)"), "the perk row lost its source")
        XCTAssertTrue(source.contains("PerkPill(perk: perk"), "the perks resolve but nothing draws them")
    }

    func testTheCardSpellsNoPerkLabelItself() throws {
        let source = try read(Self.card)
        for hardcoded in ["Express", "Recurring", "% off", "h cancel"] {
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
