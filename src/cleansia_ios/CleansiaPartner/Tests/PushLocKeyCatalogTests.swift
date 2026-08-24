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
final class PushLocKeyCatalogTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
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
        "order.preferred_offer_closed",
        "order.assignment_cancelled",
        "order.assigned",
        "order.assignment_revoked",
        "payroll.invoice_paid",
        "order.reminder_tomorrow",
        "order.reminder_soon",
        "order.reminder_not_started",
        "employee.weekly_limit_set"
    ]

    /// One-slot events, despite the name — `order.reminder_tomorrow` and
    /// `employee.weekly_limit_set` carry a COUNT through the same single `%1$@`.
    /// APNs loc-args are always strings, so every slot is `%1$@` whatever the
    /// backend put in it.
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
        "order.preferred_offer_closed",
        "order.assignment_cancelled",
        "order.assigned",
        "order.assignment_revoked",
        "order.reminder_tomorrow",
        "order.reminder_soon",
        "order.reminder_not_started",
        "employee.weekly_limit_set"
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

    /// A decline and a silent lapse produce the same event, so the copy may name neither outcome —
    /// stems for both "refused" and "did not answer", per locale.
    private let outcomeWords = [
        "en": ["declin", "refus", "reject", "turned down", "answer", "respond", "repl", "ignor"],
        "cs": ["odmít", "odmit", "odpověd", "neozval", "reagoval"],
        "sk": ["odmiet", "odmietn", "odpoved", "neozval", "reagoval"],
        "uk": ["відмов", "відповід", "відповів", "зреагував"],
        "ru": ["отказ", "отклон", "ответ", "отклик", "отреагиров"]
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

    /// One producer, three paths — an explicit decline, a five-minute lapse, and a cleaner taking a
    /// conflicting job. The customer is told the hold ended and never which of the three ended it.
    func testThePreferredOfferClosureNamesNeitherOutcome() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            let words = try XCTUnwrap(outcomeWords[language])
            for key in [
                "push.order.preferred_offer_closed.title",
                "push.order.preferred_offer_closed.body"
            ] {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                for word in words {
                    XCTAssertNil(
                        value.range(of: word, options: .caseInsensitive),
                        "\(key) says \(word) in \(language): \(value)"
                    )
                }
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
