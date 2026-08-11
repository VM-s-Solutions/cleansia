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
        UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { locale.map { [$0] } ?? [] })
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

    func testAnAnsweredPromptPersistsPerUserAndPerPrompt() {
        let store = makeStore(locale: "en")
        XCTAssertFalse(store.hasAnsweredPrompt("job_radius", userId: "emp-1"))

        store.markPromptAnswered("job_radius", userId: "emp-1")

        XCTAssertTrue(makeStore(locale: "en").hasAnsweredPrompt("job_radius", userId: "emp-1"))
        XCTAssertFalse(store.hasAnsweredPrompt("job_radius", userId: "emp-2"))
        XCTAssertFalse(store.hasAnsweredPrompt("other_prompt", userId: "emp-1"))
    }

    /// Two per-user one-shots on the same device must not answer each other.
    func testAnAnsweredPromptIsNotTheOnboardingFlag() {
        let store = makeStore(locale: "en")

        store.markPromptAnswered("job_radius", userId: "emp-1")

        XCTAssertFalse(store.hasSeenOnboarding(userId: "emp-1"))
        XCTAssertFalse(store.hasSeenOnboarding)
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
