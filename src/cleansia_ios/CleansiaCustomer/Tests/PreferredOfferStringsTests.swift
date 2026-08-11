import Foundation
import XCTest
@testable import CleansiaCustomer

/// Copy is the whole feature here. None of these assertions is visible to the compiler: an untranslated
/// row falls back to English, a dropped placeholder renders as literal text, and a sentence that says
/// too much is a policy breach nothing else in the tree can see.
///
/// Read through the BUILT `.lproj` tables rather than the `.xcstrings` source, because a key present in
/// the catalog but absent from a shipped language renders as its own name on screen.
final class PreferredOfferStringsTests: XCTestCase {
    private let appBundle = Bundle(identifier: "cz.cleansia.customer") ?? .main
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private let required = [
        "preferred_offer_section_title",
        "preferred_offer_asked_title",
        "preferred_offer_asked_body",
        "preferred_offer_accepted_title",
        "preferred_offer_closed_title",
        "preferred_offer_closed_body"
    ]

    /// A reservation is an ask, not a dispatch. No locale may tell the customer their cleaner IS coming,
    /// so no locale may use the word for it. Same stems the web catalog pins the same states against.
    private let assignmentWords = [
        "en": ["assign"],
        "cs": ["přiřaz", "prirazen"],
        "sk": ["priraden", "pridelen"],
        "uk": ["признач"],
        "ru": ["назнач"]
    ]

    /// The conduct ban. The customer is never told which way a reservation ended, and — the sentence the
    /// web lane wrote first and then dropped — never that anyone "hasn't answered" while it is still
    /// running. Stems for a refusal AND for a silence, per locale, applied to every key rather than only
    /// to the closed pair: the pending state is where the temptation to editorialise actually lives.
    private let conductWords = [
        "en": ["declin", "refus", "reject", "turned down", "answer", "respond", "repl", "ignor"],
        "cs": ["odmít", "odmit", "odpověd", "neozval", "reagoval"],
        "sk": ["odmiet", "odmietn", "odpoved", "neozval", "reagoval"],
        "uk": ["відмов", "відповід", "відповів", "зреагував"],
        "ru": ["отказ", "отклон", "ответ", "отклик", "отреагиров"]
    ]

