import Foundation
import XCTest

/// Pins the ADR-0025 day-one catalog gate: every build that registers FCM
/// tokens must carry every displayable event's loc-keys in every platform
/// language, or APNs renders the raw `push.*` key on the lock screen.
///
/// `events` mirrors `FcmMessageFactory.ApnsDisplayMap` and no count is stated
/// anywhere — a number in a doc comment goes stale silently, and this array
/// already did. It may lead the map by an event and never trail it: the copy
/// ships before the registration, so an event is listed here for the length of
/// one backend change. The list itself is pinned server-side by
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
        "order.cleaner_assigned",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "order.starting_soon",
        "dispute.reply",
        "recurring.scheduled",
        "loyalty.tier_upgrade",
        "membership.expiring_soon",
        "membership.cancellation_effective",
        "order.new_available",
        "order.preferred_offer",
        "order.assignment_cancelled",
        "order.assigned",
        "order.assignment_revoked",
        "payroll.invoice_paid"
    ]
    private let orderNumberArgEvents: Set<String> = [
        "order.confirmed",
        "order.cleaner_assigned",
        "order.on_the_way",
        "order.in_progress",
        "order.completed",
        "order.cancelled",
        "order.refunded",
        "order.starting_soon",
        "recurring.scheduled",
        "order.new_available",
        "order.preferred_offer",
        "order.assignment_cancelled",
        "order.assigned",
        "order.assignment_revoked"
    ]

    /// The word each locale uses for the person — i.e. the claim being made.
    private let cleanerWord = [
        "en": "Cleaner",
        "cs": "Uklízeč",
        "sk": "Upratovač",
        "uk": "Клінер",
        "ru": "Клинер"
    ]

    /// The word each locale uses for "cancelled" — i.e. the claim `order.assignment_revoked`
    /// must never make.
    private let cancelledWord = [
        "en": "cancel",
        "cs": "zruš",
        "sk": "zruš",
        "uk": "скасов",
        "ru": "отмен"
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
                    XCTAssertEqual(
                        formatSpecifierCount(body), 1,
                        "push.\(event).body must carry exactly one slot in \(language): \(body)"
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

    /// `order.cleaner_assigned` is the only event that may claim a cleaner is on the
    /// job. `order.confirmed` is also produced by the Stripe webhook and by the
    /// customer confirming a recurring occurrence, on neither of which has a cleaner
    /// seen the order.
    func testNoConfirmationCopyClaimsACleaner() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            let word = try XCTUnwrap(cleanerWord[language])
            for key in ["push.order.confirmed.title", "push.order.confirmed.body"] {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                XCTAssertNil(
                    value.range(of: word, options: .caseInsensitive),
                    "\(key) still claims a cleaner in \(language): \(value)"
                )
            }
        }
    }

    func testTheCleanerClaimMovedToTheAssignmentTitleRatherThanBeingDeleted() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            let word = try XCTUnwrap(cleanerWord[language])
            let value = try XCTUnwrap(
                table["push.order.cleaner_assigned.title"],
                "push.order.cleaner_assigned.title in \(language)"
            )
            XCTAssertNotNil(
                value.range(of: word, options: .caseInsensitive),
                "push.order.cleaner_assigned.title no longer names the cleaner in \(language): \(value)"
            )
        }
    }

    /// `FcmMessageFactory` sends `loc-args` but never `title-loc-args`, so a specifier in a title
    /// reaches the lock screen verbatim.
    func testNoPushTitleCarriesAFormatSpecifier() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for event in events {
                let title = try XCTUnwrap(table["push.\(event).title"], "push.\(event).title in \(language)")
                XCTAssertEqual(
                    formatSpecifierCount(title), 0,
                    "push.\(event).title carries a format specifier in \(language): \(title)"
                )
            }
        }
    }

    /// `StringCatalogCompletenessTests` makes the same claim over the source catalogs; this one reads
    /// the COMPILED table, which is what APNs resolves — a key can ship untranslated only here.
    func testNoPushCopyIsLeftAtEnglish() throws {
        let english = try localizableTable(for: "en")
        for language in languages where language != "en" {
            let table = try localizableTable(for: language)
            for event in events {
                for key in ["push.\(event).title", "push.\(event).body"] {
                    let translated = try XCTUnwrap(table[key], "\(key) in \(language)")
                    XCTAssertNotEqual(
                        translated, english[key],
                        "\(key) is still English in \(language): \(translated)"
                    )
                }
            }
        }
    }

    /// An admin reassignment leaves the job going ahead with somebody else. A cleaner who repeats
    /// "cancelled" to the customer tells them their booking is off.
    func testTheRevokedAssignmentNeverSaysCancelled() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            let word = try XCTUnwrap(cancelledWord[language])
            for key in ["push.order.assignment_revoked.title", "push.order.assignment_revoked.body"] {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                XCTAssertNil(
                    value.range(of: word, options: .caseInsensitive),
                    "\(key) says \(word) in \(language): \(value)"
                )
            }
        }
    }

    private func formatSpecifierCount(_ value: String) -> Int {
        value.replacingOccurrences(of: "%%", with: "").filter { $0 == "%" }.count
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
