import Foundation
import XCTest

/// The Live Activity's copy lives in CleansiaCore's resource bundle inside the widget extension, and a
/// non-main bundle resolves only in a language its host bundle declares. These read the BUILT appex —
/// the path the widget really takes. `LiveActivityL10nTests` calls `CoreL10n.apply` and so never takes
/// it, which is how a card that was English on every phone passed it.
final class LiveActivityExtensionLocalizationTests: XCTestCase {
    private let languages = ["en", "cs", "sk", "uk", "ru"]

    private func widgetBundle() throws -> Bundle {
        let plugIns = try XCTUnwrap(Bundle.main.builtInPlugInsURL, "the app bundle has no PlugIns folder")
        let url = plugIns.appendingPathComponent("CleansiaCustomerLiveActivity.appex")
        return try XCTUnwrap(Bundle(url: url), "the Live Activity extension is not embedded at \(url.path)")
    }

    func testTheWidgetDeclaresEveryAppLanguage() throws {
        let declared = try XCTUnwrap(
            widgetBundle().object(forInfoDictionaryKey: "CFBundleLocalizations") as? [String],
            "the widget's Info.plist declares no CFBundleLocalizations"
        )
        XCTAssertEqual(Set(declared), Set(languages))
    }

    func testEveryPhoneLanguageResolvesToItselfInTheWidget() throws {
        let bundle = try widgetBundle()
        for language in languages {
            XCTAssertEqual(
                Bundle.preferredLocalizations(from: bundle.localizations, forPreferences: [language]).first,
                language,
                "a phone in \(language) would draw the widget in another language"
            )
        }
    }
}
