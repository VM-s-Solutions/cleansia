import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// Country names in the reader's language, on the two profile dropdowns that offer them.
///
/// **The bug this pins is a source-of-truth bug, not a lookup bug.** The lookup was already correct —
/// it reads the `translations` map the API returns and falls back sensibly. What was wrong is where it
/// asked what language to use: `Locale.current`, which the in-app language switch never touches. That
/// switch repoints the string bundles but does not set `AppleLanguages`, so the process locale reports
/// the DEVICE language for the life of the app.
///
/// The visible result was a partner running the app in Czech on an English phone reading
/// "Czech Republic" in an otherwise fully Czech screen — the exact symptom `localizedName` exists to
/// prevent, reintroduced one layer down.
final class CountryLocalizationTests: XCTestCase {
    private func country(
        id: String = "country-cz",
        name: String? = "Czech Republic",
        isoCode: String? = "CZE",
        translations: [String: Translation]? = nil
    ) -> CountryListItem {
        CountryListItem(
            id: id,
            isoCode: isoCode,
            name: name,
            translations: translations
        )
    }

    private func translated(_ pairs: [String: String]) -> [String: Translation] {
        pairs.mapValues { Translation(name: $0) }
    }

    func testTheChosenLanguageWinsOverTheRawColumn() {
        let item = country(translations: translated(["cs": "Česko", "uk": "Чехія"]))

        XCTAssertEqual(item.localizedName(languageTag: "cs"), "Česko")
        XCTAssertEqual(item.localizedName(languageTag: "uk"), "Чехія")
    }

    /// A region-qualified tag must still hit a map keyed by the bare code. Matching `"cs-CZ"` against
    /// a `"cs"` key without narrowing silently never hits, which reads as "no translation exists".
    func testARegionQualifiedTagStillMatches() {
        let item = country(translations: translated(["cs": "Česko"]))

        XCTAssertEqual(item.localizedName(languageTag: "cs-CZ"), "Česko")
    }

    /// English is the raw column by definition — the seed stores every other language in the map.
    func testEnglishFallsThroughToTheRawColumn() {
        let item = country(translations: translated(["cs": "Česko"]))

        XCTAssertEqual(item.localizedName(languageTag: "en"), "Czech Republic")
    }

    /// A country seeded without this language still has to render something a person can read.
    func testAMissingTranslationFallsBackRatherThanBlanking() {
        let item = country(translations: translated(["cs": "Česko"]))

        XCTAssertEqual(item.localizedName(languageTag: "ru"), "Czech Republic")
        XCTAssertEqual(country(name: nil, translations: nil).localizedName(languageTag: "ru"), "CZE")
        XCTAssertEqual(
            country(name: nil, isoCode: nil, translations: nil).localizedName(languageTag: "ru"),
            "country-cz"
        )
    }

    /// A blank translation is an absent one — otherwise a half-seeded row renders an empty dropdown
    /// entry, which is worse than the English name it replaced.
    func testABlankTranslationIsTreatedAsMissing() {
        let item = country(translations: translated(["cs": "   "]))

        XCTAssertEqual(item.localizedName(languageTag: "cs"), "Czech Republic")
    }

    /// **The regression guard.** The default argument must follow the app's own language switch, not
    /// the device. If this ever reverts to `Locale.current` it passes on an English simulator and
    /// fails nowhere, which is exactly how the bug survived the first fix.
    func testTheDefaultFollowsTheInAppLanguageNotTheDevice() {
        let item = country(translations: translated(["cs": "Česko", "sk": "Česko (sk)"]))

        CoreL10n.apply(languageTag: "cs")
        XCTAssertEqual(item.localizedName(), "Česko")

        CoreL10n.apply(languageTag: "sk")
        XCTAssertEqual(item.localizedName(), "Česko (sk)")

        CoreL10n.apply(languageTag: "en")
        XCTAssertEqual(item.localizedName(), "Czech Republic")
    }
}
