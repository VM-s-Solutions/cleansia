import CleansiaCore
import Foundation
import XCTest
@testable import CleansiaPartner

/// Copy is the whole feature here — a reservation is a promise, and the words are the promise. None of
/// these assertions is visible to the compiler: an untranslated row falls back to English, a dropped
/// placeholder renders as literal text, and a sentence that says too much is a policy breach nothing
/// else in the tree can see.
///
/// Read through the BUILT `.lproj` tables rather than the `.xcstrings` source, because a key present in
/// the catalog but absent from a shipped language renders as its own name on screen.
final class PendingOfferStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.partner") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let required = [
        "offers_title",
        "offers_subtitle",
        "offer_reserved_until_today",
        "offer_reserved_until_tomorrow",
        "offer_reserved_until_date",
        "offer_reserved_ended",
        "offer_confirm",
        "offer_slide_to_confirm",
        "offer_confirming",
        "offer_decline",
        "offer_decline_title",
        "offer_decline_body",
        "offer_decline_cta",
        "offer_declined_toast",
        "offer_empty",
        "offer_blocked_title",
        "offer_blocked_body",
        "offer_blocked_dismiss",
        "offers_card_title",
        "offers_card_more",
        "offers_card_cta"
    ]

    func testEveryOfferStringIsWrittenInAllFiveLanguages() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
                let value = table[key]
                XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                XCTAssertFalse(value?.isEmpty ?? true, "\(language).lproj leaves \(key) empty")
            }
        }
    }

    /// A dropped positional argument renders as literal text, in one language, on a screen a cleaner
    /// only reaches when the platform has already broken a promise to them.
    func testEveryPlaceholderSurvivesTranslation() throws {
        let expected = [
            "offer_reserved_until_today": ["%1$@"],
            "offer_reserved_until_tomorrow": ["%1$@"],
            "offer_reserved_until_date": ["%1$@", "%2$@"],
            "offer_blocked_body": ["%1$@"],
            "offers_card_more": ["%1$d"]
        ]
        for language in languages {
            let table = try localizableTable(for: language)
            for (key, placeholders) in expected {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                for placeholder in placeholders {
                    XCTAssertTrue(
                        value.contains(placeholder),
                        "\(language)/\(key) no longer carries \(placeholder): \"\(value)\""
                    )
                }
            }
        }
    }

    /// The customer hears ONE sentence whether the cleaner refused or simply never answered, and it
    /// never says which. So the cleaner's decline copy may not describe what the customer will be
    /// told — the moment it mentions them it is making a claim the platform has ruled it will not
    /// make. Naming the customer at all is the cheapest observable form of that breach.
    func testTheDeclineCopyNeverSaysWhatTheCustomerWillHear() throws {
        let customerWords = [
            "en": ["customer", "client"],
            "cs": ["zákazník"],
            "sk": ["zákazník"],
            "uk": ["клієнт"],
            "ru": ["клиент"]
        ]
        let keys = ["offer_decline_title", "offer_decline_body", "offer_decline_cta", "offer_declined_toast"]
        for language in languages {
            let table = try localizableTable(for: language)
            for key in keys {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)").lowercased()
                for word in customerWords[language] ?? [] {
                    XCTAssertFalse(
                        value.contains(word),
                        "\(language)/\(key) tells the cleaner what the customer is told: \"\(value)\""
                    )
                }
            }
        }
    }

    /// No surface may state a time-to-assignment, and the deadline is an INSTANT rather than a
    /// countdown for the same reason: the hold's real expiry is server-side. Copy that spells a
    /// duration re-encodes it where nothing can check it.
    func testNoOfferStringPromisesADuration() throws {
        let durationish = try NSRegularExpression(
            pattern: #"\d+\s*(min|hour|hod|hodin|god|час|хвил|мин|minút|minut)"#,
            options: [.caseInsensitive]
        )
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                let range = NSRange(value.startIndex..., in: value)
                XCTAssertNil(
                    durationish.firstMatch(in: value, range: range),
                    "\(language)/\(key) states a duration where only an instant is honest: \"\(value)\""
                )
            }
        }
    }

    /// Every refusal `TakeOrder`'s ordered chain can answer a confirm with. The framed dialog quotes the
    /// server's own reason inside the sentence that owns the failure, so a key with no resource would
    /// put a raw `order.weekly_limit_reached` where that reason belongs.
    func testEveryRefusalAConfirmCanHitResolvesToASentence() {
        let localizer = ApiErrorLocalizer()
        for key in [
            "order.not_found",
            "order.take.already_cancelled",
            "order.take.already_completed",
            "order.not_takeable",
            "order.no_available_spots",
            "order.employee_already_assigned",
            "order.weekly_limit_reached",
            "order.time_conflict"
        ] {
            let rendered = localizer.message(for: ApiError(code: key, httpStatus: 400))
            XCTAssertNotEqual(rendered, key, "\(key) renders raw inside the refusal framing")
            XCTAssertFalse(rendered.isEmpty, "\(key) renders empty inside the refusal framing")
        }
    }

    /// The framing takes the blame and the server's reason rides inside it; a body that dropped the
    /// reason would tell the cleaner nothing about why.
    func testTheRefusalFramingCarriesTheServersOwnReasonInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in languages {
            L10n.bundle = try localeBundle(language)
            let rendered = L10n.Offers.blockedBody("REASON-SENTINEL")
            XCTAssertTrue(rendered.contains("REASON-SENTINEL"), "\(language) dropped the reason: \(rendered)")
            XCTAssertFalse(rendered.contains("offer_blocked_body"), "\(language) left the key unresolved")
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

    private func localeBundle(_ tag: String) throws -> Bundle {
        let hosts = [Bundle.main, Bundle(for: Self.self)]
        let path = hosts.lazy.compactMap { $0.path(forResource: tag, ofType: "lproj") }.first
        let resolved = try XCTUnwrap(path, "no \(tag).lproj in the built bundle")
        return try XCTUnwrap(Bundle(path: resolved), "\(tag).lproj at \(resolved) is not a bundle")
    }
}
