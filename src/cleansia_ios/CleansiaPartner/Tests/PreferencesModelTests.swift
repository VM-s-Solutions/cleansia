import CleansiaCore
import SwiftUI
import XCTest
@testable import CleansiaPartner

@MainActor
final class PreferencesModelTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        suiteName = "PreferencesModelTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        super.tearDown()
    }

    private func makeStore(locale: String = "en") -> UserDefaultsAppSettingsStore {
        UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { [locale] })
    }

    func testSeedsFromStore() {
        let store = makeStore()
        store.setLanguage("cs")
        store.setTheme(.dark)
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        XCTAssertEqual(model.languageTag, "cs")
        XCTAssertEqual(model.theme, .dark)
    }

    func testSetLanguageUpdatesPublishedAndStore() {
        let store = makeStore()
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        model.setLanguage("sk")
        XCTAssertEqual(model.languageTag, "sk")
        XCTAssertEqual(store.languageTag, "sk")
        XCTAssertEqual(model.locale.identifier, "sk")
    }

    func testSetLanguageUnsupportedClampsToResolvedTag() {
        let store = makeStore()
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        model.setLanguage("de")
        // Unsupported clears the store → resolves via locale ("en").
        XCTAssertEqual(model.languageTag, "en")
    }

    func testFreshInstallFollowsSystem() {
        let model = PreferencesModel(settings: makeStore(locale: "uk"), languageSync: SilentLanguageSync())
        XCTAssertTrue(model.isFollowingSystemLanguage)
        XCTAssertEqual(model.languageTag, "uk")
    }

    func testSetSystemClearsTagAndResolvesToDeviceLocale() {
        let store = makeStore(locale: "uk")
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        model.setLanguage("sk")
        XCTAssertFalse(model.isFollowingSystemLanguage)

        model.setSystemLanguage()
        XCTAssertTrue(model.isFollowingSystemLanguage)
        XCTAssertNil(store.persistedLanguageTag)
        // Resolves the UI bundle/locale to the device language.
        XCTAssertEqual(model.languageTag, "uk")
        XCTAssertEqual(model.locale.identifier, "uk")
    }

    func testLanguageThenSystemRoundTripsToUnset() {
        let store = makeStore(locale: "cs")
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        model.setLanguage("ru")
        XCTAssertEqual(store.persistedLanguageTag, "ru")
        model.setSystemLanguage()
        XCTAssertNil(store.persistedLanguageTag)
        XCTAssertTrue(model.isFollowingSystemLanguage)
    }

    func testSelectLanguageBySentinelIdFollowsSystem() {
        let store = makeStore(locale: "uk")
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        model.setLanguage("sk")

        model.selectLanguage(id: PreferencesLabels.systemLanguageId)

        XCTAssertTrue(model.isFollowingSystemLanguage)
        XCTAssertNil(store.persistedLanguageTag)
        XCTAssertEqual(model.languageTag, "uk")
    }

    func testSelectLanguageByTagPersistsExplicitChoice() {
        let store = makeStore(locale: "en")
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())

        model.selectLanguage(id: "cs")

        XCTAssertFalse(model.isFollowingSystemLanguage)
        XCTAssertEqual(store.persistedLanguageTag, "cs")
        XCTAssertEqual(model.languageTag, "cs")
    }

    func testSetThemeUpdatesPublishedAndStore() {
        let store = makeStore()
        let model = PreferencesModel(settings: store, languageSync: SilentLanguageSync())
        model.setTheme(.light)
        XCTAssertEqual(model.theme, .light)
        XCTAssertEqual(store.theme, .light)
    }

    func testThemeColorSchemeMapping() {
        XCTAssertNil(Theme.system.colorScheme)
        XCTAssertEqual(Theme.light.colorScheme, .light)
        XCTAssertEqual(Theme.dark.colorScheme, .dark)
    }
}

/// For the tests that are about the local preference state alone. What reaches the server is
/// `PartnerLanguageCallSiteTests`.
final class SilentLanguageSync: LanguagePreferenceSync {
    func send(languageCode _: String) {}
}

final class PreferencesLabelsTests: XCTestCase {
    func testLanguageLabelResolvesNativeName() {
        XCTAssertEqual(PreferencesLabels.languageLabel("cs"), "Čeština")
        XCTAssertEqual(PreferencesLabels.languageLabel("uk"), "Українська")
    }

    func testLanguageLabelFallsBackToTagWhenUnknown() {
        XCTAssertEqual(PreferencesLabels.languageLabel("xx"), "xx")
    }

    func testLanguageSummaryUsesNativeLabelWhenExplicit() {
        XCTAssertEqual(
            PreferencesLabels.languageSummary(isFollowingSystem: false, tag: "sk"),
            "Slovenčina"
        )
    }

    func testSystemLanguageIdIsNotARealTag() {
        XCTAssertFalse(
            UserDefaultsAppSettingsStore.supportedLanguageTags.contains(PreferencesLabels.systemLanguageId)
        )
    }

    func testLanguagesCoverTheSupportedSet() {
        XCTAssertEqual(
            Set(PreferencesLabels.languages.map(\.tag)),
            Set(UserDefaultsAppSettingsStore.supportedLanguageTags)
        )
    }

    func testThemesCoverAllCases() {
        XCTAssertEqual(PreferencesLabels.themes, Theme.allCases)
    }
}
