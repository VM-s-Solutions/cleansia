import CleansiaCore
import XCTest
@testable import CleansiaPartner

/// Three surfaces offer the cleaner a display language and all three funnel through
/// `PreferencesModel.selectLanguage`. They differ only in whether anyone is signed in, and that answer
/// decides whether a request is made at all.
@MainActor
final class PartnerLanguageCallSiteTests: XCTestCase {
    private var defaults: UserDefaults!
    private var suiteName: String!
    private var client: FakePartnerUserClient!

    override func setUp() {
        super.setUp()
        suiteName = "PartnerLanguageCallSiteTests.\(UUID().uuidString)"
        defaults = UserDefaults(suiteName: suiteName)
        client = FakePartnerUserClient()
    }

    override func tearDown() {
        defaults.removePersistentDomain(forName: suiteName)
        defaults = nil
        suiteName = nil
        client = nil
        super.tearDown()
    }

    /// The profile hub's picker: an approved cleaner with a filled-in profile.
    func testTheSettingsPickerTellsTheServerWhichLanguageWasPicked() async {
        let model = makeModel(signedIn: true)

        model.selectLanguage(id: "uk")

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(client.updates.map(\.languageCode), ["uk"])
        XCTAssertEqual(client.updates.first?.firstName, "Ondrej")
    }

    /// The registration lock is the only surface a cleaner sees between signing up and being approved,
    /// so it owns the language control for that window — and the push has to work there. `UserController`
    /// is deliberately not one of the mobile partner host's `[RequireCompleteProfile]` controllers, and a
    /// cleaner who has not filled a phone or a birth date yet is exactly this screen's population.
    func testTheRegistrationLockPickerPushesForACleanerWhoIsNotApprovedYet() async {
        client.currentUser = .success(.stub(phoneNumber: nil, birthDate: nil))
        let model = makeModel(signedIn: true)

        model.selectLanguage(id: "uk")

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(client.updates.map(\.languageCode), ["uk"])
        XCTAssertEqual(client.updates.first?.phoneNumber, "")
        XCTAssertNil(client.updates.first?.birthDate)
    }

    /// The pre-auth intro carousel. Nobody is signed in, so there is no row to update: the session is
    /// answered from the token store rather than by letting the call 401, because on a handset that has
    /// never signed in a 401 wakes the shared refresh path for nothing.
    func testTheIntroCarouselWritesLocallyAndMakesNoRequestAtAll() async {
        client.read.isInverted = true
        let model = makeModel(signedIn: false)

        model.selectLanguage(id: "uk")

        await fulfillment(of: [client.read], timeout: 0.5)
        XCTAssertEqual(client.updates, [])
        XCTAssertEqual(model.languageTag, "uk")
    }

    /// "System" is a local state, not a language: the server cannot see this handset's locale and the
    /// backend validator only accepts one of the five supported codes.
    func testFollowingTheSystemPushesTheResolvedTagNotTheSentinel() async {
        client.currentUser = .success(.stub(preferredLanguageCode: "cs"))
        let model = makeModel(signedIn: true)

        model.selectLanguage(id: PreferencesLabels.systemLanguageId)

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(client.updates.map(\.languageCode), [model.languageTag])
        XCTAssertFalse(client.updates.map(\.languageCode).contains(PreferencesLabels.systemLanguageId))
    }

    func testAFailedPushLeavesTheLocalPreferenceApplied() async {
        client.updateResult = .failure(ApiError(httpStatus: 400))
        let model = makeModel(signedIn: true)

        model.selectLanguage(id: "cs")

        await fulfillment(of: [client.pushed], timeout: 5)
        XCTAssertEqual(model.languageTag, "cs")
        XCTAssertFalse(model.isFollowingSystemLanguage)
    }

    private func makeModel(signedIn: Bool) -> PreferencesModel {
        PreferencesModel(
            settings: UserDefaultsAppSettingsStore(defaults: defaults, preferredLanguageTags: { ["en"] }),
            languageSync: LiveLanguagePreferenceSync(
                tokenStore: SessionTokenStore(signedIn: signedIn),
                client: client
            )
        )
    }
}
