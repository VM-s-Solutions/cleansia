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
        "offer_release_failed_title",
        "offer_release_failed_body",
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
            "offer_release_failed_body": ["%1$@"],
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
        let keys = [
            "offer_decline_title",
            "offer_decline_body",
            "offer_decline_cta",
            "offer_declined_toast",
            "offer_release_failed_title",
            "offer_release_failed_body"
        ]
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

    /// Both framings quote the server's own reason; a body that dropped it would tell the cleaner
    /// nothing about why.
    func testBothRefusalFramingsCarryTheServersOwnReasonInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in languages {
            L10n.bundle = try localeBundle(language)
            for (rendered, key) in [
                (L10n.Offers.blockedBody("REASON-SENTINEL"), "offer_blocked_body"),
                (L10n.Offers.releaseFailedBody("REASON-SENTINEL"), "offer_release_failed_body")
            ] {
                XCTAssertTrue(rendered.contains("REASON-SENTINEL"), "\(language)/\(key) dropped the reason")
                XCTAssertFalse(rendered.contains(key), "\(language) left \(key) unresolved")
            }
        }
    }

    /// The join between the state and the copy: a refusal carries its kind, and each kind reaches for
    /// its own sentence. Aliasing the two mappings is the same defect as aliasing the two catalog rows.
    func testEachRefusalKindReachesForItsOwnSentence() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        L10n.bundle = try localeBundle("en")

        let confirm = OfferRefusal(kind: .confirm, displayOrderNumber: "CL-1", reason: "R")
        let release = OfferRefusal(kind: .release, displayOrderNumber: "CL-1", reason: "R")

        XCTAssertEqual(confirm.headline, L10n.Offers.blockedTitle)
        XCTAssertEqual(confirm.message, L10n.Offers.blockedBody("R"))
        XCTAssertEqual(release.headline, L10n.Offers.releaseFailedTitle)
        XCTAssertEqual(release.message, L10n.Offers.releaseFailedBody("R"))
        XCTAssertNotEqual(confirm.message, release.message)
        XCTAssertEqual(release.title, "\(L10n.Offers.releaseFailedTitle) · CL-1")
    }

    /// A blank order number leaves the headline alone rather than trailing a naked separator.
    func testARefusalWithNoOrderNumberIsJustTheHeadline() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        L10n.bundle = try localeBundle("en")

        XCTAssertEqual(
            OfferRefusal(kind: .release, displayOrderNumber: "  ", reason: "R").title,
            L10n.Offers.releaseFailedTitle
        )
        XCTAssertEqual(
            OfferRefusal(kind: .confirm, displayOrderNumber: nil, reason: "R").title,
            L10n.Offers.blockedTitle
        )
    }

    /// A failure to TAKE and a failure to RELEASE are different failures. The take framing owns the
    /// platform's mistake — "we put your name on this job without checking" — and saying that when a
    /// release did not land assigns blame for something nobody did. Aliasing one key to the other is
    /// the cheapest way to reintroduce exactly that, so the two must never read the same.
    func testTheTwoRefusalFramingsAreNotTheSameSentence() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        for language in languages {
            L10n.bundle = try localeBundle(language)
            XCTAssertNotEqual(
                L10n.Offers.blockedTitle, L10n.Offers.releaseFailedTitle,
                "\(language) gives a refused release the title written for a refused confirm"
            )
            XCTAssertNotEqual(
                L10n.Offers.blockedBody("R"), L10n.Offers.releaseFailedBody("R"),
                "\(language) gives a refused release the body written for a refused confirm"
            )
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
