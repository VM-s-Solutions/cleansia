import Foundation
import XCTest

/// Pins the ADR-0025 day-one catalog gate: every build that registers FCM
/// tokens must carry every displayable event's loc-keys in every platform
/// language, or APNs renders the raw `push.*` key on the lock screen.
///
/// `events` mirrors `FcmMessageFactory.ApnsDisplayMap` and no count is stated
/// anywhere — a number in a doc comment goes stale silently, and this array
/// already did. The list itself is pinned server-side by
/// `ApnsDisplayMapIosCatalogSyncTests`, which reads this catalog off disk and
/// so fails on the backend PR that registers an event without shipping copy.
///
/// The partner-audience events are here too: a push is addressed to a USER's
/// device tokens, so a cleaner who also has this app installed receives them.
final class PushLocKeyCatalogTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]
    private let events = [
        "order.confirmed",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "dispute.reply",
        "recurring.scheduled",
        "loyalty.tier_upgrade",
        "membership.expiring_soon",
        "membership.cancellation_effective",
        "order.new_available",
        "order.preferred_offer",
        "order.assignment_cancelled",
        "payroll.invoice_paid"
    ]
    private let orderNumberArgEvents: Set<String> = [
        "order.confirmed",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "recurring.scheduled",
        "order.new_available",
        "order.preferred_offer",
        "order.assignment_cancelled"
    ]

    func testEveryPushLocKeyShipsInEveryLanguageTable() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for event in events {
                for key in ["push.\(event).title", "push.\(event).body"] {
                    let value = table[key]
                    XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                    XCTAssertFalse(value?.isEmpty ?? true, "\(key) empty in \(language).lproj")
                }
            }
        }
    }

    func testBodyArgSlotsMatchTheWireLocArgs() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for event in events {
                let body = try XCTUnwrap(table["push.\(event).body"], "push.\(event).body in \(language)")
                if orderNumberArgEvents.contains(event) {
                    XCTAssertTrue(
                        body.contains("%1$@"),
                        "push.\(event).body must carry the %1$@ loc-arg slot in \(language): \(body)"
                    )
                } else {
                    XCTAssertFalse(
                        body.contains("%"),
                        "push.\(event).body must be argless in \(language): \(body)"
                    )
                }
            }
        }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        let strings = lproj.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(
            NSDictionary(contentsOf: strings) as? [String: String],
            "Localizable.strings unreadable for \(language)"
        )
    }
}
