import CleansiaCore
import XCTest
@testable import CleansiaPartner

/// The CleansiaCore package scheme has no test action in the workspace, so the
/// store-extension coverage also runs here (Partner runs on the simulator and
/// @testable-imports Core) to guarantee it executes in CI.
final class AppSettingsStoreExtensionTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        suiteName = "AppSettingsStoreExtensionTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        super.tearDown()
    }

    private func makeStore(locale: String?) -> UserDefaultsAppSettingsStore {
        UserDefaultsAppSettingsStore(defaults: defaults, localeLanguageCode: { locale })
    }

    func testSetLanguageRoundTripsAndPersists() {
        makeStore(locale: "en").setLanguage("uk")
        XCTAssertEqual(makeStore(locale: "en").languageTag, "uk")
    }

    func testSetLanguageClampsUnsupportedTag() {
        let store = makeStore(locale: "cs")
        store.setLanguage("sk")
        XCTAssertEqual(store.languageTag, "sk")
        store.setLanguage("de")
        XCTAssertEqual(store.languageTag, "cs")
    }

    func testFollowsSystemUntilExplicitChoiceThenClearsBack() {
        let store = makeStore(locale: "uk")
        XCTAssertNil(store.persistedLanguageTag)
        store.setLanguage("sk")
        XCTAssertEqual(store.persistedLanguageTag, "sk")
        store.clearLanguage()
        XCTAssertNil(store.persistedLanguageTag)
        XCTAssertEqual(store.languageTag, "uk")
    }

    func testThemeDefaultsToSystemAndPersistsEachCase() {
        let store = makeStore(locale: "en")
        XCTAssertEqual(store.theme, .system)
        for theme in Theme.allCases {
            store.setTheme(theme)
            XCTAssertEqual(makeStore(locale: "en").theme, theme)
        }
    }
}
