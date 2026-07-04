import XCTest
@testable import CleansiaCore

final class AppSettingsStoreTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!

    override func setUp() {
        super.setUp()
        suiteName = "AppSettingsStoreTests.\(UUID().uuidString)"
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

    func testHasSeenOnboardingDefaultsFalse() {
        let store = makeStore(locale: "en")
        XCTAssertFalse(store.hasSeenOnboarding)
    }

    func testMarkOnboardingSeenPersistsAndSurvivesReinit() {
        makeStore(locale: "en").markOnboardingSeen()

        let reopened = makeStore(locale: "en")
        XCTAssertTrue(reopened.hasSeenOnboarding)
    }

    func testFreshStoreResetsOnboarding() throws {
        let freshSuite = "AppSettingsStoreTests.fresh.\(UUID().uuidString)"
        let other = try XCTUnwrap(UserDefaults(suiteName: freshSuite))
        defer { other.removePersistentDomain(forName: freshSuite) }
        let store = UserDefaultsAppSettingsStore(defaults: other, localeLanguageCode: { "en" })
        XCTAssertFalse(store.hasSeenOnboarding)
    }

    func testPerUserOnboardingIsScopedToTheUserId() {
        let store = makeStore(locale: "en")
        store.markOnboardingSeen(userId: "user-a")

        XCTAssertTrue(store.hasSeenOnboarding(userId: "user-a"))
        XCTAssertFalse(store.hasSeenOnboarding(userId: "user-b"))
        XCTAssertFalse(store.hasSeenOnboarding)
    }

    func testPerUserOnboardingPersistsAcrossReinit() {
        makeStore(locale: "en").markOnboardingSeen(userId: "user-a")
        XCTAssertTrue(makeStore(locale: "en").hasSeenOnboarding(userId: "user-a"))
    }

    func testGlobalOnboardingDoesNotLeakIntoPerUserFlag() {
        let store = makeStore(locale: "en")
        store.markOnboardingSeen()
        XCTAssertFalse(store.hasSeenOnboarding(userId: "user-a"))
    }

    func testLanguageResolvesPersistedInSetTag() {
        defaults.set("sk", forKey: "settings.language")
        let store = makeStore(locale: "en")
        XCTAssertEqual(store.languageTag, "sk")
    }

    func testLanguageIgnoresPersistedOutOfSetTagAndFallsBackToLocale() {
        defaults.set("de", forKey: "settings.language")
        let store = makeStore(locale: "cs")
        XCTAssertEqual(store.languageTag, "cs")
    }

    func testLanguageSeedsFromLocaleWhenNoPersistedTag() {
        let store = makeStore(locale: "uk")
        XCTAssertEqual(store.languageTag, "uk")
    }

    func testLanguageDefaultsToEnglishWhenLocaleOutOfSet() {
        let store = makeStore(locale: "de")
        XCTAssertEqual(store.languageTag, "en")
    }

    func testLanguageDefaultsToEnglishWhenLocaleNil() {
        let store = makeStore(locale: nil)
        XCTAssertEqual(store.languageTag, "en")
    }

    func testSetLanguageRoundTripsAndPersists() {
        makeStore(locale: "en").setLanguage("uk")
        let reopened = makeStore(locale: "en")
        XCTAssertEqual(reopened.languageTag, "uk")
    }

    func testSetLanguageClampsUnsupportedTagAndFallsBackToLocale() {
        let store = makeStore(locale: "cs")
        store.setLanguage("sk")
        XCTAssertEqual(store.languageTag, "sk")
        store.setLanguage("de")
        // Unsupported tag clears the stored value → resolves via locale ("cs").
        XCTAssertEqual(store.languageTag, "cs")
    }

    func testPersistedLanguageTagNilWhenFollowingSystem() {
        let store = makeStore(locale: "uk")
        XCTAssertNil(store.persistedLanguageTag)
        XCTAssertEqual(store.languageTag, "uk")
    }

    func testPersistedLanguageTagSetAfterExplicitChoice() {
        let store = makeStore(locale: "uk")
        store.setLanguage("sk")
        XCTAssertEqual(store.persistedLanguageTag, "sk")
    }

    func testClearLanguageReturnsToFollowingSystem() {
        let store = makeStore(locale: "uk")
        store.setLanguage("sk")
        XCTAssertEqual(store.persistedLanguageTag, "sk")
        store.clearLanguage()
        XCTAssertNil(store.persistedLanguageTag)
        XCTAssertEqual(store.languageTag, "uk")
    }

    func testThemeDefaultsToSystem() {
        XCTAssertEqual(makeStore(locale: "en").theme, .system)
    }

    func testSetThemePersistsAndSurvivesReinit() {
        makeStore(locale: "en").setTheme(.dark)
        XCTAssertEqual(makeStore(locale: "en").theme, .dark)
    }

    func testSetThemeRoundTripsEachCase() {
        let store = makeStore(locale: "en")
        for theme in Theme.allCases {
            store.setTheme(theme)
            XCTAssertEqual(store.theme, theme)
        }
    }
}