    func testEveryPreferredOfferStringIsWrittenInAllFiveLanguages() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
                let value = table[key]
                XCTAssertNotNil(value, "\(key) missing from \(language).lproj")
                XCTAssertFalse(
                    value?.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ?? true,
                    "\(language).lproj leaves \(key) empty"
                )
            }
        }
    }

    func testTheFourTranslationsAreNotTheEnglishStringCopiedOver() throws {
        let english = try localizableTable(for: "en")
        for language in languages.dropFirst() {
            let table = try localizableTable(for: language)
            for key in required {
                XCTAssertNotEqual(table[key], english[key], "\(language).lproj left \(key) in English")
            }
        }
    }

    func testNoLocaleTellsTheCustomerACleanerIsAssignedToThem() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)").lowercased()
                for word in assignmentWords[language] ?? [] {
                    XCTAssertFalse(value.contains(word), "\(language)/\(key) promises an assignment: \"\(value)\"")
                }
            }
        }
    }

    /// The state a person is in is that person's to report. A decline and a silence resolve to one
    /// sentence, and while the ask is running the copy says what the PLATFORM is doing, never what the
    /// cleaner has or has not done.
    func testNoLocaleAttributesConductToTheCleaner() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)").lowercased()
                for word in conductWords[language] ?? [] {
                    XCTAssertFalse(
                        value.contains(word),
                        "\(language)/\(key) says what the cleaner did or did not do: \"\(value)\""
                    )
                }
            }
        }
    }

    /// Nothing in a pull board can keep a statement about when a cleaner will be on the job, so no
    /// surface may make one. The pending state carries an instant, and that instant is the end of the
    /// reservation — never a time-to-assignment, and never a duration re-encoded where nothing checks it.
    func testNoLocaleStatesADuration() throws {
        let durationish = try NSRegularExpression(
            pattern: #"\d+\s*(min|hour|hod|hodin|god|час|хвил|мин|minút|minut|godin|годин)"#,
            options: [.caseInsensitive]
        )
        for language in languages {
            let table = try localizableTable(for: language)
            for key in required {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                XCTAssertNil(
                    durationish.firstMatch(in: value, range: NSRange(value.startIndex..., in: value)),
                    "\(language)/\(key) states a duration where only an instant is honest: \"\(value)\""
                )
            }
        }
    }

    /// The closure names nobody. A placeholder there is the cheapest way to reintroduce "X didn't take
    /// it", which is the one thing this state exists not to say.
    func testTheClosureCarriesNoNameInAnyLanguage() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in ["preferred_offer_closed_title", "preferred_offer_closed_body"] {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                XCTAssertFalse(value.contains("%"), "\(language)/\(key) interpolates something: \"\(value)\"")
            }
        }
    }

    /// A dropped positional argument renders as literal text in one language only.
    func testTheTwoNamedStatesAndTheDeadlineSurviveTranslation() throws {
        for language in languages {
            let table = try localizableTable(for: language)
            for key in [
                "preferred_offer_asked_title",
                "preferred_offer_asked_body",
                "preferred_offer_accepted_title"
            ] {
                let value = try XCTUnwrap(table[key], "\(key) in \(language)")
                XCTAssertTrue(value.contains("%1$@"), "\(language)/\(key) lost its placeholder: \"\(value)\"")
            }
        }
    }

    /// The join between the state and the copy, in every language: the pending headline names the person
    /// asked, and the line under it carries the reservation's end and NOT their name. A body that named
    /// them would put a person next to a deadline, which is where "they are late" comes from.
    func testEachStateReachesForItsOwnSentenceInEveryLanguage() throws {
        let restore = L10n.bundle
        defer { L10n.bundle = restore }
        let deadline = Date(timeIntervalSince1970: 1_800_000_000)

        for language in languages {
            L10n.bundle = try localeBundle(language)
            let asked = PreferredOfferDisclosure.asked(cleanerName: "NAME-SENTINEL", respondBy: deadline)
            let accepted = PreferredOfferDisclosure.accepted(cleanerName: "NAME-SENTINEL")
            let locale = Locale(identifier: language)

            XCTAssertTrue(asked.headline.contains("NAME-SENTINEL"), "\(language) drops the name it asked")
            XCTAssertTrue(accepted.headline.contains("NAME-SENTINEL"), "\(language) drops the name that took it")
            XCTAssertNotEqual(asked.headline, accepted.headline, "\(language) says the same thing to both states")

            let askedDetail = try XCTUnwrap(asked.detail(locale: locale), "\(language) has no pending body")
            XCTAssertFalse(askedDetail.contains("NAME-SENTINEL"), "\(language) puts the person beside the deadline")
            XCTAssertTrue(
                askedDetail.contains(OrdersFormat.dateTime(deadline, locale: locale)),
                "\(language) dropped the end of the reservation"
            )

            XCTAssertNil(accepted.detail(locale: locale), "\(language) added a second line to a taken job")
            XCTAssertEqual(PreferredOfferDisclosure.closed.headline, L10n.PreferredOffer.closedTitle)
            XCTAssertEqual(PreferredOfferDisclosure.closed.detail(locale: locale), L10n.PreferredOffer.closedBody)
        }
    }

    private func localizableTable(for language: String) throws -> [String: String] {
        let strings = try localeBundle(language).bundleURL.appendingPathComponent("Localizable.strings")
        return try XCTUnwrap(
            NSDictionary(contentsOf: strings) as? [String: String],
            "Localizable.strings unreadable for \(language)"
        )
    }

    private func localeBundle(_ language: String) throws -> Bundle {
        let lproj = try XCTUnwrap(
            appBundle.url(forResource: language, withExtension: "lproj"),
            "\(language).lproj missing from the app bundle"
        )
        return try XCTUnwrap(Bundle(url: lproj), "\(language).lproj at \(lproj.path) is not a bundle")
    }
}
