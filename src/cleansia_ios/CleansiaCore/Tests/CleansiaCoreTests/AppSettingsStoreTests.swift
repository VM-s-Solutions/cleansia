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
}
